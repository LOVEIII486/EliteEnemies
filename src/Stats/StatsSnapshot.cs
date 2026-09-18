using System.Collections.Generic;

namespace EliteEnemies.Stats
{
    /// <summary>
    /// 一份统计快照——**纯数据，零依赖**：不引用 UnityEngine，也不引用本项目其它命名空间。
    ///
    /// <para><b>为什么把「采集」与「渲染」拆成两半</b>：采集那一侧（<see cref="SessionStats"/>）
    /// 必须读游戏类型（角色、配置、词条表），只能在游戏里跑；而渲染这一侧
    /// （<see cref="StatsReport"/>）一旦只吃纯数据，就能**在游戏外喂假数据**
    /// 把报告打出来，核对版式与边界（0 精英、空来源、超长预设名、除零）。
    /// 本工程没法自动化测试游戏内行为，「至少让输出格式可验证」是能拿到的那部分。</para>
    ///
    /// <para>⚠ 本类型里**刻意不出现</b> <c>AffixRarity</c>——那个枚举所在的文件引用了
    /// <c>UnityEngine.Color</c>，带上它上面那条"零依赖"就不成立了。稀有度改用
    /// <c>int[5]</c> 下标表达，标签表在 <see cref="StatsReport"/> 里。</para>
    /// </summary>
    internal sealed class StatsSnapshot
    {
        // ── 环境 ──

        public string SceneName = "";
        public float DurationSeconds;

        /// <summary>**产生过数据的战斗关卡**数（不含 MainMenu / Base / LoadingScreen，
        /// 也不重复数没有新数据的场景）。见 <c>SessionStats.RefreshRaidFlag</c>。</summary>
        public int SceneCount;

        // ── 配置快照 ──
        //
        // ⚠ 这几项必须打出来：玩家发来的日志里如果看不到他当时的设置，
        //   「为什么没有精英怪」十有八九就是精英率被调成了 0，而你不看见就猜不到。

        public float NormalEliteChance;
        public float BossEliteChance;
        public float MerchantEliteChance;
        public int MaxAffixCount;
        public float DropRateMultiplier;
        public int ItemQualityTier;

        /// <summary>
        /// 玩家**启用的**词条（= 词条池全集减去设置里逐条关掉的那些），已排序。
        ///
        /// <para>为什么要报它：读日志的人得先知道"他开了哪些词条"，
        /// 才判断得了词条直方图正不正常。实测一次排查里，作者只能**反推**
        /// ——"高频词条只有三个 ⇒ 只开了三个"，而那只在有精英生成时推得出来；
        /// 一份"零精英"的日志完全看不出他是不是把词条全禁了。</para>
        ///
        /// <para>⚠ 这是**全局配置**这一层。实际池子还有第二层过滤：
        /// <c>AffixPresetRules.IsAffixAllowedForPreset</c>（**逐敌人预设**不同），
        /// 那层报不出来、也不必报。</para>
        /// </summary>
        public List<string> EnabledAffixes = new List<string>();

        /// <summary>词条池全集大小（<c>EliteAffixes.Pool.Count</c>），用来配「启用 X/Y」。</summary>
        public int AffixPoolTotal;

        // ── 生成 ──

        /// <summary>参与过判定的敌人数（进入概率判定之前的都算）。</summary>
        public int Tried;

        /// <summary>真的变成了精英的数量。</summary>
        public int Elite;

        public Dictionary<SkipReason, int> Skipped = new Dictionary<SkipReason, int>();

        /// <summary>词条条数 → 只数。看 <c>MaxAffixCount</c> 与权重曲线是否生效。</summary>
        public Dictionary<int, int> AffixCountHist = new Dictionary<int, int>();

        /// <summary>词条名 → 出现次数。看词条权重配置是否生效。</summary>
        public Dictionary<string, int> AffixNameHist = new Dictionary<string, int>();

        /// <summary>下标 = 稀有度档位（0 普通 … 4 传说），标签见 <c>StatsReport.RarityLabels</c>。</summary>
        public int[] RarityHist = new int[5];

        public List<PresetStat> Presets = new List<PresetStat>();

        // ── 掉落 ──

        /// <summary>处理过掉落的精英数（≈ 精英击杀数）。</summary>
        public int LootAttempts;

        public int TotalDrops;

        /// <summary>下标 = 品质 1–7（0 不用）。</summary>
        public int[] QualityHist = new int[8];

        public List<SourceStat> Sources = new List<SourceStat>();

        /// <summary>一个精英预设的判定账：进过几次判定、成了几次。</summary>
        internal struct PresetStat
        {
            public string Key;      // 预设技术名（EnemyPreset_…），与日志其它地方可对照
            public string Label;    // 显示名（ResolveBaseName 的结果），可能是空串
            public int Tried;
            public int Elite;
        }

        /// <summary>
        /// 一个掉落来源的账。
        /// <para>来源名的取值域见 <c>EliteLootSystem</c>：<c>词缀固定</c>、
        /// <c>词缀随机(&lt;词条名&gt;)</c>、<c>稀有度奖励</c>、<c>BOSS奖励</c>。</para>
        /// </summary>
        internal struct SourceStat
        {
            public string Name;
            public int Total;
            public int[] Quality;   // 下标 1–7
        }
    }
}
