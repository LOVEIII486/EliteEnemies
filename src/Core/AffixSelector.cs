using System;
using System.Collections.Generic;
using System.Linq;
using EliteEnemies.Affixes;
using EliteEnemies.Combos;
using EliteEnemies.DebugTools;
using EliteEnemies.Settings;
using UnityEngine;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 词条抽取：决定一个精英**带哪几条词条**。
    ///
    /// <para>它是「精英强度曲线」的所在——想调精英带几条词条、各词条的相对权重、
    /// 组合（Combo）的触发概率，改这里。抽取规则有三段，按顺序生效：</para>
    /// <list type="number">
    /// <item><b>Combo 拦截</b>：按配置的概率判定，命中且当前预设匹配时，直接返回该组合的
    /// 词条集，不再走下面的常规抽取</item>
    /// <item><b>常规抽取</b>：从「基础有效词缀池」按权重抽若干条（条数也按权重决定）</item>
    /// <item><b>封弊者突破</b>：若抽中 <c>Obscurer</c>，额外再补 1~4 条（受硬上限约束）</item>
    /// </list>
    ///
    /// <para>从 <see cref="EliteEnemyCore"/> 拆出来的理由：这是**会被反复调参的一块**，
    /// 值得一个能一次读完的文件，而不是埋在核心类的中间。</para>
    /// </summary>
    internal static class AffixSelector
    {
        // 本类从 EliteEnemyCore 拆出后有了自己的日志前缀。原先这些消息打在
        // [EliteEnemies.Core] 下——排查"词条抽得不对"时，能直接按类名定位更有用。
        private const string LogTag = "[EliteEnemies.AffixSelector]";

        public static List<string> SelectRandomAffixes(int maxCount, CharacterMainControl cmc)
        {
            // 1. Combo 系统拦截逻辑
            if (EliteEnemyCore.Config.EnableComboSystem && UnityEngine.Random.value < EliteEnemyCore.Config.ComboSystemChance)
            {
                string currentPresetName = cmc?.characterPreset != null ? cmc.characterPreset.name : string.Empty;

                var availableCombos = EliteComboRegistry.ComboPool.FindAll(c => 
                    GameConfig.IsComboEnabled(c.ComboId) && 
                    (c.AllowedPresets.Count == 0 || c.AllowedPresets.Contains(currentPresetName))
                );

                if (availableCombos.Count > 0)
                {
                    float totalWeight = availableCombos.Sum(c => c.Weight);
                    float roll = UnityEngine.Random.Range(0, totalWeight);
                    float currentSum = 0;
                    EliteComboDefinition selectedCombo = availableCombos[0];

                    foreach (var combo in availableCombos)
                    {
                        currentSum += combo.Weight;
                        if (roll <= currentSum)
                        {
                            selectedCombo = combo;
                            break;
                        }
                    }

                    var marker = cmc.GetComponent<EliteMarker>();
                    if (!marker) marker = cmc.gameObject.AddComponent<EliteMarker>();
                    marker.SetCombo(selectedCombo);

                    //Debug.Log($"{LogTag} [Combo模式] 敌人 {currentPresetName} 匹配成功: {selectedCombo.ComboId}");

                    // ⚠ **刻意不过滤 `Config.DisabledAffixes`（玩家逐条关掉的词条）**——
                    // 作者 2026-09-18 确认：**组合是"整套"语义，只受它自己的开关管**
                    // （`EnableComboSystem` + 该 combo 的 `IsComboEnabled`）。
                    // 玩家看得到组合里含哪些词条——设置面板里那条开关的描述就是
                    // `EliteComboDefinition.GetFormattedDescription()` 拼出来的，会把彩色标签逐个列出来。
                    // ⚠ 这与"绕过互斥规则""绕过预设白/黑名单"是**三件事**，
                    // 后两条见 `docs\词条模块审查与设计.md` §4，本条见
                    // `docs\Combos模块代码审查.md` §4.1——别再问第四遍。
                    return new List<string>(selectedCombo.AffixIds);
                }
            }

            // 2. 原有基础有效词缀池逻辑
            List<string> basePool = GetBaseValidAffixes(cmc);
            if (basePool.Count == 0) return new List<string>();

            var selected = new List<string>();
            if (cmc?.characterPreset != null && cmc.characterPreset.name == "EnemyPreset_Custom_Love486")
            {
                selected.Add("Obscurer");
            }

            var currentAvailable = new List<string>(basePool);
            currentAvailable.RemoveAll(a => selected.Contains(a));

            int targetCount = Mathf.Clamp(SelectWeightedAffixCount(maxCount), 1, currentAvailable.Count);
            SelectAndAppendAffixes(selected, currentAvailable, targetCount);

            // 3. 封弊者突破逻辑 
            const string SpecialAffix = "Obscurer";
            const int SafetyHardLimit = 10;

            if (selected.Contains(SpecialAffix))
            {
                if (selected.Count < SafetyHardLimit)
                {
                    var extraAvailable = new List<string>(basePool);
                    extraAvailable.RemoveAll(a => selected.Contains(a));
                    extraAvailable.RemoveAll(affix => AffixExclusivity.ConflictsWithAny(affix, selected));

                    if (extraAvailable.Count > 0)
                    {
                        // 上界**排他**：Range(1, 5) 才是 1~4，与卖点文案「随机获得 1-4 个词条」一致。
                        // （曾经是 Range(1, 4)，实际只能取到 1~3。）
                        int extraRewardCount = UnityEngine.Random.Range(1, 5);
                        int actualAddCount = Mathf.Min(extraRewardCount, extraAvailable.Count, SafetyHardLimit - selected.Count);

                        if (actualAddCount > 0)
                        {
                            // 生产代码里的诊断打印必须**就地带门控**（AGENT.md §4）。
                            // 这条落在**生成路径**上：正式版里每出现一只封弊者就写一行玩家日志，
                            // 而它对玩家没有任何可操作性（封弊者本就以乱码标签呈现，看不出条数）。
                            if (DebugSwitch.Enabled)
                            {
                                Debug.Log($"{LogTag} [封弊者] 生效！额外添加 {actualAddCount} 个词条。");
                            }

                            SelectAndAppendAffixes(selected, extraAvailable, actualAddCount);
                        }
                    }
                }
            }

            return selected;
        }

        /// <summary>
        /// 从 available 中选择 count 个不冲突的词缀加入 selected
        /// </summary>
        private static void SelectAndAppendAffixes(List<string> selected, List<string> available, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (available.Count == 0) break;

                if (selected.Count > 0)
                {
                    available.RemoveAll(affix => AffixExclusivity.ConflictsWithAny(affix, selected));
                }

                if (available.Count == 0) break;

                string chosen = SelectWeightedRandom(available);
                selected.Add(chosen);
                available.Remove(chosen);
            }
        }

        /// <summary>
        /// 获取基础词缀池
        /// </summary>
        private static List<string> GetBaseValidAffixes(CharacterMainControl cmc)
        {
            var pool = new List<string>(EliteAffixes.Pool.Keys);
            
            string resourceName = cmc?.characterPreset != null ? cmc.characterPreset.name : string.Empty;

            // 1. 过滤预设白名单
            pool.RemoveAll(n => !AffixPresetRules.IsAffixAllowedForPreset(n, resourceName));

            // 2. 过滤用户黑名单
            if (EliteEnemyCore.Config.DisabledAffixes != null && EliteEnemyCore.Config.DisabledAffixes.Count > 0)
            {
                var disabled = new HashSet<string>(EliteEnemyCore.Config.DisabledAffixes, StringComparer.OrdinalIgnoreCase);
                pool.RemoveAll(key => disabled.Contains(key));
            }

            return pool;
        }

        private static string SelectWeightedRandom(List<string> affixNames)
        {
            int totalWeight = 0;
            foreach (string name in affixNames)
            {
                if (EliteAffixes.Pool.TryGetValue(name, out var data))
                {
                    totalWeight += data.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return affixNames[UnityEngine.Random.Range(0, affixNames.Count)];
            }

            int randomValue = UnityEngine.Random.Range(0, totalWeight);
            int currentSum = 0;

            foreach (string name in affixNames)
            {
                if (EliteAffixes.Pool.TryGetValue(name, out var data))
                {
                    currentSum += data.Weight;
                    if (randomValue < currentSum)
                    {
                        return name;
                    }
                }
            }

            return affixNames[affixNames.Count - 1];
        }

        private static int SelectWeightedAffixCount(int maxCount)
        {
            var weights = EliteEnemyCore.Config.AffixCountWeights;

            if (weights == null || weights.Length < 2)
            {
                Debug.LogWarning($"{LogTag} 词条权重配置无效，使用默认均匀分布");
                return UnityEngine.Random.Range(1, maxCount + 1);
            }

            int totalWeight = 0;
            for (int i = 1; i <= maxCount && i < weights.Length; i++)
            {
                totalWeight += Mathf.Max(0, weights[i]);
            }

            if (totalWeight <= 0)
            {
                Debug.LogWarning($"{LogTag} 词条权重总和为0，使用默认均匀分布");
                return UnityEngine.Random.Range(1, maxCount + 1);
            }

            int rand = UnityEngine.Random.Range(0, totalWeight);
            int sum = 0;

            for (int i = 1; i <= maxCount && i < weights.Length; i++)
            {
                sum += Mathf.Max(0, weights[i]);
                if (rand < sum)
                {
                    return i;
                }
            }

            return 1;
        }
    }
}
