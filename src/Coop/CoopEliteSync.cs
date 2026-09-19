using System;
using System.Collections.Generic;
using EliteEnemies.Core;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 精英词条的联机同步——**第 0 期：只验证链路，不改变任何游戏行为**。
    ///
    /// <para><b>本期的唯一目标</b>：证明「主机侧精英化 → 词条清单经联机 API 到达客户端」
    /// 这条链是通的，以及主机侧的广播时机与客户端复制体的生成时机能不能对上。
    /// 因此客户端<b>只打日志、不应用</b>——应用是第 1 期的事。</para>
    ///
    /// <para><b>为什么挂在 <c>ModApiEvents.AiSpawned</c> 上</b>：它在<b>两侧都会触发</b>，
    /// 且都带有效的 AI 网络 id：主机侧在 <c>AICharacterController.Init</c> 之后 800ms、
    /// 且在联机模组把该 AI 注册进同步库之后（所以 id 有效）；客户端侧在复制体造好之后。
    /// 主机侧那 800ms 是白送的余量——本模组的精英化补丁挂在同一个 <c>Init</c> 上且<b>不延迟</b>，
    /// 所以事件触发时 <see cref="EliteMarker"/> 必定已经挂好。</para>
    ///
    /// <para>⚠ <b>本文件是第 0 期的探针，验证完要做决策</b>：
    /// 按 <c>AGENT.md §3.8</c>「正式版不留一次性探针」，第 1 期落地时这里的日志与
    /// 「只记录不应用」的分支应当被真正的应用逻辑取代，而不是留着。</para>
    /// </summary>
    internal static class CoopEliteSync
    {
        /// <summary>
        /// 自定义频道名。**带版本后缀**：日后格式或语义大改时换新频道，
        /// 老客户端不会再收到它解不了的东西（联机模组的频道分发是精确匹配的）。
        /// </summary>
        public const string ChannelName = "eliteenemies:v1/affix";

        /// <summary>
        /// AI 上线回调——**主机与客户端都会走到这里**。
        ///
        /// <para>两侧要做的事完全不同：主机侧读本地 <see cref="EliteMarker"/> 并广播；
        /// 客户端侧的复制体没有 <c>EliteMarker</c>（它绕开了 <c>AICharacterController.Init</c>），
        /// 只能等主机广播过来。所以这里第一件事就是分流。</para>
        /// </summary>
        public static void OnAiSpawned(int aiId, CharacterMainControl cmc)
        {
            if (!cmc) return;

            if (!CoopApi.IsServer)
            {
                // 第 0 期只记录，用于验证"复制体就绪"与"词条到达"两个时刻的先后关系。
                CoopLog.Emit($"[客户端] 复制体就绪 aiId={aiId}（等待主机的词条清单）");
                return;
            }

            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null) return;   // 非精英，没什么可广播的——这是绝大多数情况

            var affixes = marker.Affixes;
            if (affixes == null || affixes.Count == 0) return;

            var payload = CoopWire.EncodeEliteAffixes(aiId, affixes);
            if (CoopApi.Broadcast(payload))
            {
                CoopLog.Emit($"[主机] 已广播精英词条 aiId={aiId} " +
                             $"词条=[{string.Join(",", affixes)}]（{payload.Length} 字节）");
            }
            else
            {
                // 广播失败通常意味着联机还没起（backend 未装好）。**不静默**。
                CoopLog.Emit($"[主机] 精英词条广播失败 aiId={aiId}——联机可能尚未就绪。");
            }
        }

        /// <summary>
        /// 收到本模组频道上的报文。
        /// 第 0 期只记录；第 1 期在这里把词条应用到客户端复制体上。
        /// </summary>
        public static void OnNetworkMessage(ReadOnlyMemory<byte> payload, bool isServer)
        {
            if (!CoopWire.TryDecodeEliteAffixes(payload.Span, out int aiId, out List<string> affixes, out string failure))
            {
                // 解不了就**说出来**，不要静默丢弃——静默失效正是本工程一路在清的东西。
                CoopLog.Emit($"收到无法解析的报文（{payload.Length} 字节）：{failure}");
                return;
            }

            string side = isServer ? "主机" : "客户端";
            CoopLog.Emit($"[{side}] 收到精英词条 aiId={aiId} " +
                         $"词条=[{string.Join(",", affixes)}]（第 0 期：仅记录，尚未应用）");
        }
    }
}
