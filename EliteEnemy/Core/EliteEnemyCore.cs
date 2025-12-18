using System;
using System.Collections.Generic;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.Settings;
using SodaCraft.Localizations;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.Core
{
    /// <summary>
    /// 标记组件：挂载此组件的物体将被精英怪系统忽略
    /// </summary>
    public class EliteIgnoredTag : MonoBehaviour { }

    /// <summary>
    /// 精英敌人核心系统 
    /// </summary>
    public static class EliteEnemyCore
    {
        private const string LogTag = "[EliteEnemies.Core]";

        private static EliteEnemiesConfig _config = new EliteEnemiesConfig();
        public static EliteEnemiesConfig Config => _config;

        // [修改] 移除了 NonEliteSuffix

        // ========== 忽略逻辑 (新增) ==========

        // 用于存储由生成器创建的临时预设实例ID，这些实例对应的敌人不应精英化
        // 使用 InstanceID 避免内存泄漏
        private static readonly HashSet<int> IgnoredPresetInstanceIDs = new HashSet<int>();

        /// <summary>
        /// 注册一个预设实例为“忽略精英化”
        /// 用于 EggSpawnHelper 生成的临时预设
        /// </summary>
        public static void RegisterIgnoredPreset(ScriptableObject preset)
        {
            if (preset == null) return;
            IgnoredPresetInstanceIDs.Add(preset.GetInstanceID());
        }

        /// <summary>
        /// 检查预设是否在忽略名单中
        /// </summary>
        public static bool IsIgnoredPreset(ScriptableObject preset)
        {
            if (preset == null) return false;
            return IgnoredPresetInstanceIDs.Contains(preset.GetInstanceID());
        }

        /// <summary>
        /// 检查对象是否被标记为忽略（不生成精英）
        /// </summary>
        public static bool IsIgnored(GameObject target)
        {
            if (target == null) return true;
            return target.GetComponent<EliteIgnoredTag>() != null;
        }

        /// <summary>
        /// 将对象标记为忽略（不会变成精英怪）
        /// </summary>
        public static void MarkAsIgnored(GameObject target)
        {
            if (target == null) return;
            if (target.GetComponent<EliteIgnoredTag>() == null)
            {
                target.AddComponent<EliteIgnoredTag>();
            }
        }

        // ========== 预设集合 ==========

        internal static readonly HashSet<string> EligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cname_Scav", "Cname_ScavRage", "Cname_Wolf", "Cname_Usec", "Cname_DengWolf",
            "Cname_SpeedyChild", "Cname_RobSpider", "Cname_BALeader_Child", "Cname_Boss_Fly_Child",
            "Cname_Football_1", "Cname_Football_2", "Cname_SchoolBully_Child",
            "Cname_StormVirus", "Cname_MonsterClimb", "Cname_Raider", "Cname_LabTestObjective",
            "Cname_StormBoss1_Child", "Cname_Mushroom", "Cname_3Shot_Child", "Cname_Ghost", "Cname_XINGS","Cname_SpeedyChild_Ice"
        };

        // 风暴生物临时移动到 Boss列表
        internal static readonly HashSet<string> BossPresets = new HashSet<string>
        {
            "Cname_UltraMan", "Cname_ShortEagle", "Cname_Boss_Sniper", "Cname_Speedy", "Cname_Vida",
            "Cname_Grenade", "Cname_ServerGuardian", "Cname_Boss_Fly", "Cname_SenorEngineer",
            "Cname_BALeader", "Cname_Boss_Shot", "Cname_Boss_Arcade", "Cname_CrazyRob",
            "Cname_SchoolBully", "Cname_RPG", "Cname_Boss_3Shot", "Cname_Roadblock",
            "Cname_StormBoss1", "Cname_StormBoss2", "Cname_StormBoss3", "Cname_StormBoss4",
            "Cname_StormBoss5", "Cname_Boss_Red", "Cname_StormCreature", "Cname_XING","Cname_Speedy_Ice","Cname_Snow_BigIce"
        };

        internal static readonly HashSet<string> MerchantPresets = new HashSet<string>
        {
            "MerchantName_Myst",
        };

        internal static readonly HashSet<string> ExternalEligiblePresets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static readonly Dictionary<string, HashSet<string>> AffixPresetWhitelist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MimicTear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Cname_Scav", "Cname_Usec", "Cname_Raider", "Cname_BALeader_Child", "Cname_3Shot_Child", "Cname_SpeedyChild"
                },
            };

        internal static readonly Dictionary<string, HashSet<string>> AffixPresetBlacklist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mimic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Enemy_Kamakoto_Special"
                },
            };
        
        private static readonly string[] AutoRegisterBlacklist = new string[]
        {
            "_CM_", // 战斗女仆
        };

        // ========== 公共接口 ==========

        public static void UpdateConfig(EliteEnemiesConfig newConfig)
        {
            if (newConfig == null)
            {
                Debug.LogError($"{LogTag} 配置更新失败: 配置为空");
                return;
            }

            _config = newConfig;
        }

        /// <summary>
        /// 强制将敌人变为精英
        /// </summary>
        public static void ForceMakeElite(CharacterMainControl cmc, IReadOnlyList<string> affixes)
        {
            if (!cmc) return;

            AccumulateFromAffixes(affixes, out float hp, out float dmg, out float spd);
            AttributeModifier.AttributeModifier.Quick.ModifyHealth(cmc, hp, healToFull: true);
            AttributeModifier.AttributeModifier.Quick.ModifyDamage(cmc, dmg);
            AttributeModifier.AttributeModifier.Quick.ModifySpeed(cmc, spd);
            TagAsElite(cmc, new List<string>(affixes), ResolveBaseName(cmc));
        }

        internal static bool IsEligiblePreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return false;
            return EligiblePresets.Contains(presetName);
        }

        // ========== 词缀选择 ==========

        public static List<string> SelectRandomAffixes(int maxCount, CharacterMainControl cmc)
        {
            // 1. 获取基础有效词缀池
            List<string> basePool = GetBaseValidAffixes(cmc);

            if (basePool.Count == 0) return new List<string>();

            // 2. 常规随机选择
            var selected = new List<string>();
            // 额外添加一个彩蛋怪的判断
            if (cmc?.characterPreset?.nameKey == "Enemy_Custom_Love486")
            {
                selected.Add("Obscurer");
            }
            var currentAvailable = new List<string>(basePool); 
            currentAvailable.RemoveAll(a => selected.Contains(a));
            
            int targetCount = Mathf.Clamp(SelectWeightedAffixCount(maxCount), 1, currentAvailable.Count);

            // 执行常规选择
            SelectAndAppendAffixes(selected, currentAvailable, targetCount);
            
            // 3. 封弊者逻辑
            const string SpecialAffix = "Obscurer";
            const int SafetyHardLimit = 10;

            if (selected.Contains(SpecialAffix))
            {
                // 当总数未达到绝对安全熔断值
                if (selected.Count < SafetyHardLimit)
                {
                    // 重新构建可用池：从基础池中移除 已选词条 和 与已选词条冲突的词条
                    var extraAvailable = new List<string>(basePool);
                    extraAvailable.RemoveAll(a => selected.Contains(a));
                    extraAvailable.RemoveAll(affix => EliteAffixes.IsAffixConflictingWithList(affix, selected));

                    if (extraAvailable.Count > 0)
                    {
                        // 设定额外奖励数量：随机 1 到 3 个
                        int extraRewardCount = UnityEngine.Random.Range(1, 4);
                        
                        // 实际可添加数量 = Min(想要奖励的数量, 剩余可用词条数量, 距离熔断值的剩余空间)
                        int actualAddCount = Mathf.Min(extraRewardCount, extraAvailable.Count);
                        actualAddCount = Mathf.Min(actualAddCount, SafetyHardLimit - selected.Count);

                        if (actualAddCount > 0)
                        {
                            Debug.Log($"{LogTag} [封弊者] 生效！突破上限，额外添加 {actualAddCount} 个词条。当前总数: {selected.Count + actualAddCount}");
                            SelectAndAppendAffixes(selected, extraAvailable, actualAddCount);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"{LogTag} [封弊者] 触发，但没有更多可用/不冲突的词条可添加。");
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
                    available.RemoveAll(affix => EliteAffixes.IsAffixConflictingWithList(affix, selected));
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
            string nameKey = cmc?.characterPreset?.nameKey ?? string.Empty;

            // 1. 过滤预设白名单
            pool.RemoveAll(n => !IsAffixAllowedForPreset(n, nameKey));

            // 2. 过滤用户黑名单
            if (_config.DisabledAffixes != null && _config.DisabledAffixes.Count > 0)
            {
                var disabled = new HashSet<string>(_config.DisabledAffixes, StringComparer.OrdinalIgnoreCase);
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
            var weights = Config.AffixCountWeights;

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

        private static bool IsAffixAllowedForPreset(string affixName, string presetName)
        {
            if (string.IsNullOrEmpty(affixName) || string.IsNullOrEmpty(presetName))
                return false;
            
            if (AffixPresetBlacklist.TryGetValue(affixName, out var blacklist))
            {
                // 如果存在黑名单且包含当前预设，则禁止
                if (blacklist != null && blacklist.Contains(presetName))
                    return false;
            }
            
            if (AffixPresetWhitelist.TryGetValue(affixName, out var whitelist))
                return whitelist == null || whitelist.Count == 0 || whitelist.Contains(presetName);

            return true;
        }

        // ========== 倍率计算 ==========

        internal static void AccumulateFromAffixes(IReadOnlyList<string> affixes, out float hpMult, out float dmgMult,
            out float spdMult)
        {
            hpMult = 1f;
            dmgMult = 1f;
            spdMult = 1f;

            if (affixes == null) return;

            foreach (string name in affixes)
            {
                if (EliteAffixes.TryGetAffix(name, out var affix))
                {
                    hpMult *= affix.HealthMultiplier;
                    dmgMult *= affix.DamageMultiplier;
                    spdMult *= affix.MoveSpeedMultiplier;
                }
            }

            hpMult *= Config.GlobalHealthMultiplier;
            dmgMult *= Config.GlobalDamageMultiplier;
            spdMult *= Config.GlobalSpeedMultiplier;
        }

        // ========== 精英标记与命名 ==========

        public static void TagAsElite(CharacterMainControl cmc, List<string> affixes, string baseName)
        {
            if (!cmc) return;

            var marker = cmc.GetComponent<EliteMarker>();
            if (!marker) marker = cmc.gameObject.AddComponent<EliteMarker>();

            marker.BaseName = baseName;
            marker.Affixes = affixes ?? new List<string>();
        }

        internal static string ResolveBaseName(CharacterMainControl cmc)
        {
            var preset = cmc.characterPreset;
            if (preset != null && !string.IsNullOrEmpty(preset.nameKey))
            {
                string localized = preset.nameKey.ToPlainText();
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            string rawName = cmc.name ?? "未知敌人";
            rawName = rawName.Replace("(Clone)", "").Trim();

            if (string.Equals(rawName, "Character", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(rawName))
                rawName = "未知敌人";

            return rawName;
        }

        internal static string BuildColoredPrefix(IReadOnlyList<string> affixes)
        {
            if (affixes == null || affixes.Count == 0) return "*";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string affixKey in affixes)
            {
                sb.Append(EliteAffixes.TryGetAffix(affixKey, out var affix)
                    ? affix.ColoredTag
                    : $"[{affixKey}]");
            }

            return sb.ToString();
        }

        // ========== 外部预设注册 ==========

        internal static bool TryAutoRegisterExternalPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return false;

            if (EligiblePresets.Contains(presetName) ||
                BossPresets.Contains(presetName) ||
                MerchantPresets.Contains(presetName))
                return false;

            if (!LooksLikeEnemyPreset(presetName)) return false;
            
            foreach (var keyword in AutoRegisterBlacklist)
            {
                if (presetName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"{LogTag} 跳过自动注册（匹配黑名单 '{keyword}'）: {presetName}");
                    return false;
                }
            }
            
            if (ExternalEligiblePresets.Contains(presetName)) return true;

            ExternalEligiblePresets.Add(presetName);
            EligiblePresets.Add(presetName);

            Debug.Log($"{LogTag} 自动注册外部敌人类型: {presetName}");
            return true;
        }

        private static bool LooksLikeEnemyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return false;
            if (presetName.StartsWith("MerchantName_", StringComparison.OrdinalIgnoreCase)) return false;
            if (presetName.StartsWith("Player", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        

        // ========== 精英标记组件 ==========

        public class EliteMarker : MonoBehaviour
        {
            public string BaseName;
            public List<string> Affixes = new List<string>();
            public string CustomDisplayName { get; set; }
        }
    }
}