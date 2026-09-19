using System;
using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Affixes.Behaviors;
using Kind = EliteEnemies.Affixes.PlayerEffectRelay.Kind;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 「作用于玩家」的效果的**转交层**。
    ///
    /// <para><b>为什么需要它</b>：这类效果（击退 / 换位 / 咬枪 / 强制换弹 / 掉落武器 / 偷窃）
    /// 在联机下<b>不能由主机替远端玩家做</b>——主机上那个只是<b>复制体</b>，
    /// 改它的移动/背包/武器到不了真人，下一帧还会被客机的同步覆盖回去。
    /// 所以只能把效果<b>连参数一起</b>转交给受害者那台机器，由它在自己的角色上执行。</para>
    ///
    /// <para><b>为什么不复用联机模组的 buff 转发</b>：那条路已经验证可用，但
    /// <c>PlayerBuffBroadcastRpc</c> 只有 <c>PlayerId / WeaponTypeId / BuffId</c> 三个字段，
    /// <b>传不了参数</b>——而击退要方向、换位要坐标、偷窃要物品 id。故走本模块自己的频道。</para>
    ///
    /// <para><b>单机不回归</b>：<see cref="TryRelay"/> 在"联机未启动"时返回 <c>false</c>，
    /// 调用方照常本地执行 ⇒ <b>单机路径一字未改</b>。
    /// 联机时主机<b>自己的玩家</b>也走本地（<c>IsMainCharacter</c>），行为与从前一致。</para>
    /// </summary>
    internal static class CoopPlayerEffect
    {
        /// <summary>换位时把玩家抬高的量——与 <c>PhaseSwapBehavior</c> 里一致，避免卡进地形。</summary>
        private static readonly Vector3 SwapOffset = Vector3.up * 0.1f;

        // ===== 主机侧：偷窃的令牌 → 那只精英 =====
        //
        // 回报时要知道"东西该加给谁"。**刻意用令牌而不是传精英的 aiId**：
        // 用 aiId 就得在主机上再把它反查成一个角色对象，那要碰联机模组的同步库；
        // 而发请求的那一刻，精英本来就在手里。
        private static readonly Dictionary<int, CharacterMainControl> s_pendingSteals =
            new Dictionary<int, CharacterMainControl>();

        private static int s_nextStealToken = 1;

        // ==================== 主机侧：转交 ====================

        /// <summary>
        /// 把效果转交给 <paramref name="victim"/> 那台机器执行。
        ///
        /// <returns>
        /// <c>true</c> = **已经处理**，调用方<b>不要</b>再本地执行；
        /// <c>false</c> = 照常本地执行（单机、联机未启动、或受害者就是主机自己）。
        /// </returns>
        /// </summary>
        public static bool TryRelay(CharacterMainControl victim, Kind kind,
                                    Vector3 v = default(Vector3), int i = 0)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;   // 单机 → 本地执行
            if (victim == null) return false;
            if (victim.IsMainCharacter) return false;                        // 主机自己的玩家 → 本地执行

            var playerId = CoopApi.GetPlayerIdFor(victim);
            if (string.IsNullOrEmpty(playerId))
            {
                // ⚠ 拿不到对方的网络 id ⇒ **既不远程也不本地**。
                //   本地执行是错的（改的是复制体，到不了真人），所以这里宁可什么都不做——
                //   但要**说出来**，否则又是一次静默失效。
                CoopLog.Warn($"[玩家效果] 拿不到受害者的网络 id，{kind} 未生效（既不远程也不本地）");
                return true;
            }

            CoopApi.Broadcast(CoopWire.EncodePlayerEffect(
                CoopWire.Kind.PlayerEffect, playerId, (byte)kind, v.x, v.y, v.z, i));

            return true;
        }

        /// <summary>
        /// 转交一次偷窃。
        ///
        /// <para>**单独开一个入口**（而不是让 <see cref="TryRelay"/> 处理 <c>Steal</c>）：
        /// 偷窃回报时要知道"东西该加给谁"，而<b>目标精英是调用方（行为）持有的、
        /// 不在 <c>victim</c> 身上</b>。所以这里顺手登记一条"令牌 → 那只精英"。</para>
        /// </summary>
        public static bool TryRelaySteal(CharacterMainControl victim, CharacterMainControl thief)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;
            if (victim == null || thief == null) return false;
            if (victim.IsMainCharacter) return false;

            var playerId = CoopApi.GetPlayerIdFor(victim);
            if (string.IsNullOrEmpty(playerId))
            {
                CoopLog.Warn("[玩家效果] 拿不到受害者的网络 id，偷窃未生效（既不远程也不本地）");
                return true;
            }

            int token = NextStealToken();
            s_pendingSteals[token] = thief;

            CoopApi.Broadcast(CoopWire.EncodePlayerEffect(
                CoopWire.Kind.PlayerEffect, playerId, (byte)Kind.Steal, 0f, 0f, 0f, token));

            return true;
        }

        private static int NextStealToken()
        {
            // 溢出时清空重来：令牌只是"这次请求"的把手，不需要跨会话唯一。
            if (s_nextStealToken >= int.MaxValue - 1)
            {
                s_nextStealToken = 1;
                s_pendingSteals.Clear();
            }

            if (s_pendingSteals.Count > 256)
            {
                // 客户端一直没回报（掉线等）——清掉陈旧的，别让表长着。
                s_pendingSteals.Clear();
                CoopLog.Warn("[玩家效果] 偷窃待回报表超过 256 条，已清空（客户端可能掉线了）");
            }

            return s_nextStealToken++;
        }

        /// <summary>
        /// 转交一次「在玩家头顶弹字」。
        ///
        /// <para><b>语义与 <see cref="TryRelay"/> 相同</b>（<c>true</c> = 已接管，调用方不要再本地弹），
        /// 但走的是**独立报文**——它要带"本地化键 + 兜底"两份文本，而玩家效果报文只有一个文本字段。</para>
        ///
        /// <para>⚠ <b>这条通道原先从未接通</b>：<c>PlayerEffectRelay.PlayerPopTextHandler</c>
        /// 全库没有赋值点，于是 <c>TryRelayPlayerPopText</c> 恒返回 false，
        /// 主机一律本地弹——弹在客机玩家的**复制体**上，真人看不到（实测确认）。</para>
        /// </summary>
        public static bool TryRelayPlayerPopText(CharacterMainControl victim, string key,
                                                 string fallback = null, string arg = null)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;   // 单机 → 本地弹
            if (victim == null || string.IsNullOrEmpty(key)) return false;
            if (victim.IsMainCharacter) return false;                        // 主机自己的玩家 → 本地弹

            var playerId = CoopApi.GetPlayerIdFor(victim);
            if (string.IsNullOrEmpty(playerId))
            {
                // 同 TryRelay：拿不到 id 时**既不远程也不本地**（本地弹在复制体上也看不见），
                // 但要说出来。
                CoopLog.Warn($"[玩家效果] 拿不到受害者的网络 id，弹字 '{key}' 未生效（既不远程也不本地）");
                return true;
            }

            CoopApi.Broadcast(CoopWire.EncodePlayerPopText(playerId, key, fallback, arg));
            return true;
        }

        /// <summary>客户端收到「请你在自己头顶弹一行字」——按**本机语言**解析那个键。</summary>
        public static void OnPlayerPopText(EliteMessage message)
        {
            if (!CoopApi.Active) return;
            if (!CoopApi.IsSelfPlayerId(message.TargetPlayerId)) return;   // 不是给我的
            if (string.IsNullOrEmpty(message.Text)) return;

            var player = CharacterMainControl.Main;
            if (player == null) return;

            // `Text` 是键，`ComboId` 被借来装兜底文本，`TextArg` 是格式参数（都见 CoopWire v8）。
            player.PopText(PlayerEffectRelay.Format(
                message.Text,
                string.IsNullOrEmpty(message.ComboId) ? null : message.ComboId,
                message.TextArg));
        }

        // ==================== 主机侧：收偷窃回报 ====================

        public static void OnEffectResult(EliteMessage message)
        {
            if (!CoopApi.Active) return;

            var kind = (Kind)message.Effect;
            if (kind != Kind.StealResult) return;

            if (!s_pendingSteals.TryGetValue(message.Ei, out var thief))
            {
                CoopLog.Warn($"[玩家效果] 收到未知令牌 {message.Ei} 的偷窃回报，已忽略");
                return;
            }

            s_pendingSteals.Remove(message.Ei);

            if (message.Ei2 < 0)
            {
                CoopLog.Info($"[玩家效果] 玩家 {message.TargetPlayerId} 背包里没有可偷的东西");
                return;
            }

            ThiefBehavior.OnRemoteStealCompleted(thief, message.Ei2);
        }

        // ==================== 客户端侧：执行 ====================

        /// <summary>收到「请某个玩家执行效果」的报文——是给自己的才执行。</summary>
        public static void OnEffect(EliteMessage message)
        {
            if (!CoopApi.Active) return;
            if (!CoopApi.IsSelfPlayerId(message.TargetPlayerId)) return;   // 不是给我的

            var player = CharacterMainControl.Main;
            if (player == null) return;

            var kind = (Kind)message.Effect;
            var v = new Vector3(message.Fx, message.Fy, message.Fz);

            switch (kind)
            {
                case Kind.Knockback:
                    PlayerEffectActions.Knockback(player, v);
                    break;

                case Kind.PhaseSwap:
                    // 与主机侧同一份效果体；平滑时长取 PhaseSwapBehavior 的常量。
                    player.StartCoroutine(PlayerEffectActions.SmoothMoveTo(
                        player, v + SwapOffset, PhaseSwapBehavior.SwapDurationSeconds));
                    break;

                case Kind.ConsumeBullets:
                    PlayerEffectActions.ConsumeBullets(player, message.Ei);
                    break;

                case Kind.ForceReload:
                    PlayerEffectActions.ForceReload(player);
                    break;

                case Kind.DropWeapon:
                    PlayerEffectActions.DropCurrentWeapon(player);
                    break;

                case Kind.Steal:
                    ExecuteSteal(player, message.Ei);
                    break;

                default:
                    CoopLog.Warn($"[玩家效果] 收到未知的效果种类 {message.Effect}——两端模组版本可能不同");
                    break;
            }
        }

        /// <summary>
        /// 客户端执行偷窃：**从自己的真背包里挑一件拿走**（主机拿不到客机的完整背包，
        /// 所以只能由这边挑），然后回报偷到了什么，好让主机把它加到精英身上。
        /// </summary>
        private static void ExecuteSteal(CharacterMainControl player, int token)
        {
            int stolenTypeId = -1;

            try
            {
                var bag = player.CharacterItem;
                if (bag != null && bag.Inventory != null)
                {
                    // 复用行为类自己的挑选逻辑（**不再写一份**，否则两边标准会漂移）。
                    var stolen = ThiefBehavior.PickStealable(bag.Inventory);
                    if (stolen != null)
                    {
                        stolenTypeId = stolen.TypeID;
                        stolen.Detach();
                    }
                }
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[玩家效果] 客户端执行偷窃失败：{ex.Message}");
            }

            CoopApi.SendToServer(CoopWire.EncodePlayerEffect(
                CoopWire.Kind.PlayerEffectResult, CoopApi.SelfPlayerId ?? string.Empty,
                (byte)Kind.StealResult, 0f, 0f, 0f, token, stolenTypeId));
        }

        /// <summary>把处理函数装进中立钩子（<see cref="PlayerEffectRelay"/>）。</summary>
        public static void Install()
        {
            PlayerEffectRelay.Handler = HandleRelayRequest;
            PlayerEffectRelay.StealHandler = TryRelaySteal;
            PlayerEffectRelay.PlayerPopTextHandler = TryRelayPlayerPopText;
        }

        public static void Shutdown()
        {
            PlayerEffectRelay.Handler = null;
            PlayerEffectRelay.StealHandler = null;
            PlayerEffectRelay.PlayerPopTextHandler = null;

            s_pendingSteals.Clear();
            s_nextStealToken = 1;
        }

        /// <summary>中立钩子的实现：主机侧决定这次效果是转交还是本地做。</summary>
        private static bool HandleRelayRequest(CharacterMainControl victim, Kind kind, Vector3 v, int i)
            => TryRelay(victim, kind, v, i);
    }
}
