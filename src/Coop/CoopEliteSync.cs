using System;
using System.Collections.Generic;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 精英词条的联机同步——**第 0 期：验证链路与时机，尚不改变任何游戏行为**。
    ///
    /// <para><b>本期的产出是什么</b>：证明「主机侧精英化 → 词条清单到达客户端」这条链是通的，
    /// 并回答两个决定第 1 期写法的问题——</para>
    ///
    /// <list type="number">
    /// <item><b>复制体与词条哪个先到？</b>决定第 1 期要不要在"词条先到"时排队等待。
    /// 两种顺序都要能应付，但代价不同。</item>
    /// <item><b>客户端会不会自己掷出精英？</b>联机模组在 <c>Base</c> 场景
    /// <b>不拦截刷怪器</b>（<c>Patch/Scene/AIPatch.cs:38-39</c>），于是客户端的本地刷怪
    /// 照常走 <c>AICharacterController.Init</c>，本模块的精英化补丁<b>照样触发</b>——
    /// 两端各自随机，结果不同。这是<b>实测已确认</b>的隐性分叉，第 1 期必须堵掉。</item>
    /// </list>
    ///
    /// <para>客户端本期**只记录、不应用**；应用是第 1 期的事。</para>
    /// </summary>
    internal static class CoopEliteSync
    {
        /// <summary>
        /// 自定义频道名，**带版本后缀**。格式大改时换新频道——
        /// 老客户端不会再收到它解不了的东西（联机模组的频道分发是精确匹配的）。
        /// 与 <see cref="CoopWire.ProtocolVersion"/> 是两道独立的闸门，冗余但便宜。
        /// </summary>
        public const string ChannelName = "eliteenemies:v2/elite";

        /// <summary>向主机请求全量快照的最小间隔——避免每只怪都问一次。</summary>
        private const float QueryCooldown = 2f;

        /// <summary>摘要日志的最小间隔。</summary>
        private const float SummaryInterval = 60f;

        /// <summary>主机侧已知精英表的上限（防御性；正常一局远达不到）。</summary>
        private const int MaxKnownElites = 4096;

        /// <summary>逐条记录复制体 id 的上限——够核对重合度就行，不必全程记。</summary>
        private const int MaxReplicaIdsLogged = 50;

        // ===== 主机侧 =====
        private static readonly Dictionary<int, List<string>> s_hostKnown = new Dictionary<int, List<string>>();

        // ===== 客户端侧 =====
        private static readonly Dictionary<int, List<string>> s_clientAffixes = new Dictionary<int, List<string>>();
        private static readonly HashSet<int> s_clientReplicas = new HashSet<int>();

        // ===== 统计（回答上面那两个问题）=====
        private static int s_recvTotal;
        private static int s_replicaTotal;
        private static int s_affixBeforeReplica;    // 配对：词条先到（复制体后建）
        private static int s_replicaFirst;          // 配对：复制体先到（词条后到）★ 第 1 期要处理的那种
        private static int s_replicaNoAffixYet;     // 复制体出现时尚无词条——**多数只是非精英**，不是顺序
        private static int s_paired;
        private static int s_localEliteDivergence;  // 客户端自己掷出精英的次数
        private static int s_replicaIdsLogged;      // 已逐条记录的复制体 id 数

        private static float s_lastQueryTime = -999f;
        private static float s_lastSummaryTime;
        private static bool s_summarySeeded;

        /// <summary>
        /// 通道就绪后的启动。探针的输出全部走 <see cref="CoopLog"/> 的文件，不再进 <c>Player.log</c>。
        /// </summary>
        public static void Initialize()
        {
            CoopDiag.Install();
            CoopStallWatch.Install();
            CoopLog.Announce();

            s_lastSummaryTime = Time.unscaledTime;
            s_summarySeeded = true;
        }

        public static void Shutdown()
        {
            LogSummary("停机");
            CoopStallWatch.Uninstall();
            CoopDiag.Uninstall();

            s_hostKnown.Clear();
            s_clientAffixes.Clear();
            s_clientReplicas.Clear();
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

            if (CoopApi.IsServer)
            {
                OnHostSawAi(aiId, cmc);
                return;
            }

            OnClientSawReplica(aiId, cmc);
        }

        private static void OnHostSawAi(int aiId, CharacterMainControl cmc)
        {
            // 非精英是绝大多数——静默跳过，连日志都不留（否则每只杂兵都写一行）。
            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null) return;

            var affixes = marker.Affixes;
            if (affixes == null || affixes.Count == 0) return;

            if (s_hostKnown.Count >= MaxKnownElites)
            {
                CoopLog.Warn($"主机已知精英表超过 {MaxKnownElites}，已清空重来（正常情况下不该发生）");
                s_hostKnown.Clear();
            }

            s_hostKnown[aiId] = new List<string>(affixes);

            var payload = CoopWire.EncodeAffix(aiId, affixes);
            if (CoopApi.Broadcast(payload))
            {
                CoopLog.Info($"[主机] 广播精英 aiId={aiId} 词条=[{string.Join(",", affixes)}]");
            }
            else
            {
                // 广播失败通常意味着联机还没起（backend 未装好）。**不静默**。
                CoopLog.Warn($"[主机] 广播失败 aiId={aiId}——联机可能尚未就绪（已记入全量表，客户端可来问）");
            }
        }

        private static void OnClientSawReplica(int aiId, CharacterMainControl cmc)
        {
            s_replicaTotal++;
            s_clientReplicas.Add(aiId);

            // ★ 分叉检测：客户端身上有 EliteMarker ⇒ 它自己掷过骰了。
            //   客户端的复制体不该带标记（它走 CreateCharacter，不经过 Init），
            //   所以一旦有标记，就说明本机走了游戏自己的刷怪路径（Base 场景即如此）。
            bool locallyElite = cmc.GetComponent<EliteMarker>() != null;
            if (locallyElite)
            {
                s_localEliteDivergence++;
                CoopLog.Warn($"[客户端] ⚠ 本机自行掷出了精英 aiId={aiId}" +
                             "——Base 场景不拦截刷怪器，两端会各自随机、结果不一致");
            }

            if (s_clientAffixes.TryGetValue(aiId, out var affixes))
            {
                s_paired++;
                s_affixBeforeReplica++;
                CoopLog.Info($"[客户端] 配对成功（**词条先到**）aiId={aiId} " +
                             $"词条=[{string.Join(",", affixes)}]");
            }
            else
            {
                // ⚠ 这一条**不能读作"复制体先到"**：绝大多数 AI 本来就不是精英，
                //   它们永远不会有词条。所以只记"复制体出现在词条之前"这件事本身。
                s_replicaNoAffixYet++;

                // 把复制体 id 记下来（前若干个），好与主机的广播 id 离线核对——
                // 否则"有没有精英复制体"这个问题只能靠猜。id 是 int，几十行不占地方。
                if (s_replicaIdsLogged < MaxReplicaIdsLogged)
                {
                    s_replicaIdsLogged++;
                    CoopLog.Info($"[客户端] 复制体就绪 aiId={aiId}（此刻尚无词条）");
                }
                else if (s_replicaIdsLogged == MaxReplicaIdsLogged)
                {
                    s_replicaIdsLogged++;
                    CoopLog.Info($"[客户端] 复制体 id 已记满 {MaxReplicaIdsLogged} 个，后续不再逐条记录");
                }

                RequestFullSnapshot($"复制体 aiId={aiId} 尚无词条");
            }

            MaybeLogSummary();
        }

        /// <summary>收到本模块频道上的报文。</summary>
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
                    // 只有主机能回答。客户端收到 Query（不该发生）时不理会。
                    if (isServer) AnswerQuery();
                    break;

                case CoopWire.Kind.Affix:
                case CoopWire.Kind.Batch:
                    // 主机收到的 Affix/Batch 是自己广播的回环（Broadcast 的 includeServer 默认 true），
                    // 主机身上没有复制体，无需处理。
                    if (!isServer) ApplyOnClient(message);
                    break;
            }
        }

        // ===== 主机：回答全量请求 =====

        private static void AnswerQuery()
        {
            if (s_hostKnown.Count == 0)
            {
                CoopLog.Info("[主机] 客户端请求全量，但当前没有已知精英，回空表");
            }

            var ids = new List<int>(s_hostKnown.Count);
            var affixes = new List<List<string>>(s_hostKnown.Count);
            foreach (var pair in s_hostKnown)
            {
                ids.Add(pair.Key);
                affixes.Add(pair.Value);
            }

            var payload = CoopWire.EncodeBatch(ids, affixes);
            if (CoopApi.Broadcast(payload))
            {
                CoopLog.Info($"[主机] 已回应全量请求：{ids.Count} 只精英（{payload.Length} 字节）");
            }
            else
            {
                CoopLog.Warn("[主机] 全量回应广播失败——联机可能尚未就绪");
            }
        }

        // ===== 客户端：记录（本期不应用） =====

        private static void ApplyOnClient(EliteMessage message)
        {
            if (message.Kind == CoopWire.Kind.Affix)
            {
                RecordOnClient(message.AiId, message.Affixes, "单条");
                return;
            }

            CoopLog.Info($"[客户端] 收到全量快照：{message.BatchIds.Count} 只精英");
            for (int i = 0; i < message.BatchIds.Count; i++)
                RecordOnClient(message.BatchIds[i], message.BatchAffixes[i], "全量", quiet: true);

            MaybeLogSummary();
        }

        private static void RecordOnClient(int aiId, List<string> affixes, string via, bool quiet = false)
        {
            s_recvTotal++;
            s_clientAffixes[aiId] = affixes;

            if (s_clientReplicas.Contains(aiId))
            {
                s_paired++;
                s_replicaFirst++;
                CoopLog.Info($"[客户端] 配对成功（**复制体先到**，需补应用）aiId={aiId} " +
                             $"词条=[{string.Join(",", affixes)}]（{via}）");
            }
            else if (!quiet)
            {
                CoopLog.Info($"[客户端] 收到词条（复制体尚未生成）aiId={aiId} " +
                             $"词条=[{string.Join(",", affixes)}]（{via}）");
            }

            MaybeLogSummary();
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

        // ===== 摘要 =====

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
            //   真正有意义的只有 `配对`（两边都有）与 `客户端自行掷出精英`（分叉）。
            CoopLog.Info($"[摘要·{trigger}] 收到词条={s_recvTotal}（去重 {s_clientAffixes.Count}） " +
                         $"见到复制体={s_replicaTotal}（去重 {s_clientReplicas.Count}） " +
                         $"配对={s_paired}（词条先到 {s_affixBeforeReplica} / 复制体先到 {s_replicaFirst}） " +
                         $"复制体出现时无词条={s_replicaNoAffixYet} " +
                         $"客户端自行掷出精英={s_localEliteDivergence}");
        }
    }
}
