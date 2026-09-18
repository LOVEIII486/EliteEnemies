using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Core;
using EliteEnemies.Settings;
using UnityEngine;

namespace EliteEnemies.Stats
{
    /// <summary>
    /// 本局统计的**采集**侧：把精英生成与掉落的账记下来，场景卸载时输出一份报告。
    ///
    /// <para><b>它是常开的</b>（不受 <c>DebugSwitch</c> 管辖），因为它的用途不是调试，
    /// 而是**提高玩家出错时那份日志的可分析率**——玩家把 <c>Player.log</c> 发过来，
    /// 能不能从里面看出「精英生成了没有、卡在哪一步、掉了几件」，决定了排查要几轮。
    /// 因此它必须出现在**正式版**里。</para>
    ///
    /// <para><b>「对玩家无感」是硬要求</b>，具体落成三条：</para>
    /// <list type="number">
    /// <item><b>不刷屏</b>：全模块只有一处 <c>Debug.Log</c>，只在**离开战斗关卡**时打
    /// **一条**（整块报告是一个字符串，在控制台里是一个可折叠条目）。
    /// 三道闸见 <see cref="DumpSummary"/>——非战斗场景不打、没数据不打、
    /// 数据一个字没变也不打。</item>
    /// <item><b>不占输入</b>：没有热键、没有 UI、没有 Update。</item>
    /// <item><b>不出错</b>：记录路径上只有字典自增与数组下标（下标已 clamp），
    /// 不碰游戏对象、不发事件——它不可能把一个正在跑的补丁带崩。</item>
    /// </list>
    ///
    /// <para>⚠ <b>刻意不做的事</b>：没有「定期心跳」（怕刷屏），也没有热键
    /// （玩家不该发现它存在）。崩溃/强退时场景不会卸载 ⇒ 那份日志里没有本报告。
    /// 这是**明知并接受**的取舍。</para>
    /// </summary>
    internal static class SessionStats
    {
        internal const string LogTag = "[EliteEnemies.Stats]";

        // ── 生成 ──
        private static readonly Dictionary<SkipReason, int> _skipped = new Dictionary<SkipReason, int>();
        private static readonly Dictionary<int, int> _affixCountHist = new Dictionary<int, int>();
        private static readonly Dictionary<string, int> _affixNameHist =
            new Dictionary<string, int>(System.StringComparer.Ordinal);
        private static readonly int[] _rarityHist = new int[5];
        private static readonly Dictionary<string, StatsSnapshot.PresetStat> _presets =
            new Dictionary<string, StatsSnapshot.PresetStat>(System.StringComparer.Ordinal);

        private static int _tried;
        private static int _elite;
        private static int _notInvolved;

        // ── 掉落 ──
        private static int _lootAttempts;
        private static int _totalDrops;
        private static readonly int[] _qualityHist = new int[8];
        private static readonly Dictionary<string, int[]> _dropsBySource =
            new Dictionary<string, int[]>(System.StringComparer.Ordinal);

        // ── 环境 ──
        private static float _startTime;
        private static string _sceneName = "";
        private static int _sceneCount;

        /// <summary>
        /// **产生这批数据的那个场景是不是战斗关卡**（取自游戏自己的 <c>LevelManager.IsRaidMap</c>）。
        ///
        /// <para>⚠ <b>只在记录数据时刷新</b>（<see cref="RefreshRaidFlag"/>），
        /// 卸载时读的是这个记下的值。两个时机都不能用：</para>
        /// <list type="bullet">
        /// <item><b>不能在卸载时读</b>：<c>LevelConfig</c> 是 MonoBehaviour，它的
        /// <c>Instance</c> 靠 <c>FindObjectOfType</c> 惰性获取（<c>LevelConfig.cs:77-87</c>），
        /// 场景一卸载就没了 ⇒ 只会读到 <c>false</c>，**连战斗关卡的报告都会被一起吞掉**。</item>
        /// <item><b>也不能在加载时读</b>：本作是多场景叠加加载，此刻
        /// <c>FindObjectOfType</c> 拿到的是哪一个 <c>LevelConfig</c> 不确定；而且
        /// <c>isRaidMap</c> 的字段默认值是 <c>true</c>（<c>LevelConfig.cs:17</c>），
        /// 万一某个过渡场景带了一份，就会把 LoadingScreen 误判成战斗关卡。</item>
        /// </list>
        /// <para>记录数据的那一刻玩家必然正在关卡里跑，那时读一定是准的——
        /// 而"数据有没有变"本来就只可能因为记录而发生。</para>
        /// </summary>
        private static bool _sceneIsRaidMap;

        /// <summary>上一个产生过数据的战斗关卡名，用来给 <see cref="_sceneCount"/> 去重。</summary>
        private static string _lastRaidSceneName = "";

        // 上一次输出报告时的计数器。用来压掉"数据一个字没变"的重复报告——
        // 一场 raid 会卸载多个子场景，Base→MainMenu 也各卸载一次，不压的话
        // 实测一次游玩就打了 7 份，其中 4 份与上一份完全相同。
        private static bool _hasDumped;
        private static int _lastDumpTried, _lastDumpElite, _lastDumpLoot, _lastDumpDrops;

        /// <summary>本局开始（模块启用时）。**数据不随场景清零**——调掉率要大样本，
        /// 单场景十几只精英说明不了问题。</summary>
        internal static void BeginSession()
        {
            _skipped.Clear();
            _affixCountHist.Clear();
            _affixNameHist.Clear();
            _presets.Clear();
            System.Array.Clear(_rarityHist, 0, _rarityHist.Length);

            _tried = 0;
            _elite = 0;
            _notInvolved = 0;

            _lootAttempts = 0;
            _totalDrops = 0;
            System.Array.Clear(_qualityHist, 0, _qualityHist.Length);
            _dropsBySource.Clear();

            _startTime = Time.realtimeSinceStartup;
            _sceneName = "";
            _sceneCount = 0;
            _sceneIsRaidMap = false;
            _lastRaidSceneName = "";

            _hasDumped = false;
            _lastDumpTried = _lastDumpElite = _lastDumpLoot = _lastDumpDrops = 0;
        }

        /// <summary>本局结束（模块停用）。</summary>
        internal static void EndSession() => BeginSession();

        internal static void OnSceneLoaded(string sceneName)
        {
            _sceneName = sceneName ?? "";
        }

        /// <summary>
        /// 刷新「当前是不是战斗关卡」。**在每次记录数据时调用**，见 <see cref="RefreshRaidFlag"/>。
        /// </summary>
        private static void RefreshRaidFlag()
        {
            try
            {
                var level = LevelManager.Instance;
                _sceneIsRaidMap = level != null && level.IsRaidMap;
            }
            catch
            {
                _sceneIsRaidMap = false;
            }

            // 「本局 N 个场景」只数**确实产生过数据**的战斗关卡：
            // 这样自动排除了 LoadingScreen / MainMenu / Base，
            // 也不会因为同一关卡的多个子场景各加载一次而虚高
            //（实测一场 5 个战斗场景的游玩被旧实现报成 13）。
            if (_sceneIsRaidMap && !string.Equals(_sceneName, _lastRaidSceneName))
            {
                _lastRaidSceneName = _sceneName;
                _sceneCount++;
            }
        }

        // ────────────────────── 记录：生成 ──────────────────────

        /// <summary>一只敌人走到了判定但没成为精英。</summary>
        internal static void RecordSkipped(string presetName, string displayLabel, SkipReason reason)
        {
            RefreshRaidFlag();
            _tried++;
            Bump(_skipped, reason);
            BumpPreset(presetName, displayLabel, elite: false);
        }

        /// <summary>一只敌人真的成了精英。</summary>
        internal static void RecordElite(string presetName, string displayLabel, List<string> affixes)
        {
            RefreshRaidFlag();
            _tried++;
            _elite++;
            BumpPreset(presetName, displayLabel, elite: true);

            int count = affixes != null ? affixes.Count : 0;
            Bump(_affixCountHist, count);

            if (affixes == null) return;
            for (int i = 0; i < affixes.Count; i++)
            {
                string key = affixes[i];
                if (string.IsNullOrEmpty(key)) continue;

                Bump(_affixNameHist, key);

                // 稀有度分布要看**词条本身**的稀有度（AffixData.Rarity），不是抽取时的权重。
                // 查不到就跳过——不猜、也不记 0，免得把"不认识这个词条"混进分布里。
                AffixData data;
                if (EliteAffixes.Pool.TryGetValue(key, out data))
                {
                    int idx = RarityIndex(data.Rarity);
                    if (idx >= 0) _rarityHist[idx]++;
                }
            }
        }

        /// <summary>一只敌人根本没参与判定（主控 / 友军 / 无预设）。</summary>
        internal static void RecordNotInvolved(SkipReason reason)
        {
            RefreshRaidFlag();
            _notInvolved++;
            Bump(_skipped, reason);
        }

        // ────────────────────── 记录：掉落 ──────────────────────

        /// <summary>开始处理一只精英的掉落（≈ 一次精英击杀）。</summary>
        internal static void RecordLootAttempt()
        {
            RefreshRaidFlag();
            _lootAttempts++;
        }

        /// <summary>一件物品真的进了箱子时调。质量与数量都由调用方给。</summary>
        internal static void RecordDrop(string sourcePool, int quality, int count)
        {
            if (count <= 0) return;

            RefreshRaidFlag();

            // 越界要 clamp 而不是丢弃：丢一件会让「总掉落」与各来源之和对不上账，
            // 而这份报告的全部价值就在于数字能对上。
            int q = quality < 1 ? 1 : quality > 7 ? 7 : quality;

            if (string.IsNullOrEmpty(sourcePool)) sourcePool = "(未命名来源)";

            int[] row;
            if (!_dropsBySource.TryGetValue(sourcePool, out row))
            {
                row = new int[8];
                _dropsBySource[sourcePool] = row;
            }
            row[q] += count;
            _qualityHist[q] += count;   // ⚠ 别漏这句。漏了的话「品质分布」整行是空的，
            _totalDrops += count;       //    而分来源的分布却正常——实测就这么漏了一版。
        }

        // ────────────────────── 输出 ──────────────────────

        /// <summary>
        /// 场景卸载时输出本局累计报告。**全模块唯一的日志出口。**
        ///
        /// <para>三道闸，都为了同一个目标——**别让看日志的人先排除噪音**：</para>
        /// <list type="number">
        /// <item>非战斗关卡不打（MainMenu / Base / LoadingScreen…）。判据是游戏自己的
        /// <c>IsRaidMap</c>，**不硬编码场景名**——名字会变，而这个标志是游戏的语义。</item>
        /// <item>一条数据都没有时不打（沿用原实现的惯例）。</item>
        /// <item>自上次以来**一个数都没变**时不打。同一场 raid 会卸载多个子场景，
        /// 不压的话实测一次游玩打了 7 份，其中 4 份与上一份逐字相同。</item>
        /// </list>
        /// </summary>
        internal static void DumpSummary()
        {
            if (!_sceneIsRaidMap) return;
            if (_tried == 0 && _lootAttempts == 0) return;

            if (_hasDumped
                && _tried == _lastDumpTried
                && _elite == _lastDumpElite
                && _lootAttempts == _lastDumpLoot
                && _totalDrops == _lastDumpDrops)
            {
                return;
            }

            _hasDumped = true;
            _lastDumpTried = _tried;
            _lastDumpElite = _elite;
            _lastDumpLoot = _lootAttempts;
            _lastDumpDrops = _totalDrops;

            Debug.Log(LogTag + " " + StatsReport.Build(BuildSnapshot()));
        }

        private static StatsSnapshot BuildSnapshot()
        {
            CheckTotalsReconcile();

            var s = new StatsSnapshot
            {
                SceneName = _sceneName,
                SceneCount = _sceneCount,
                DurationSeconds = Time.realtimeSinceStartup - _startTime,

                NormalEliteChance = EliteEnemyCore.Config.NormalEliteChance,
                BossEliteChance = EliteEnemyCore.Config.BossEliteChance,
                MerchantEliteChance = EliteEnemyCore.Config.MerchantEliteChance,
                MaxAffixCount = EliteEnemyCore.Config.MaxAffixCount,
                DropRateMultiplier = EliteEnemyCore.Config.DropRateMultiplier,
                // ⚠ 取自 GameConfig 而**不是** EliteEnemyCore.Config：后者只有派生出的
                // `ItemQualityBias`（浮点，如 -1.5），而这里要报的是**设置界面里那个档位旋钮**
                // （0–6 的整数）——玩家发日志时，你要能一眼对上他调的是哪一档。
                ItemQualityTier = GameConfig.ItemQualityTier,
                AffixPoolTotal = EliteAffixes.Pool.Count,
                EnabledAffixes = BuildEnabledAffixes(),

                Tried = _tried,
                Elite = _elite,
                Skipped = new Dictionary<SkipReason, int>(_skipped),
                AffixCountHist = new Dictionary<int, int>(_affixCountHist),
                AffixNameHist = new Dictionary<string, int>(_affixNameHist),
                RarityHist = (int[])_rarityHist.Clone(),
                Presets = new List<StatsSnapshot.PresetStat>(_presets.Values),

                LootAttempts = _lootAttempts,
                TotalDrops = _totalDrops,
                QualityHist = (int[])_qualityHist.Clone(),
                Sources = BuildSources(),
            };
            return s;
        }

        /// <summary>
        /// 输出前自检：三处计数必须对得上账，对不上就把话说明白。
        ///
        /// <para><b>为什么值得加</b>：本模块**第一次实机跑就踩了一个**——
        /// <c>RecordDrop</c> 里漏写了「总分品质直方图」那一句，于是报告里
        /// 「品质分布」整行是空的，而**分来源的分布完全正常**。整个报告没有任何
        /// 异常信号，只有把数字对着看才发现「总数 60 件、直方图全零」。
        /// 这道自检本来会在第一次输出时就点破它。</para>
        ///
        /// <para>做成 warning 而不是抛异常：它跑在玩家机器上，
        /// 记录出点问题不该把游戏带崩（本模块的硬要求是「对玩家无感」）。</para>
        /// </summary>
        private static void CheckTotalsReconcile()
        {
            int qualitySum = 0;
            for (int q = 1; q < _qualityHist.Length; q++) qualitySum += _qualityHist[q];

            int sourceSum = 0;
            foreach (int[] row in _dropsBySource.Values)
            {
                for (int q = 1; q < row.Length; q++) sourceSum += row[q];
            }

            if (qualitySum != _totalDrops || sourceSum != _totalDrops)
            {
                Debug.LogWarning($"{LogTag} 统计自检不通过：总掉落 {_totalDrops} 件，但" +
                                 $"品质直方图合计 {qualitySum}、各来源合计 {sourceSum}。" +
                                 "这是本模块的记录 bug，报告里这几个数字不可信，请反馈。");
            }
        }

        /// <summary>
        /// 玩家启用的词条 = 词条池全集 − 设置里逐条关掉的。
        ///
        /// <para>⚠ 这是**全局配置**这一层。真正抽取时还有第二层过滤
        /// <c>AffixPresetRules.IsAffixAllowedForPreset</c>（逐敌人预设不同），
        /// 那层不在这里报——它取决于打的是谁，报成一个全局名单反而是错的。</para>
        ///
        /// <para>⚠ 也别拿这个名单去核对 Combo：Combo 路径**刻意不过滤
        /// <c>DisabledAffixes</c>**（那是作者确认的"整套"语义，见
        /// <c>AffixSelector.cs:67</c> 的注释），所以被禁用的词条仍可能通过组合出现。</para>
        /// </summary>
        private static List<string> BuildEnabledAffixes()
        {
            var disabled = EliteEnemyCore.Config.DisabledAffixes;
            var enabled = new List<string>(EliteAffixes.Pool.Count);

            foreach (string key in EliteAffixes.Pool.Keys)
            {
                if (disabled != null && disabled.Contains(key)) continue;
                enabled.Add(key);
            }

            // 排序只为让同一份配置每次输出的名单顺序一致——两份日志才好逐行对照
            enabled.Sort(string.CompareOrdinal);
            return enabled;
        }

        private static List<StatsSnapshot.SourceStat> BuildSources()
        {
            var list = new List<StatsSnapshot.SourceStat>(_dropsBySource.Count);
            foreach (var kv in _dropsBySource)
            {
                int total = 0;
                for (int q = 1; q < kv.Value.Length; q++) total += kv.Value[q];

                list.Add(new StatsSnapshot.SourceStat
                {
                    Name = kv.Key,
                    Total = total,
                    Quality = (int[])kv.Value.Clone(),
                });
            }
            return list;
        }

        // ────────────────────── 小工具 ──────────────────────

        private static void Bump(Dictionary<SkipReason, int> map, SkipReason key)
        {
            map.TryGetValue(key, out int c);
            map[key] = c + 1;
        }

        private static void Bump(Dictionary<int, int> map, int key)
        {
            map.TryGetValue(key, out int c);
            map[key] = c + 1;
        }

        private static void Bump(Dictionary<string, int> map, string key)
        {
            map.TryGetValue(key, out int c);
            map[key] = c + 1;
        }

        private static void BumpPreset(string key, string label, bool elite)
        {
            if (string.IsNullOrEmpty(key)) key = "(无预设名)";

            StatsSnapshot.PresetStat st;
            if (!_presets.TryGetValue(key, out st))
            {
                st = new StatsSnapshot.PresetStat { Key = key, Label = label };
            }
            if (string.IsNullOrEmpty(st.Label)) st.Label = label;

            st.Tried++;
            if (elite) st.Elite++;
            _presets[key] = st;
        }

        /// <summary>稀有度 → 报告里的档位下标（0 普通 … 4 传说）。</summary>
        private static int RarityIndex(AffixRarity rarity)
        {
            switch (rarity)
            {
                case AffixRarity.Common: return 0;
                case AffixRarity.Uncommon: return 1;
                case AffixRarity.Rare: return 2;
                case AffixRarity.Epic: return 3;
                case AffixRarity.Legendary: return 4;
                default: return -1;
            }
        }
    }
}
