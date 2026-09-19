using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 「作用于玩家」的效果**该由哪台机器执行**的中立出口。
    ///
    /// <para><b>为什么要有这一层</b>：联机下这类效果不能由主机替远端玩家做——
    /// 主机上那个只是复制体，改了到不了真人。所以要把效果转交给受害者那台机器。
    /// 但<b>词条行为不该认识联机模块</b>（本工程 §5.5 的单向依赖纪律：
    /// <c>Coop</c> 可以引用 <c>Affixes</c>，反过来不行）。</para>
    ///
    /// <para>所以这里只留一个"口子"：联机模块在激活时把自己的处理函数装进来，
    /// 行为类只问"这件事有人接管吗"。<b>没装 ⇒ 恒为 false ⇒ 照常本地执行</b>，
    /// 单机路径一字未改。</para>
    ///
    /// <para>同一形状的钩子本工程已有四个：<c>EliteAuthorityOverride</c>、
    /// <c>RemotePlayerPredicate</c>、<c>RemotePlayers</c>、<c>RuntimeDisabledAffixesProvider</c>。</para>
    /// </summary>
    internal static class PlayerEffectRelay
    {
        public enum Kind : byte
        {
            /// <summary><c>v</c> = **已乘过距离倍率**的水平方向。</summary>
            Knockback = 1,

            /// <summary><c>v</c> = 玩家应被送到的位置（精英原先站的地方）。</summary>
            PhaseSwap = 2,

            /// <summary><c>i</c> = 扣几发。</summary>
            ConsumeBullets = 3,

            ForceReload = 4,

            DropWeapon = 5,

            /// <summary>请求：从你背包里挑一件拿走（另有 <see cref="StealHandler"/> 走专门入口）。</summary>
            Steal = 6,

            /// <summary>回报：偷到了什么（见 <c>CoopPlayerEffect.OnEffectResult</c>）。</summary>
            StealResult = 7,

            /// <summary><c>Text</c> = 要弹在**这个玩家**头顶的文本。</summary>
            PopTextOnPlayer = 8,
        }

        /// <summary>
        /// 转交处理器。参数：<c>(受害者, 效果, 参数向量, 整数参数)</c>；
        /// 返回 <c>true</c> = **已被接管**（调用方不要再本地执行）。
        /// <b>默认 <c>null</c> ⇒ 恒不接管 ⇒ 单机行为与从前一字不差。</b>
        /// </summary>
        public static System.Func<CharacterMainControl, Kind, Vector3, int, bool> Handler { get; set; }

        /// <summary>
        /// 偷窃的专门入口。**为什么不并进 <see cref="Handler"/>**：
        /// 偷窃回报时要知道"东西该加给谁"，而**目标精英是调用方持有的、不在受害者身上**，
        /// 所以它多一个参数。
        /// </summary>
        public static System.Func<CharacterMainControl, CharacterMainControl, bool> StealHandler { get; set; }

        /// <summary>
        /// 在 AI 头顶弹字的转交。**与其它效果不同：主机自己也要弹**，
        /// 所以这个口是"两边都做"，返回值没有"接管"的含义。
        /// </summary>
        public static System.Func<CharacterMainControl, string, bool> AiPopTextHandler { get; set; }

        /// <summary>
        /// 精英**视觉状态**（体型缩放 / 显隐）的转交。
        /// 起因是这两样都不在联机模组的 <c>AISyncEntry</c> 里，客机因此看不到。
        /// 同样是"主机自己也要应用"。
        /// </summary>
        public static System.Func<CharacterMainControl, float, bool, bool> EliteVisualHandler { get; set; }

        /// <summary>
        /// 在**玩家**头顶弹字的转交。**这一条是"接管"语义**——主机弹在复制体上等于没弹，
        /// 所以联机下只让客机弹。
        /// </summary>
        public static System.Func<CharacterMainControl, string, bool> PlayerPopTextHandler { get; set; }

        /// <summary>
        /// 在 AI 头顶弹字：**本地弹一份，联机下再让客机各弹一份**。
        ///
        /// <para>用这个助手，不要在行为里直接写 <c>ai.PopText(...)</c>——
        /// 联机模组的通用 <c>PopText</c> 补丁**被它自己注释掉了**
        /// （<c>Patch/Character/AIAwarenessPatch.cs:10-19</c>），
        /// 所以第三方弹的字**不会**过网。</para>
        /// </summary>
        public static void PopTextOnElite(CharacterMainControl ai, string text)
        {
            var handler = AiPopTextHandler;
            handler?.Invoke(ai, text);

            if (ai != null && !string.IsNullOrEmpty(text)) ai.PopText(text);
        }

        /// <summary>把精英的视觉状态告知客机（本地应用由调用方自己做）。</summary>
        public static void RelayEliteVisual(CharacterMainControl ai, float scale, bool hidden)
        {
            var handler = EliteVisualHandler;
            handler?.Invoke(ai, scale, hidden);
        }

        /// <summary>在玩家头顶弹字。返回 <c>true</c> = 已转交，**不要**再本地弹。</summary>
        public static bool TryRelayPlayerPopText(CharacterMainControl victim, string text)
        {
            var handler = PlayerPopTextHandler;
            return handler != null && handler(victim, text);
        }

        /// <summary>把效果转交给受害者那台机器。返回 <c>true</c> = 已接管，**不要**再本地执行。</summary>
        public static bool TryRelay(CharacterMainControl victim, Kind kind, Vector3 v = default(Vector3), int i = 0)
        {
            var handler = Handler;
            return handler != null && handler(victim, kind, v, i);
        }

        /// <summary>转交一次偷窃（<paramref name="thief"/> = 要把战利品加给谁）。</summary>
        public static bool TryRelaySteal(CharacterMainControl victim, CharacterMainControl thief)
        {
            var handler = StealHandler;
            return handler != null && handler(victim, thief);
        }
    }
}
