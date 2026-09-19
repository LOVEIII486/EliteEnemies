using System;
using System.Collections.Generic;
using EliteEnemies.Combos;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>主机侧广播的一只精英。</summary>
    internal sealed class EliteInfo
    {
        /// <summary>combo id；非 combo 精英为 null。</summary>
        public string ComboId;

        public List<string> Affixes;
    }

    /// <summary>
    /// 精英词条的联机同步。
    ///
    /// <para><b>心智模型</b>：主机是**精英逻辑的唯一权威**——它掷骰、它决定掉落；
    /// 客户端只做两件事：<b>别自己判定</b>（<see cref="EliteEnemyCore.IsEliteAuthority"/> 为 false），
    /// 以及<b>把主机说的结果显示出来</b>。完整依据见
    /// <c>docs\联机兼容可行性分析.md</c>。</para>
    ///
    /// <para><b>顺序无关</b>：词条与 AI 复制体是两条独立到达的异步链，
    /// 谁先到都有可能。所以两边各自只<b>记录</b>，每次记录后都试着<b>配对</b>——
    /// 两边都在了就应用。不假设任何顺序。</para>
    ///
    /// <para><b>客户端应用什么</b>：挂一个带正确 <c>Affixes</c>/combo 的
    /// <see cref="EliteMarker"/>。这一个动作就够让客机"看起来像精英"——血条标签
    /// 与配色都读它（<c>Visuals\EliteHealthBarUI.cs</c>、<c>Patches\HealthBarPatches.cs</c>），
    /// 而且血条 UI **专门为"标记晚挂上"留了 0.5 秒重查窗口**，正好是客户端的情形。</para>
    ///
    /// <para><b>客户端应用不了什么</b>：词条的<b>行为</b>（发光、体型、技能）本轮不跑。
    /// 客户端的复制体是"哑"的（NavMeshAgent 与 AICharacterController 都被禁用），
    /// 让行为在客户端跑会与主机分叉。那需要一个「权威归属」轴，是下一步的事。</para>
    /// </summary>
    internal static class CoopEliteSync
    {
        /// <summary>
        /// 自定义频道名，**带版本后缀**。格式大改时换新频道——
        /// 老客户端不会再收到它解不了的东西（联机模组的频道分发是精确匹配的）。
        /// 与 <see cref="CoopWire.ProtocolVersion"/> 是两道独立的闸门，冗余但便宜。
        /// </summary>
        public const string ChannelName = "eliteenemies:v3/elite";

        /// <summary>向主机请求全量快照的最小间隔——避免每只怪都问一次。</summary>
        private const float QueryCooldown = 2f;

        /// <summary>摘要日志的最小间隔。</summary>
        private const float SummaryInterval = 60f;

        /// <summary>主机侧已知精英表的上限（防御性；正常一局远达不到）。</summary>
        private const int MaxKnownElites = 4096;

        /// <summary>逐条记录复制体 id 的上限——够核对重合度就行，不必全程记。</summary>
        private const int MaxReplicaIdsLogged = 50;

        /// <summary>
        /// 主机逐条记录 **全部** AI 注册的上限（不只精英）。
        /// 用途是诊断"分裂有没有触发"——分裂克隆是新生成的 AI，会在日志里多出两条。
        /// </summary>
        private const int MaxHostRegistrationsLogged = 200;

        // ===== 主机侧 =====
        private static readonly Dictionary<int, EliteInfo> s_hostKnown = new Dictionary<int, EliteInfo>();

        /// <summary>客户端：复制体 id → 角色。**要存角色引用**——应用标记时得拿到它。</summary>
        private static readonly Dictionary<int, CharacterMainControl> s_clientReplicas =
            new Dictionary<int, CharacterMainControl>();

        /// <summary>客户端：词条已到、但复制体还没到的那批（等复制体来了再应用）。</summary>
        private static readonly Dictionary<int, EliteInfo> s_clientPending = new Dictionary<int, EliteInfo>();

        // ===== 统计 =====
        private static int s_recvTotal;
        private static int s_replicaTotal;
        private static int s_affixBeforeReplica;    // 配对：词条先到（复制体后建）
        private static int s_replicaFirst;          // 配对：复制体先到（词条后到）
        private static int s_replicaNoAffixYet;     // 复制体出现时尚无词条——**多数只是非精英**，不是顺序
        private static int s_paired;
        private static int s_applied;
        private static int s_localEliteDivergence;  // 客户端自己掷出精英的次数（应为 0）
        private static int s_replicaIdsLogged;
        private static int s_hostRegLogged;
        private static bool s_zeroIdWarned;

        private static float s_lastQueryTime = -999f;
        private static float s_lastSummaryTime;
        private static bool s_summarySeeded;

        /// <summary>通道就绪后的启动。诊断输出全部走 <see cref="CoopLog"/> 的文件。</summary>
        public static void Initialize()
        {
            CoopDiag.Install();
            CoopStallWatch.Install();
            CoopLog.Announce();

            // ★ 交出/收回「精英逻辑权威」——客户端不再自己判定精英、也不再注入精英掉落。
            //
            // ⚠ **三个条件缺一不可**：只在"联机真的已启动 **且** 本端不是主机"时才交出权威。
            //   少写 `!NetworkStarted` 那一项就会踩到：玩家**装了联机模组却自己单机玩**时
            //   `IsServer` 同样是 false（从没 StartNetwork 过），于是精英会被全部关掉——
            //   不报错、不留日志。这是本工程最忌的那类静默失效。
            //
            //   用委托而不是缓存 bool：身份在会话中会变（联机模组的 StartNetwork/StopNetwork）。
            EliteEnemyCore.EliteAuthorityOverride =
                () => !CoopApi.Active || !CoopApi.NetworkStarted || CoopApi.IsServer;

            CoopLog.Info($"[启动] 联机已启动={CoopApi.NetworkStarted} 本端角色=" +
                         $"{(CoopApi.IsServer ? "主机" : "客户端")}；" +
                         $"精英逻辑权威={EliteEnemyCore.IsEliteAuthority}" +
                         (EliteEnemyCore.IsEliteAuthority ? string.Empty : "（本机将不自行判定精英）"));

            s_lastSummaryTime = Time.unscaledTime;
            s_summarySeeded = true;
        }

        public static void Shutdown()
        {
            LogSummary("停机");

            EliteEnemyCore.EliteAuthorityOverride = null;

            CoopStallWatch.Uninstall();
            CoopDiag.Uninstall();

            s_hostKnown.Clear();
            s_clientReplicas.Clear();
            s_clientPending.Clear();

            // 计数器一并归零：模组停用后重新启用时，统计不该背着上一局的数据。
            s_recvTotal = 0;
            s_replicaTotal = 0;
            s_affixBeforeReplica = 0;
            s_replicaFirst = 0;
            s_replicaNoAffixYet = 0;
            s_paired = 0;
            s_applied = 0;
            s_localEliteDivergence = 0;
            s_replicaIdsLogged = 0;
            s_hostRegLogged = 0;
            s_zeroIdWarned = false;
            s_lastQueryTime = -999f;
        }

        /// <summary>
        /// AI 上线回调——**主机与客户端都会走到这里**，两侧要做的事完全不同。
        ///
        /// <para>时序依据：主机侧由 <c>Patch/Scene/AIPatch.cs:168</c> 触发，
        /// 在 <c>AICharacterController.Init</c> 之后 800ms、且在 <c>Server_RegisterCharacter</c>
        /// 之后，所以 <c>Id</c> 有效、且本模块挂在 <c>Init</c> 上的精英化补丁**必定已经跑完**。
        /// 客户端侧由 <c>AISyncService.cs:3129</c> 在 <c>SpawnReplicaAsync</c> 末尾触发。</para>
        /// </summary>
        public static void OnAiSpawned(int aiId, CharacterMainControl cmc)
        {
            if (!cmc) return;

            if (CoopApi.IsServer) OnHostSawAi(aiId, cmc);
            else OnClientSawReplica(aiId, cmc);
        }

        // ==================== 主机侧 ====================

        private static void OnHostSawAi(int aiId, CharacterMainControl cmc)
        {
            // ⚠ **aiId == 0 是哨兵，不是 id。**
            //   联机模组那侧是这么给的（Patch/Scene/AIPatch.cs:164-167）：
            //       var id = 0;
            //       if (CoopSyncDatabase.AI.TryGet(ai, out var entry) && entry != null) id = entry.Id;
            //       ModApiEvents.RaiseAiSpawned(id, cmc);
            //   即「这只 AI 不在联机同步库里」——最典型的是**基地场景的 NPC**
            //   （联机模组刻意不复制 Base 的刷怪器，两端各有一份自己的本地副本）。
            //
            //   广播它没有任何意义：客户端永远不会有 id=0 的复制体，那条词条只会
            //   永远卡在"待配对"里，还会刷日志。**而且两边是不同实体，本来就同步不了。**
            if (aiId == 0)
            {
                if (!s_zeroIdWarned)
                {
                    s_zeroIdWarned = true;
                    CoopLog.Info("[主机] 遇到 aiId=0（不在联机同步库里的 AI，典型是基地 NPC）——" +
                                 "这类实体两端各有一份本地副本，**无法按 id 关联**，已跳过不再广播");
                }

                return;
            }

            var marker = cmc.GetComponent<EliteMarker>();
            bool isElite = marker != null && marker.Affixes != null && marker.Affixes.Count > 0;

            // 记录**全部** AI 注册（不只精英），上限若干——这是"分裂到底有没有触发"的证据：
            // 分裂克隆是新生成的 AI，会在这里多出两条。数量上限防止刷屏。
            if (s_hostRegLogged < MaxHostRegistrationsLogged)
            {
                s_hostRegLogged++;
                CoopLog.Info($"[主机] AI 注册 aiId={aiId} 精英={isElite}" +
                             (isElite ? $" 词条=[{string.Join(",", marker.Affixes)}]" : string.Empty));
            }

            // 非精英是绝大多数——到此为止，不做别的。
            if (!isElite) return;

            var affixes = marker.Affixes;

            if (s_hostKnown.Count >= MaxKnownElites)
            {
                CoopLog.Warn($"主机已知精英表超过 {MaxKnownElites}，已清空重来（正常情况下不该发生）");
                s_hostKnown.Clear();
            }

            var info = new EliteInfo
            {
                ComboId = marker.ComboId,
                Affixes = new List<string>(affixes)
            };

            s_hostKnown[aiId] = info;

            var payload = CoopWire.EncodeAffix(aiId, info.ComboId, info.Affixes);
            if (CoopApi.Broadcast(payload))
            {
                CoopLog.Info($"[主机] 广播精英 aiId={aiId} " +
                             $"combo={info.ComboId ?? "-"} 词条=[{string.Join(",", info.Affixes)}]");
            }
            else
            {
                // 广播失败通常意味着联机还没起（backend 未装好）。**不静默**。
                CoopLog.Warn($"[主机] 广播失败 aiId={aiId}——联机可能尚未就绪（已记入全量表，客户端可来问）");
            }
        }

        private static void AnswerQuery()
        {
            var ids = new List<int>(s_hostKnown.Count);
            var combos = new List<string>(s_hostKnown.Count);
            var affixes = new List<List<string>>(s_hostKnown.Count);

            foreach (var pair in s_hostKnown)
            {
                ids.Add(pair.Key);
                combos.Add(pair.Value.ComboId);
                affixes.Add(pair.Value.Affixes);
            }

            var payload = CoopWire.EncodeBatch(ids, combos, affixes);
            if (CoopApi.Broadcast(payload))
            {
                CoopLog.Info($"[主机] 已回应全量请求：{ids.Count} 只精英（{payload.Length} 字节）");
            }
            else
            {
                CoopLog.Warn("[主机] 全量回应广播失败——联机可能尚未就绪");
            }
        }

        // ==================== 客户端侧 ====================

        private static void OnClientSawReplica(int aiId, CharacterMainControl cmc)
        {
            s_replicaTotal++;
            s_clientReplicas[aiId] = cmc;

            // 分叉检测：客户端复制体**不该**带 EliteMarker（它走 CreateCharacter，不经过 Init）。
            // 带了就说明本机走了游戏自己的刷怪路径（Base 场景即如此），两端会各自随机。
            // 第 1 期起 IsEliteAuthority 会在客户端为 false，这条应当归零——不归零就是真有漏网。
            if (cmc.GetComponent<EliteMarker>() != null)
            {
                s_localEliteDivergence++;
                CoopLog.Warn($"[客户端] ⚠ 本机自行掷出了精英 aiId={aiId}" +
                             "——「精英逻辑权威」的闸门没挡住，请检查 EliteAuthorityOverride");
            }

            // 词条已经在等着了 ⇒ 这一次是"词条先到"的配对。
            // （原先只在 RecordOnClient 里记 s_paired，于是这条路径配对了也不计数，
            //   摘要里会出现"配对=0 但已应用标记=29"这种自相矛盾的读数。）
            if (s_clientPending.ContainsKey(aiId)) { s_affixBeforeReplica++; s_paired++; }
            else s_replicaNoAffixYet++;

            if (s_replicaIdsLogged < MaxReplicaIdsLogged)
            {
                s_replicaIdsLogged++;
                CoopLog.Info($"[客户端] 复制体就绪 aiId={aiId}");
            }

            TryApply(aiId);
            if (!s_clientPending.ContainsKey(aiId)) RequestFullSnapshot($"复制体 aiId={aiId} 尚无词条");

            MaybeLogSummary();
        }

        private static void RecordOnClient(int aiId, string comboId, List<string> affixes, string via, bool quiet = false)
        {
            s_recvTotal++;

            var info = new EliteInfo { ComboId = comboId, Affixes = affixes };

            if (s_clientReplicas.ContainsKey(aiId))
            {
                s_paired++;
                s_replicaFirst++;
                CoopLog.Info($"[客户端] 配对（**复制体先到**）aiId={aiId} " +
                             $"combo={comboId ?? "-"} 词条=[{string.Join(",", affixes)}]（{via}）");
            }
            else
            {
                s_clientPending[aiId] = info;
                if (!quiet)
                {
                    CoopLog.Info($"[客户端] 收到词条（复制体尚未生成）aiId={aiId} " +
                                 $"combo={comboId ?? "-"} 词条=[{string.Join(",", affixes)}]（{via}）");
                }
            }

            TryApply(aiId);
            MaybeLogSummary();
        }

        /// <summary>
        /// **顺序无关的配对点**：两边都在了就应用。任何一边到达后都调它。
        /// 用的是"检查另一边在不在"，所以不依赖任何到达顺序。
        ///
        /// <para><b>幂等性靠"看目标对象的实际状态"，不靠记 id。</b>
        /// 复制体是**可能被销毁重建**的（换场景、激活门控来回切），
        /// 若按 id 记账，重建出来的新对象就再也挂不上标记——而且是静默的。
        /// 所以这里每次都去查那个角色身上有没有"内容正确的"标记。</para>
        /// </summary>
        private static void TryApply(int aiId)
        {
            if (!s_clientReplicas.TryGetValue(aiId, out var cmc)) return;
            if (!s_clientPending.TryGetValue(aiId, out var info)) return;
            if (!cmc)
            {
                // 复制体已被销毁——清掉引用，等重建后由 OnClientSawReplica 再进来。
                s_clientReplicas.Remove(aiId);
                return;
            }

            try
            {
                var existing = cmc.GetComponent<EliteMarker>();
                if (existing != null && MarkerMatches(existing, info)) return;   // 已经是这个内容了

                ApplyEliteMarker(cmc, info);
                s_applied++;
                CoopLog.Info($"[客户端] 已应用精英标记 aiId={aiId} " +
                             $"combo={info.ComboId ?? "-"} 词条=[{string.Join(",", info.Affixes)}]");
            }
            catch (Exception ex)
            {
                // 应用失败就**说出来**。不记账 ⇒ 下次事件还会再试。
                CoopLog.Warn($"[客户端] 应用精英标记失败 aiId={aiId}: {ex}");
            }
        }

        /// <summary>标记的内容是否已经是目标内容（词条集合与 combo 都一致）。</summary>
        private static bool MarkerMatches(EliteMarker marker, EliteInfo info)
        {
            if (marker.Affixes == null || marker.Affixes.Count != info.Affixes.Count) return false;
            for (int i = 0; i < info.Affixes.Count; i++)
            {
                if (!string.Equals(marker.Affixes[i], info.Affixes[i], StringComparison.Ordinal))
                    return false;
            }

            return string.Equals(marker.ComboId ?? string.Empty,
                                 info.ComboId ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// 客户端把"这只是精英"显示出来——**只挂标记，不跑任何词条行为**。
        ///
        /// <para><see cref="EliteMarker.BaseName"/> 由本机现算（<c>ResolveBaseName</c> 只依赖
        /// 本地预设与本地化），**不需要过网**——传渲染好的字符串会把语言焊死。</para>
        /// </summary>
        private static void ApplyEliteMarker(CharacterMainControl cmc, EliteInfo info)
        {
            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null) marker = cmc.gameObject.AddComponent<EliteMarker>();

            marker.BaseName = EliteEnemyCore.ResolveBaseName(cmc);
            marker.Affixes = new List<string>(info.Affixes);

            if (!string.IsNullOrEmpty(info.ComboId))
            {
                var combo = FindCombo(info.ComboId);
                if (combo != null)
                {
                    marker.SetCombo(combo);
                }
                else
                {
                    // 找不到就**说出来**：这几乎总是"两端词条表不一致"，不是本机的问题。
                    CoopLog.Warn($"[客户端] 找不到 combo '{info.ComboId}'——两端词条表可能不一致，" +
                                 "该精英将只显示词条标签而不显示 combo 称号");
                }
            }
        }

        private static EliteComboDefinition FindCombo(string comboId)
        {
            var pool = EliteComboRegistry.ComboPool;
            for (int i = 0; i < pool.Count; i++)
            {
                var combo = pool[i];
                if (combo != null && string.Equals(combo.ComboId, comboId, StringComparison.Ordinal))
                    return combo;
            }

            return null;
        }

        // ==================== 收包 ====================

        public static void OnNetworkMessage(ReadOnlyMemory<byte> payload, bool isServer)
        {
            if (!CoopWire.TryDecode(payload.Span, out var message, out string failure))
            {
                // 解不了就**说出来**，不要静默丢弃——静默失效正是本工程一路在清的东西。
                CoopLog.Warn($"收到无法解析的报文（{payload.Length} 字节）：{failure}");
                return;
            }

            switch (message.Kind)
            {
                case CoopWire.Kind.Query:
                    if (isServer) AnswerQuery();   // 只有主机能回答
                    break;

                case CoopWire.Kind.Affix:
                    if (!isServer) RecordOnClient(message.AiId, message.ComboId, message.Affixes, "单条");
                    break;

                case CoopWire.Kind.Batch:
                    if (isServer) break;
                    CoopLog.Info($"[客户端] 收到全量快照：{message.BatchIds.Count} 只精英");
                    for (int i = 0; i < message.BatchIds.Count; i++)
                    {
                        RecordOnClient(message.BatchIds[i], message.BatchComboIds[i],
                                       message.BatchAffixes[i], "全量", quiet: true);
                    }
                    break;
            }
        }

        private static void RequestFullSnapshot(string reason)
        {
            if (Time.unscaledTime - s_lastQueryTime < QueryCooldown) return;
            s_lastQueryTime = Time.unscaledTime;

            if (CoopApi.SendToServer(CoopWire.EncodeQuery()))
            {
                CoopLog.Info($"[客户端] 向主机请求全量快照（原因：{reason}）");
            }
            else
            {
                CoopLog.Warn($"[客户端] 全量请求发送失败（原因：{reason}）——联机可能尚未就绪");
            }
        }

        // ==================== 摘要 ====================

        private static void MaybeLogSummary()
        {
            if (!s_summarySeeded) { s_summarySeeded = true; s_lastSummaryTime = Time.unscaledTime; return; }
            if (Time.unscaledTime - s_lastSummaryTime < SummaryInterval) return;

            LogSummary("周期");
        }

        private static void LogSummary(string trigger)
        {
            s_lastSummaryTime = Time.unscaledTime;

            // ⚠ 措辞要准：`复制体出现时无词条` **不等于**"复制体先到"——
            //   绝大多数 AI 本就不是精英，它们永远不会有词条。
            CoopLog.Info($"[摘要·{trigger}] 收到词条={s_recvTotal}（待配对 {s_clientPending.Count}） " +
                         $"见到复制体={s_replicaTotal} 配对={s_paired}" +
                         $"（词条先到 {s_affixBeforeReplica} / 复制体先到 {s_replicaFirst}） " +
                         $"已应用标记={s_applied} " +
                         $"复制体出现时无词条={s_replicaNoAffixYet} " +
                         $"客户端自行掷出精英={s_localEliteDivergence}");
        }
    }
}
