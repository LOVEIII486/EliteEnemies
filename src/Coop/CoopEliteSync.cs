using System;
using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Affixes.Behaviors;
using EliteEnemies.Combos;
using EliteEnemies.Core;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>主机侧广播的一只精英。</summary>
    internal sealed class EliteInfo
    {
        /// <summary>combo id；非 combo 精英为 null。</summary>
        public string ComboId;

        public List<string> Affixes;

        /// <summary>
        /// 这只精英的**最新视觉状态**（体型缩放 + 显隐）。
        ///
        /// <para>⚠️ <b>为什么它属于"条目"、而不只是一条独立报文</b>：视觉状态原先只有
        /// <see cref="CoopWire.Kind.EliteVisual"/> 一条**一次性**消息，于是<b>错过就永远错过</b>
        /// ——中途加入的客机靠全量快照只补得到词条、补不到体型（实测症状正是
        /// 「标签看得到、体型看不到」）。放进条目后，全量快照天然带上当前值。</para>
        /// </summary>
        public EliteVisualState Visual;

        /// <summary>
        /// 这只精英的**最新反射状态**——客机据此在自己那边把射向它的子弹弹开
        /// （判定发生在开枪方，见 <c>ReflectBehavior.IsReflecting</c> 的注释）。
        ///
        /// <para>⚠️ <b>它同样属于"条目"，理由与 <see cref="Visual"/> 一字不差</b>：
        /// 反射是 <c>4.5s</c> 冷却 + <c>3s</c> 持续的**周期**状态，
        /// 而 <c>Kind.EliteReflect</c> 那条只是一次性的变化通知——错过就没了。
        /// 搭在条目上，迟到的客机与"复制体被销毁重建过"的客机都能从全量快照里补到当前值。</para>
        /// </summary>
        public bool Reflecting;

        /// <summary>
        /// 主机侧：这只精英的**角色对象**。
        ///
        /// <para>用途是**反查**——"某只 AI 弹了字 / 改了体型"要广播时，
        /// 手里只有那个角色对象，得把它换成网络上认得的 <c>aiId</c>。
        /// 客户端侧这条为 null（客户端只有复制体，用 <c>s_clientReplicas</c> 就够了）。</para>
        /// </summary>
        public CharacterMainControl Character;
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

        /// <summary>
        /// 客户端：**每只精英的最新视觉状态**（aiId → 缩放 + 显隐）。
        ///
        /// <para>⚠️ <b>它同时承担两件事，而这两件事原先各错了一半</b>：</para>
        /// <list type="number">
        /// <item><b>复制体还没建出来时的暂存</b>——巨大化/迷你是在**精英生成时**设一次体型，
        /// 那一刻客机的复制体往往还没建出来。丢掉的是一条**再也不会重发**的状态。</item>
        /// <item><b>复制体被销毁重建后的重放</b>——联机模组在离开 <c>DeactivationRadius</c> 时
        /// 把复制体**整个销毁**（<c>AISyncService.Client_DestroyReplica</c>），玩家走回来再造一个。
        /// 原先这份暂存在套用后就 <c>Remove</c> 掉了，于是<b>走远再回来体型就没了，
        /// 而标签还在</b>（标记那条路 <see cref="s_clientPending"/> 从不删除）。</item>
        /// </list>
        ///
        /// <para>⇒ <b>它是"最后已知状态"，不是"待办事项"</b>：套用后**不删除**，
        /// 每次复制体出现都重新套一遍。这与 <see cref="s_clientPending"/> 同形，
        /// 也与联机模组文档给的纪律一致（补发只缓存最后一条 ⇒ 按"最新状态覆盖"设计）。</para>
        /// </summary>
        private static readonly Dictionary<int, EliteVisualState> s_clientVisual =
            new Dictionary<int, EliteVisualState>();

        /// <summary>
        /// 客机：**当前正在反射的精英**（aiId 集合）。
        ///
        /// <para>与 <see cref="s_clientVisual"/> 同形、同理：收到后**不消费**，
        /// 因为复制体可能被销毁重建（联机模组在离开 <c>DeactivationRadius</c> 时
        /// 把复制体整个销毁），重建后要能重新套上。</para>
        ///
        /// <para><b>为什么是集合、而不是 <c>Dictionary&lt;int,bool&gt;</c></b>：
        /// 主机永远知道答案（没带这个词条的精英就是"不在反射"），
        /// 所以"没有这条信息"与"不在反射"是同一件事——集合的"在不在里面"
        /// 正好就是那个语义，不必再存一个恒为 false 的值。</para>
        /// </summary>
        private static readonly HashSet<int> s_clientReflecting = new HashSet<int>();

        /// <summary>客机已收到的反射状态条数（摘要里报出来，用于判断"主机发了没/收了没"）。</summary>
        private static int s_clientReflectRecv;

        /// <summary>主机上报过反射状态的次数。</summary>
        private static int s_hostReflectSent;

        /// <summary>视觉状态表的容量上限（防御性；正常一局远达不到）。超出后不再记录新的。</summary>
        private const int MaxPendingVisual = 512;

        /// <summary>
        /// **主机侧**：视觉状态已到、但**这只 AI 还没进同步库**（拿不到 aiId）的那批。
        ///
        /// <para>⚠️ <b>为什么主机也需要暂存</b>：巨大化/迷你在
        /// <c>OnEliteInitialized</c>（= <c>AICharacterController.Init</c> 时）就设好了体型，
        /// 而主机侧的 <c>AiSpawned</c> 比它**晚 800ms**（那个事件本身要等 AI 初始化完）。
        /// 也就是说：**报体型的那一刻，这只 AI 还没有 id**——原先直接丢掉，
        /// 而这些词条**不会重报** ⇒ 客机永远看不到。</para>
        ///
        /// <para>与客机侧不同：主机是**权威**，值就存在角色对象上，所以这里存的是
        /// "还没找到归属的那一份"，等 <see cref="OnHostSawAi"/> 拿到 id 时**并进条目**
        /// （<see cref="EliteInfo.Visual"/>）随词条报文一起发出去，之后就可以丢掉了。</para>
        /// </summary>
        private static readonly Dictionary<CharacterMainControl, EliteVisualState> s_hostPendingVisual =
            new Dictionary<CharacterMainControl, EliteVisualState>();

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

        /// <summary>主机上报过视觉状态的次数（含首次搭在词条报文上的那条）。摘要里报出来。</summary>
        private static int s_hostVisualSent;

        /// <summary>
        /// 因为**状态和上次一样**而被挡下的上报次数（见 <c>OnHostEliteVisual</c> 里那道门）。
        ///
        /// <para>摘要里专门报出来：它是"有没有人在每帧空转上报"的**唯一可见证据**——
        /// 挡下的那批本来就一条日志都不会打。这个数很大就说明又有人写出了
        /// 每帧调 `RelayEliteVisual` 的形状，该去源头拆开而不是靠这道门兜着。</para>
        /// </summary>
        private static int s_hostVisualSkipped;

        /// <summary>其中**打了日志**的条数。逐条记录，但设上限——史莱姆会反复上报。</summary>
        private static int s_hostVisualLogged;

        /// <summary>主机逐条记录「上报反射」的上限。理由同 <see cref="MaxHostVisualLogged"/>：
        /// 反射每 7.5 秒来回一次，不设上限会把日志刷成流水账（总次数仍由
        /// <see cref="s_hostReflectSent"/> 在摘要里报出）。</summary>
        private const int MaxHostReflectLogged = 50;

        private static int s_hostReflectLogged;

        /// <summary>
        /// 主机逐条记录「上报视觉」的上限。
        ///
        /// <para>⚠️ <b>为什么主机侧必须有这行日志</b>：原先<b>发送分支一行输出都没有</b>
        /// （只有"补发"那条会打），于是出问题时<b>分不清「没发」「发了客机没收」「收了没应用」</b>
        /// ——这三者要查的方向完全不同。症状在客机、证据在两端，缺一半就查不动。</para>
        /// </summary>
        private const int MaxHostVisualLogged = 50;

        /// <summary>
        /// 已经修过血量基准的那批精英。
        /// <b>不是统计用，是幂等判据</b>——那个修法连做两次会把加成反向吃掉一倍
        /// （见 <c>StatModifiers.TryUnbakeMaxHealthBonus</c>）。
        /// 与 <see cref="s_hostKnown"/> 同样持有角色引用、同样随停机清空。
        /// </summary>
        private static readonly HashSet<CharacterMainControl> s_healthUnbaked =
            new HashSet<CharacterMainControl>();

        /// <summary>修正过"被算两遍的血量加成"的精英数（摘要里报出来）。</summary>
        private static int s_hostHealthFixed;

        /// <summary>其中打了日志的条数（上限同 <see cref="MaxHostVisualLogged"/> 的理由）。</summary>
        private static int s_hostHealthFixLogged;

        private const int MaxHostHealthFixLogged = 20;

        /// <summary>抓到过"隐身被翻回来"的复制体 id——只用于把那条日志限制成每只一条。</summary>
        private static readonly HashSet<int> s_reassertLogged = new HashSet<int>();

        private const int MaxReassertLogged = 20;

        private static float s_lastQueryTime = -999f;
        private static float s_lastSummaryTime;
        private static bool s_summarySeeded;

        /// <summary>通道就绪后的启动。诊断输出全部走 <see cref="CoopLog"/> 的文件。</summary>
        public static void Initialize()
        {
            CoopDiag.Install();
            CoopStallWatch.Install();

            // 客机的显隐要**持续**压制（主机是每帧压的），见 HideReplica 的注释。
            CoopVisualWatch.Install();

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

            // 这两条是"主机自己也要做"的转交（本地那份由调用方做），**不是接管**。
            // 起因：联机模组把通用的 PopText 补丁**注释掉了**，而体型也不在 AISyncEntry 里。
            PlayerEffectRelay.AiPopTextHandler = OnHostElitePopText;
            PlayerEffectRelay.EliteVisualHandler = OnHostEliteVisual;

            // ★ 反射状态：**两个方向各挂一个**，缺任何一半都会静默失效。
            //
            //   上报（主机）：行为类在 StartReflect/EndReflect 调它 ⇒ 广播给客机。
            //   查询（客机）：弹道补丁在每次子弹命中时问它 ⇒ 客机据此把子弹弹开。
            //
            //   ⚠ **查询这一半在主机上也要挂**：主机侧 s_clientReplicas 是空的
            //   （它只在 !IsServer 时被填充），所以恒返回 false、不干扰
            //   ——主机自己的反射本来就走 ActiveReflectorIDs 那条本地路。
            //   挂上而不是"只在客机挂"，是为了不引入"角色判断写错就静默失效"的第二种写法。
            //
            //   ⚠ 本工程栽过"钩子定义了却从没被赋值"（PlayerPopTextHandler 空转了整整一版，
            //   见 CoopWire v8 的版本说明）⇒ **新增转交口必须同时在这里挂上**。
            EliteStateRelay.ReflectStateHandler = OnHostEliteReflect;
            EliteStateRelay.ReflectQueryHandler = QueryClientReflect;

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
            PlayerEffectRelay.AiPopTextHandler = null;
            PlayerEffectRelay.EliteVisualHandler = null;
            EliteStateRelay.ReflectStateHandler = null;
            EliteStateRelay.ReflectQueryHandler = null;

            CoopVisualWatch.Uninstall();
            CoopStallWatch.Uninstall();
            CoopDiag.Uninstall();

            s_hostKnown.Clear();
            s_clientReplicas.Clear();
            s_clientPending.Clear();
            s_clientVisual.Clear();
            s_clientReflecting.Clear();
            s_hostPendingVisual.Clear();
            s_healthUnbaked.Clear();
            s_reassertLogged.Clear();

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
            s_hostVisualSent = 0;
            s_hostVisualSkipped = 0;
            s_hostVisualLogged = 0;
            s_clientReflectRecv = 0;
            s_hostReflectSent = 0;
            s_hostReflectLogged = 0;
            s_hostHealthFixed = 0;
            s_hostHealthFixLogged = 0;
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

            FixBakedInHealthBonus(aiId, cmc);

            var affixes = marker.Affixes;

            if (s_hostKnown.Count >= MaxKnownElites)
            {
                CoopLog.Warn($"主机已知精英表超过 {MaxKnownElites}，已清空重来（正常情况下不该发生）");
                s_hostKnown.Clear();
            }

            var info = new EliteInfo
            {
                ComboId = marker.ComboId,
                Affixes = new List<string>(affixes),
                Character = cmc
            };

            // 体型可能是"先于 id"报的（巨大化/迷你就是在 `Init` 时设的，见
            // `s_hostPendingVisual`）——并进条目，随词条报文一起发出去。**单独发一条也行**，
            // 但那要多一条报文、还多一个"两条谁先到"的假设，没必要。
            info.Visual = TakeHostPendingVisual(cmc);

            // 反射状态**现问一次当前值**，不走 `s_hostPendingVisual` 那套暂存。
            //
            // 为什么不学它：反射的首次上报**必然"还没有 id"**——`ReflectBehavior.OnEliteInitialized`
            // 把 `_timer` 直接置成 `CooldownTime`，第一次 `OnUpdate` 就满足条件，
            // 于是**第 1 帧**就 StartReflect；而本方法要等 `AiSpawned`（`Init` 之后 800ms）。
            // 那样每只带反射的精英开场都会漏掉一条。
            //
            // 但反射是**状态**、不是事件：这里现问一次就把"当前"补上了，
            // 后面那条 StartReflect 也没白丢（`EndReflect` 3 秒后会收尾）。
            // 为此再建一套并行暂存表，只会多一处会长歪的状态。
            info.Reflecting = ReflectBehavior.IsReflecting(cmc.GetInstanceID());

            s_hostKnown[aiId] = info;

            var payload = CoopWire.EncodeAffix(aiId, info.ComboId, info.Affixes, info.Visual,
                                               info.Reflecting);
            if (CoopApi.Broadcast(payload))
            {
                // 计数只在**真的发出去**之后加：这个读数是用来看"主机有没有发"的，
                // 放进失败分支会让它说谎（见 `MaxHostVisualLogged`）。
                if (info.Visual.Has) s_hostVisualSent++;

                CoopLog.Info($"[主机] 广播精英 aiId={aiId} " +
                             $"combo={info.ComboId ?? "-"} 词条=[{string.Join(",", info.Affixes)}]" +
                             DescribeVisual(info.Visual, prependSpace: true) +
                             (info.Reflecting ? " 反射中" : string.Empty));
            }
            else
            {
                // 广播失败通常意味着联机还没起（backend 未装好）。**不静默**。
                CoopLog.Warn($"[主机] 广播失败 aiId={aiId}——联机可能尚未就绪（已记入全量表，客户端可来问）");
            }
        }

        /// <summary>
        /// 修掉「联机模组把含词条加成的上限当成基准」造成的**加成被算两遍**。
        ///
        /// <para>时序正好：联机模组的难度血量加成在
        /// <c>Patch/Scene/AIPatch.cs:156</c> 的 <c>DifficultyManager.ApplyToAI</c> 里做，
        /// 而 <see cref="OnHostSawAi"/> 挂在同一条链的 <c>:168</c>（800ms 那一步的末尾）——
        /// <b>它之后、且只在这一条链上</b>，所以这里是唯一该动手的时机。</para>
        ///
        /// <para>为什么只在联机出现、单机一直是对的：那个加成要
        /// <c>targetMax &gt; nowMax</c> 才会写基准，而人数倍率
        /// （<c>GetAdditionalPlayerCount</c>）在单人时为 0 ⇒ 系数为 1 ⇒ 不写。
        /// 机制的完整说明见 <see cref="StatModifiers.TryUnbakeMaxHealthBonus"/>。</para>
        /// </summary>
        private static void FixBakedInHealthBonus(int aiId, CharacterMainControl cmc)
        {
            // ⚠ **必须自己保证只修一次**：那个修法不幂等（连修两次会把加成反向吃掉一倍）。
            //   今天 `AiSpawned` 每只 AI 只来一次（游戏侧 `Init` 也只调一次），
            //   但那是别人的不变量——不押注它。
            if (s_healthUnbaked.Contains(cmc)) return;

            try
            {
                if (!StatModifiers.TryUnbakeMaxHealthBonus(cmc, out float before, out float after)) return;

                s_healthUnbaked.Add(cmc);

                s_hostHealthFixed++;
                if (s_hostHealthFixLogged < MaxHostHealthFixLogged)
                {
                    s_hostHealthFixLogged++;
                    CoopLog.Info($"[主机] 修正被算了两遍的血量加成 aiId={aiId} " +
                                 $"基准 {before:0.##} → {after:0.##}" +
                                 "（联机模组把含词条加成的上限当成了基准）");
                }
            }
            catch (Exception ex)
            {
                // 修不了不该拖垮注册/广播这条链——它后面还有词条同步。
                CoopLog.Warn($"[主机] 修正血量加成失败 aiId={aiId}（该精英的血量上限会偏大）: {ex.Message}");
            }
        }

        /// <summary>
        /// 回应全量请求。
        ///
        /// <para>⚠️ <b>必须带上视觉状态</b>（v7）：全量快照是**迟到的客机唯一能补到体型的路**——
        /// 实测的进图流程是"先在基地集合、投票后主机先进关卡、客机约 10 秒后才进"，
        /// 精英在主机进图时就生成了，那一条一次性广播客机根本不在场。</para>
        /// </summary>
        private static void AnswerQuery()
        {
            var ids = new List<int>(s_hostKnown.Count);
            var combos = new List<string>(s_hostKnown.Count);
            var affixes = new List<List<string>>(s_hostKnown.Count);
            var visuals = new List<EliteVisualState>(s_hostKnown.Count);
            var reflects = new List<bool>(s_hostKnown.Count);

            foreach (var pair in s_hostKnown)
            {
                ids.Add(pair.Key);
                combos.Add(pair.Value.ComboId);
                affixes.Add(pair.Value.Affixes);
                visuals.Add(pair.Value.Visual);
                reflects.Add(pair.Value.Reflecting);
            }

            int withVisual = 0;
            int withReflect = 0;
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i].Has) withVisual++;
                if (reflects[i]) withReflect++;
            }

            var payload = CoopWire.EncodeBatch(ids, combos, affixes, visuals, reflects);
            if (CoopApi.Broadcast(payload))
            {
                // 带上"其中几只有视觉状态 / 几只在反射"——
                // 前者决定迟到的客机能否补到体型，后者决定它能否立刻参与反弹判定。
                CoopLog.Info($"[主机] 已回应全量请求：{ids.Count} 只精英" +
                             $"（其中 {withVisual} 只带视觉状态、{withReflect} 只在反射，{payload.Length} 字节）");
            }
            else
            {
                CoopLog.Warn("[主机] 全量回应广播失败——联机可能尚未就绪");
            }
        }

        /// <summary>
        /// 反查：这个角色对应哪个 <c>aiId</c>。找不到返回 0（沿用那个哨兵值）。
        ///
        /// <para>线性查：精英表最多几十条，而弹字/改体型都不是每帧发生的事
        /// （见 <c>MaxKnownElites</c> 的上限）。**刻意不做反向字典**——
        /// 两份映射必然漂移，而这里的规模不值那个风险。</para>
        /// </summary>
        private static int FindHostAiId(CharacterMainControl cmc)
        {
            if (cmc == null) return 0;

            foreach (var pair in s_hostKnown)
            {
                if (pair.Value.Character == cmc) return pair.Key;
            }

            return 0;
        }

        /// <summary>
        /// 主机侧：某只精英弹了一行字。**本机那份由调用方自己弹**（见
        /// <c>PlayerEffectRelay.PopTextOnElite</c>），这里只负责让客机也弹。
        /// </summary>
        public static bool OnHostElitePopText(CharacterMainControl ai, string key, string fallback, string arg)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;
            if (ai == null || string.IsNullOrEmpty(key)) return false;

            int aiId = FindHostAiId(ai);
            if (aiId == 0) return false;   // 不在同步库里（例：基地 NPC）——两端各弹各的即可

            CoopApi.Broadcast(CoopWire.EncodeAiPopText(aiId, key, fallback, arg));
            return true;
        }

        /// <summary>主机侧：某只精英的视觉状态变了（体型缩放 / 显隐）。</summary>
        public static bool OnHostEliteVisual(CharacterMainControl ai, Vector3 scale, bool hidden)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;
            if (ai == null) return false;

            var state = new EliteVisualState { Has = true, Scale = scale, Hidden = hidden };

            int aiId = FindHostAiId(ai);
            if (aiId == 0)
            {
                // 这只 AI 还没进同步库（典型：体型是在 `Init` 时设的，
                // 而 `AiSpawned` 要晚 800ms）。**暂存**，等 `OnHostSawAi` 拿到 id 时并进条目——
                // 那些词条不会重报，丢了就是永久丢。
                if (s_hostPendingVisual.Count < MaxPendingVisual) s_hostPendingVisual[ai] = state;
                return true;
            }

            // ⚠ **同时写回条目**：全量快照（`AnswerQuery`）发的是表里的值，
            // 只广播不更新表的话，迟到的客机补到的就是**过期的体型**。
            if (s_hostKnown.TryGetValue(aiId, out var info))
            {
                // ★ **状态没变就不发。**
                //
                // 这道门是为了堵住一类"调用方每帧都来报一次"的浪费——它真的发生过：
                // `InvisibilityBehavior.OnUpdate` 曾在非闪烁期**每帧**调 `RelayEliteVisual`
                // （触发后约 60 条/秒/只），而 `Hide()`/`Show()` 是边沿触发的，
                // 那种重复调用里**唯一有副作用的只有这次网络广播**。
                // 那一处已在源头拆开（见 `InvisibilityBehavior` 的注释），
                // 这里是**第二道防线**：不论将来谁写出同样的调用形状，都不会变成广播洪水。
                //
                // ⚠ 用 `Vector3` 的 `==`（Unity 的近似比较，带极小 epsilon）——
                // 对"同一个值被反复上报"这个判据正是想要的语义。
                if (info.Visual.Has && info.Visual.Scale == scale && info.Visual.Hidden == hidden)
                {
                    s_hostVisualSkipped++;
                    return true;
                }

                info.Visual = state;
            }

            CoopApi.Broadcast(CoopWire.EncodeEliteVisual(aiId, scale, hidden));
            s_hostVisualSent++;

            if (s_hostVisualLogged < MaxHostVisualLogged)
            {
                s_hostVisualLogged++;
                CoopLog.Info($"[主机] 上报精英视觉 aiId={aiId}{DescribeVisual(state, prependSpace: true)}");
            }

            return true;
        }

        /// <summary>
        /// 主机侧：某只精英的**反射状态变了**。由 <c>ReflectBehavior</c> 在
        /// <c>StartReflect</c> / <c>EndReflect</c> 各调一次。
        ///
        /// <para>本机不需要为它做任何事——主机自己的反射走
        /// <c>ReflectBehavior.ActiveReflectorIDs</c> 那条本地路，这里**只是让客机也判得对**：
        /// 子弹由开枪方本地模拟，客机射出的子弹是在客机上判"该不该弹开"的。</para>
        /// </summary>
        public static bool OnHostEliteReflect(CharacterMainControl ai, bool reflecting)
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return false;
            if (ai == null) return false;

            int aiId = FindHostAiId(ai);

            // 拿不到 id 有两种情形，**都不是错误，也都刻意不暂存**：
            //   · 这只 AI 还没进同步库——开场第一次 StartReflect 必然如此
            //     （它在第 1 帧，而 AiSpawned 在 800ms），那条会被 `OnHostSawAi`
            //     建条目时现问一次补上；
            //   · 它压根不在同步库里（基地 NPC 之类）⇒ 本来就不需要对谁讲。
            //
            // ⚠ 与 `s_hostPendingVisual` 的区别值得记住：体型是**只报一次、丢了就永久丢**
            // 的状态，所以必须暂存；反射是**周期**状态，漏一条下一个周期就自愈。
            // 判据是"漏了会不会自愈"，不是"这条消息重不重要"。
            if (aiId == 0) return false;

            // ⚠ **必须同时写回条目**：全量快照（`AnswerQuery`）发的是表里的值，
            // 只广播不更新表的话，迟到的客机补到的就是**过期的反射状态**。
            if (s_hostKnown.TryGetValue(aiId, out var info)) info.Reflecting = reflecting;

            if (!CoopApi.Broadcast(CoopWire.EncodeEliteReflect(aiId, reflecting)))
            {
                // 广播失败通常意味着联机还没起（backend 未装好）。**不静默**。
                CoopLog.Warn($"[主机] 广播反射状态失败 aiId={aiId}（反射={reflecting}）" +
                             "——联机可能尚未就绪");
                return true;
            }

            s_hostReflectSent++;

            if (s_hostReflectLogged < MaxHostReflectLogged)
            {
                s_hostReflectLogged++;
                CoopLog.Info($"[主机] 上报反射 aiId={aiId} 反射={reflecting}");
            }

            return true;
        }

        /// <summary>
        /// 客机侧：这个**角色实例**此刻在不在反射。
        /// 由弹道补丁在每次子弹命中时经 <c>ReflectBehavior.IsReflecting</c> →
        /// <c>EliteStateRelay.ReflectQueryHandler</c> 调到这里。
        ///
        /// <para><b>用线性反查而不另建反向字典</b>，与主机侧 <see cref="FindHostAiId"/>
        /// 是同一个取舍、理由也一样：精英表只有几十条，而"再维护一份
        /// instanceID ↔ aiId 的映射"必然要在复制体销毁重建时同步，
        /// 漏一处就是"客机偶尔不反弹"这类静默失效。</para>
        ///
        /// <para><b>顺带的好处</b>：正因为它每次都是现查，
        /// "复制体被销毁重建后要重套状态"这件事**根本不存在**——
        /// 集合按 aiId 存（稳定），复制体按 aiId 现查。少一整类 bug。</para>
        ///
        /// <para>主机上 <see cref="s_clientReplicas"/> 是空的（它只在 <c>!IsServer</c> 时填充），
        /// 所以主机恒返回 false——主机自己的反射走本地集合，不受影响。</para>
        /// </summary>
        private static bool QueryClientReflect(int characterInstanceID)
        {
            foreach (var pair in s_clientReplicas)
            {
                var cmc = pair.Value;
                if (!cmc) continue;   // 复制体已被销毁（Unity 的受检空）
                if (cmc.GetInstanceID() != characterInstanceID) continue;

                return s_clientReflecting.Contains(pair.Key);
            }

            return false;
        }

        /// <summary>主机侧：这只 AI 刚拿到 id，把它之前暂存的视觉状态**取走**（并入条目）。</summary>
        private static EliteVisualState TakeHostPendingVisual(CharacterMainControl ai)
        {
            if (!s_hostPendingVisual.TryGetValue(ai, out var state)) return default(EliteVisualState);

            s_hostPendingVisual.Remove(ai);
            CoopLog.Info($"[主机] 精英视觉是在拿到 id 之前报的，已并进词条报文" +
                         $"{DescribeVisual(state, prependSpace: true)}");
            return state;
        }

        /// <summary>把视觉状态拼成日志片段；没有就不产出任何字符。</summary>
        private static string DescribeVisual(EliteVisualState visual, bool prependSpace = false)
        {
            if (!visual.Has) return string.Empty;

            string text = $"缩放={visual.Scale.x:0.00},{visual.Scale.y:0.00},{visual.Scale.z:0.00}" +
                          $" 隐藏={visual.Hidden}";
            return prependSpace ? " " + text : text;
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
            ApplyKnownVisual(aiId, cmc);   // 已知的视觉状态每次都重套一遍（复制体可能刚被重建）
            if (!s_clientPending.ContainsKey(aiId)) RequestFullSnapshot($"复制体 aiId={aiId} 尚无词条");

            MaybeLogSummary();
        }

        private static void RecordOnClient(int aiId, string comboId, List<string> affixes,
                                           EliteVisualState visual, bool reflecting,
                                           string via, bool quiet = false)
        {
            s_recvTotal++;

            // 视觉状态可能与词条同到（v7 起首次状态就搭在条目上），也可能随后才到。
            RememberVisual(aiId, visual);

            // 反射状态（v9）同理：搭在条目上一起来，之后的变化走 Kind.EliteReflect。
            RememberReflect(aiId, reflecting, via);

            var info = new EliteInfo { ComboId = comboId, Affixes = affixes, Visual = visual,
                                       Reflecting = reflecting };

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
            ApplyKnownVisual(aiId);
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

                ApplyEliteMarker(aiId, cmc, info);
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
        private static void ApplyEliteMarker(int aiId, CharacterMainControl cmc, EliteInfo info)
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

            // 客机侧的**视觉预警**随标记一起挂上：报复的青色闪烁、自爆的红色脉冲。
            //
            // 这两条在主机上是词条行为干的，而客机不跑行为 ⇒ 客机玩家看不到任何提示
            // （自爆那条尤其要紧：**零提示就被炸**）。它们都能由客机本地从词条名推出来，
            // 所以不需要任何新报文。挂载幂等（会重复调），且组件跟着复制体的生命周期走。
            CoopEliteWarningVisual.EnsureOn(cmc);

            // 反射状态**按 aiId 存在 s_clientReflecting 里**（不是本地推导——
            // 护盾必须与主机的 3 秒窗口对齐，理由见 CoopEliteWarningVisual 的类注释），
            // 所以组件刚建出来时要把当前值补上。**这一步不能省**：
            // 复制体重建时若正好在反射中，光靠"变化时才推"是补不回来的。
            CoopEliteWarningVisual.SetReflecting(cmc, s_clientReflecting.Contains(aiId));
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

        /// <summary>
        /// 客户端：把主机报来的视觉**变化**收到"最新状态"表里，并立即套用。
        ///
        /// <para>⚠️ 收到后**不区分**复制体在不在——在就套、不在就等
        /// （<see cref="OnClientSawReplica"/> 会补套）。这与原先"没复制体就丢弃/暂存一次"
        /// 的区别在于：状态是**常驻**的，复制体被销毁重建后还能重放。</para>
        /// </summary>
        private static void ApplyEliteVisual(EliteMessage message)
        {
            var state = new EliteVisualState
            {
                Has = true,
                Scale = new Vector3(message.Fx, message.Fy, message.Fz),
                Hidden = message.Ei != 0
            };

            RememberVisual(message.AiId, state);

            if (!s_clientReplicas.TryGetValue(message.AiId, out var cmc) || !cmc)
            {
                CoopLog.Info($"[客户端] 收到精英视觉 aiId={message.AiId}" +
                             $"{DescribeVisual(state, prependSpace: true)}，复制体尚未就绪，已记住待用");
                return;
            }

            ApplyVisualTo(cmc, state.Scale, state.Hidden);
        }

        /// <summary>把一条视觉状态记进"最新状态"表（<c>Has=false</c> 的条目**不覆盖**已知值）。</summary>
        private static void RememberVisual(int aiId, EliteVisualState visual)
        {
            if (!visual.Has) return;

            if (!s_clientVisual.ContainsKey(aiId) && s_clientVisual.Count >= MaxPendingVisual) return;
            s_clientVisual[aiId] = visual;
        }

        /// <summary>
        /// 把一条反射状态记进 <see cref="s_clientReflecting"/>。
        ///
        /// <para>⚠️ <b>这里不需要"套用到复制体"这一步，也不需要"复制体重建后重套"</b>——
        /// 与视觉状态不同：视觉要往 <c>transform</c>/渲染器上写，所以必须有个对象可写、
        /// 且对象重建后要重写一遍；而反射只是<b>一张按 aiId 存的表</b>，
        /// 判定时（<see cref="QueryClientReflect"/>）现查现用。
        /// 少这一层"状态搬运"，就少一整类"重建后忘了重套"的静默失效。</para>
        /// </summary>
        private static void RememberReflect(int aiId, bool reflecting, string via)
        {
            bool changed = reflecting ? s_clientReflecting.Add(aiId) : s_clientReflecting.Remove(aiId);
            if (!changed) return;

            s_clientReflectRecv++;
            CoopLog.Info($"[客户端] 反射状态 aiId={aiId} 反射={reflecting}（{via}）");

            // 顺手把护盾画出来。复制体还没到（或组件还没挂）时什么都不做——
            // 那种情形由 `ApplyEliteMarker` 在挂组件之后补一次当前值，漏不掉。
            if (s_clientReplicas.TryGetValue(aiId, out var cmc) && cmc)
                CoopEliteWarningVisual.SetReflecting(cmc, reflecting);
        }

        /// <summary>客户端：主机报来一条反射状态的**变化**（<c>Kind.EliteReflect</c>）。</summary>
        private static void ApplyEliteReflect(EliteMessage message)
        {
            RememberReflect(message.AiId, message.Reflecting, "变化");
        }

        /// <summary>把已知的视觉状态套到该 aiId 当前的复制体上（没有复制体或没有状态就什么都不做）。</summary>
        private static void ApplyKnownVisual(int aiId)
        {
            if (!s_clientReplicas.TryGetValue(aiId, out var cmc) || !cmc) return;
            ApplyKnownVisual(aiId, cmc);
        }

        private static void ApplyKnownVisual(int aiId, CharacterMainControl cmc)
        {
            if (!s_clientVisual.TryGetValue(aiId, out var state)) return;
            ApplyVisualTo(cmc, state.Scale, state.Hidden);
        }

        /// <summary>
        /// 把视觉状态套到复制体上。
        ///
        /// <para>⚠️ <b>显隐必须走 <c>Hide()/Show()</c>，不能用 <c>gameObject.SetActive</c>。</b>
        /// 后者会**连碰撞体一起关掉**——客机的子弹会直接穿过去、打不中，表现为
        /// "看不见也打不中"（实测确认）；而主机侧用的正是 <c>Hide()/Show()</c>
        /// （只关渲染、保留碰撞）⇒ 两边不一致。隐身该是"**看不见但打得到**"。</para>
        /// </summary>
        private static void ApplyVisualTo(CharacterMainControl cmc, Vector3 scale, bool hidden)
        {
            cmc.transform.localScale = scale;

            if (hidden) HideReplica(cmc);
            else ShowReplica(cmc);

            CoopLog.Info($"[客户端] 应用精英视觉 缩放={scale.x:0.00} 隐藏={hidden}");
        }

        /// <summary>
        /// 把复制体藏起来。**动作与主机侧同一套**：`Hide()` + 直接关模型渲染器。
        ///
        /// <para>⚠️ <b>为什么不能只调一次 <c>Hide()</c>（这正是"拟态在客机一直显形"的根因）</b>：</para>
        /// <list type="number">
        /// <item><c>Hide()</c> 是**边沿触发**的——<c>if (!hidden)</c> 早退（<c>CharacterMainControl.cs:2545</c>），
        /// 状态已经是"藏"时它什么也不做 ⇒ **没法用它再压一次**。所以这里先清标志再调。</item>
        /// <item>而主机侧本来就是**每帧**压制的（<c>MimicBehavior.SetRenderersEnabled</c> 的注释：
        /// "模型可能被别的系统重新启用，所以要持续压制"）。客机只收到**一条**消息，
        /// 被翻回来就再也回不去——实测症状就是拟态在客机一直显形。</item>
        /// <item><c>Hide()</c> 走的是**换层**（<c>CharacterSubVisuals.SetRenderersHidden</c> →
        /// <c>gameObject.layer</c>），只覆盖 <c>subVisuals</c>；主机侧另外还关
        /// <c>characterModel.renderers</c> ⇒ 两边都做，才不会只藏半边。</item>
        /// </list>
        ///
        /// <para>配套：<see cref="CoopVisualWatch"/> 会每 0.5 秒对"仍是隐藏"的那些重压一次
        /// （主机是每帧压的，客机得跟得上）。</para>
        /// </summary>
        private static void HideReplica(CharacterMainControl cmc)
        {
            // ⚠ 这里**故意不调 `Hide()`**——下面这几句与它的实现逐句相同
            //（`CharacterMainControl.cs:2545-2555`），只去掉那个边沿守卫，
            // 并保留 `Teams.player` 那条（游戏不让玩家队伍的角色被藏）。
            if (cmc.Team == Teams.player) return;

            cmc.hidden = true;

            var model = cmc.characterModel;
            if (model != null) model.SyncHiddenToMainCharacter();

            SetModelRenderers(cmc, false);
        }

        /// <summary>取消隐藏：同样**两种机制都还原**（否则被关掉的那批渲染器永远回不来）。</summary>
        private static void ShowReplica(CharacterMainControl cmc)
        {
            SetModelRenderers(cmc, true);
            cmc.Show();
        }

        /// <summary>开关角色模型上的全部渲染器（与主机侧 <c>MimicBehavior</c> 用的是同一份列表）。</summary>
        private static void SetModelRenderers(CharacterMainControl cmc, bool enabled)
        {
            var model = cmc.characterModel;
            if (model == null) return;

            var renderers = model.renderers;
            if (renderers == null) return;

            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer != null && renderer.enabled != enabled) renderer.enabled = enabled;
            }
        }

        /// <summary>
        /// 对"主机说它该藏着"的复制体**再压一次**（见 <see cref="HideReplica"/>）。
        /// 由 <see cref="CoopVisualWatch"/> 每 0.5 秒调一次；没有隐藏中的目标时是零成本。
        /// </summary>
        internal static void ReassertHiddenVisuals()
        {
            if (s_clientVisual.Count == 0 || s_clientReplicas.Count == 0) return;

            foreach (var pair in s_clientVisual)
            {
                if (!pair.Value.Hidden) continue;
                if (!s_clientReplicas.TryGetValue(pair.Key, out var cmc) || !cmc) continue;

                // 先看一眼"它是不是真的又露出来了"——只用于**决定要不要报日志**。
                // 压制本身是无条件的：`Hide()` 换的是 layer、我们关的是 enabled，
                // 两条路哪一条被翻回来都得盖回去（而且这一步很便宜）。
                bool exposed = IsVisuallyExposed(cmc);

                HideReplica(cmc);

                // 只在**第一次**真的抓到"被翻回来"时报一条——它同时是"这套重压确实必要"的证据。
                // 每次报会变成 2Hz 的日志洪水。
                if (exposed && s_reassertLogged.Add(pair.Key) && s_reassertLogged.Count <= MaxReassertLogged)
                {
                    CoopLog.Info($"[客户端] 复制体 aiId={pair.Key} 的隐身被翻回来了，已重新压制" +
                                 "（主机侧是每帧压制的，客机只能按低频率跟）");
                }
            }
        }

        /// <summary>这个复制体的模型**当前是不是露着**（只看渲染器开关，不看 layer）。</summary>
        private static bool IsVisuallyExposed(CharacterMainControl cmc)
        {
            var model = cmc.characterModel;
            if (model == null) return false;

            var renderers = model.renderers;
            if (renderers == null) return false;

            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer != null && renderer.enabled) return true;
            }

            return false;
        }

        /// <summary>
        /// 客户端：在复制体头顶弹字。
        ///
        /// <para>⚠ <b>按本机语言解析那个键</b>——收到的不是译文（见 <c>CoopWire</c> 的 v6 说明）。</summary>
        private static void ApplyAiPopText(EliteMessage message)
        {
            if (!s_clientReplicas.TryGetValue(message.AiId, out var cmc) || !cmc) return;

            // `Text` 是键，`ComboId` 被借来装兜底文本，`TextArg` 是格式参数（都是 string 字段）。
            string key = message.Text;
            if (string.IsNullOrEmpty(key)) return;

            cmc.PopText(PlayerEffectRelay.Format(
                key,
                string.IsNullOrEmpty(message.ComboId) ? null : message.ComboId,
                message.TextArg));
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
                    if (!isServer)
                        RecordOnClient(message.AiId, message.ComboId, message.Affixes,
                                       message.Visual, message.Reflecting, "单条");
                    break;

                case CoopWire.Kind.EliteVisual:
                    if (!isServer) ApplyEliteVisual(message);
                    break;

                case CoopWire.Kind.EliteReflect:
                    if (!isServer) ApplyEliteReflect(message);
                    break;

                case CoopWire.Kind.AiPopText:
                    if (!isServer) ApplyAiPopText(message);
                    break;

                case CoopWire.Kind.PlayerPopText:
                    // 玩家侧的东西归玩家效果那个模块处理（它拿着 id ↔ 玩家的判定）。
                    if (!isServer) CoopPlayerEffect.OnPlayerPopText(message);
                    break;

                case CoopWire.Kind.PlayerEffect:
                    // 主机也会收到自己广播的回环——`OnEffect` 里用"是不是本机玩家 id"过滤，
                    // 主机自己那条会被 `IsSelfPlayerId` 挡掉（它走的是本地执行）。
                    CoopPlayerEffect.OnEffect(message);
                    break;

                case CoopWire.Kind.PlayerEffectResult:
                    if (isServer) CoopPlayerEffect.OnEffectResult(message);
                    break;

                case CoopWire.Kind.Batch:
                    if (isServer) break;
                    CoopLog.Info($"[客户端] 收到全量快照：{message.BatchIds.Count} 只精英");
                    for (int i = 0; i < message.BatchIds.Count; i++)
                    {
                        RecordOnClient(message.BatchIds[i], message.BatchComboIds[i],
                                       message.BatchAffixes[i], message.BatchVisuals[i],
                                       message.BatchReflects[i], "全量", quiet: true);
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
                         $"已记住视觉={s_clientVisual.Count} " +
                         $"反射中的精英={s_clientReflecting.Count}（收到变更 {s_clientReflectRecv}） " +
                         $"复制体出现时无词条={s_replicaNoAffixYet} " +
                         $"客户端自行掷出精英={s_localEliteDivergence} " +
                         $"主机上报视觉={s_hostVisualSent}（挡下重复 {s_hostVisualSkipped}） " +
                         $"主机上报反射={s_hostReflectSent} " +
                         $"主机修正血量加成={s_hostHealthFixed}");
        }
    }
}
