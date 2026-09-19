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
        public static System.Func<CharacterMainControl, string, string, string, bool> AiPopTextHandler { get; set; }

        /// <summary>
        /// 精英**视觉状态**（体型缩放 / 显隐）的转交。
        /// 起因是这两样都不在联机模组的 <c>AISyncEntry</c> 里，客机因此看不到。
        /// 同样是"主机自己也要应用"。
        /// </summary>
        public static System.Func<CharacterMainControl, Vector3, bool, bool> EliteVisualHandler { get; set; }

        /// <summary>
        /// 在**玩家**头顶弹字的转交。**这一条是"接管"语义**——主机弹在复制体上等于没弹，
        /// 所以联机下只让客机弹。
        /// </summary>
        public static System.Func<CharacterMainControl, string, string, string, bool> PlayerPopTextHandler { get; set; }

        /// <summary>
        /// 在 AI 头顶弹字：**本地弹一份，联机下再让客机各弹一份**。
        ///
        /// <para>用这个助手，不要在行为里直接写 <c>ai.PopText(...)</c>——
        /// 联机模组的通用 <c>PopText</c> 补丁**被它自己注释掉了**
        /// （<c>Patch/Character/AIAwarenessPatch.cs:10-19</c>），
        /// 所以第三方弹的字**不会**过网。</para>
        /// </summary>
        /// <summary>
        /// 在 AI 头顶弹字。**传键，不传译文。**
        ///
        /// <para><b>为什么必须传键</b>：联机下这条要发给客机，而传渲染好的文本
        /// 等于把<b>主机那门语言</b>焊死到客机身上——正是 <c>AGENT.md §3.5</c>
        /// 反复强调的那类问题。传键则两端**各按自己的语言**解析。</para>
        ///
        /// <para>用这个助手，不要在行为里直接写 <c>ai.PopText(...)</c>——
        /// 联机模组的通用 <c>PopText</c> 补丁**被它自己注释掉了**
        /// （<c>Patch/Character/AIAwarenessPatch.cs:10-19</c>），第三方弹的字不会过网。</para>
        ///
        /// <para>含**运行时参数**的弹字（例如"偷到了 X 物品"）用不了这个口——
        /// 那类走 <see cref="PopTextOnEliteResolved"/>，代价见那里的注释。</para>
        /// </summary>
        /// <param name="key">本地化键。</param>
        /// <param name="fallback">键缺失时的兜底文本。**跟键一起发给客机**——
        /// 接收方是通用路径、不知道是哪一条，所以带上最省事（它是代码常量，两端本就相同）。</param>
        /// <param name="arg">格式参数——**只放语言无关的那部分**（数字等）。
        /// 参数里若含本地化文本（物品名、Buff 名），那条只能退到
        /// <see cref="PopTextOnEliteResolved"/>。</param>
        public static void PopTextOnElite(CharacterMainControl ai, string key, string fallback = null,
                                          string arg = null)
        {
            if (ai == null || string.IsNullOrEmpty(key)) return;

            var handler = AiPopTextHandler;
            handler?.Invoke(ai, key, fallback, arg);

            ai.PopText(Format(key, fallback, arg));
        }

        /// <summary>按本地化键取值，有参数时再套一层 format。</summary>
        internal static string Format(string key, string fallback, string arg)
        {
            string text = EliteEnemies.Localization.LocalizationManager.GetText(key, fallback);
            if (string.IsNullOrEmpty(arg)) return text;

            try
            {
                return string.Format(text, arg);
            }
            catch
            {
                // 键里没有 {0} 之类的占位符就会抛——那时原样返回，别把弹字整没了。
                return text;
            }
        }

        /// <summary>
        /// 弹一条**已经拼好的**文本（含运行时参数，无法只传键）。
        ///
        /// <para>⚠️ <b>这是退路，不是常规路径</b>：客机会显示**主机那门语言**的文本。
        /// 只在"文本里拼了运行期才知道的东西、而那样东西又没随报文传过去"时才用。
        /// 能拆成键 + 参数的一律用 <see cref="PopTextOnElite"/>。</para>
        /// </summary>
        public static void PopTextOnEliteResolved(CharacterMainControl ai, string resolvedText)
        {
            if (ai == null || string.IsNullOrEmpty(resolvedText)) return;

            var handler = AiPopTextHandler;
            handler?.Invoke(ai, resolvedText, null, null);   // 兜底为 null ⇒ 对端原样用键

            ai.PopText(resolvedText);
        }

        /// <summary>把精英的视觉状态告知客机（本地应用由调用方自己做）。</summary>
        /// <param name="scale">**绝对缩放**（直接就是 <c>transform.localScale</c> 的值），
        /// 不是倍率——史莱姆是 <c>_originalScale * 倍率</c>，按倍率重建会错。</param>
        public static void RelayEliteVisual(CharacterMainControl ai, Vector3 scale, bool hidden)
        {
            var handler = EliteVisualHandler;
            handler?.Invoke(ai, scale, hidden);
        }

        /// <summary>在玩家头顶弹字。返回 <c>true</c> = 已转交，**不要**再本地弹。</summary>
        public static bool TryRelayPlayerPopText(CharacterMainControl victim, string key, string fallback = null)
        {
            var handler = PlayerPopTextHandler;
            return handler != null && handler(victim, key, fallback, null);
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
