using System;
using System.Collections.Generic;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.EliteEnemy.ComboSystem;
using EliteEnemies.Settings;
using SodaCraft.Localizations;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.Core
{
    /// <summary>
    /// 标记组件：挂载此组件的物体将被精英怪系统忽略
    /// </summary>
    public class EliteIgnoredTag : MonoBehaviour
    {
    }

    /// <summary>
    /// 精英敌人核心系统 
    /// </summary>
    public static class EliteEnemyCore
    {
        private const string LogTag = "[EliteEnemies.Core]";

        private static EliteEnemiesConfig _config = new EliteEnemiesConfig();
        public static EliteEnemiesConfig Config => _config;
        
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
        /// 检查对象是否被标记为忽略
        /// </summary>
        public static bool IsIgnored(GameObject target)
        {
            if (target == null) return true;
            return target.GetComponent<EliteIgnoredTag>() != null;
        }

        /// <summary>
        /// 将对象标记为忽略
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

        /// <summary>
        /// 普通敌人预设
        /// </summary>
        internal static readonly HashSet<string> EligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 动物与生物
            "EnemyPreset_Animal_Wolf", "EnemyPreset_Animal_Wolf_Farm", "EnemyPreset_Boss_Deng_Wolf",
            "EnemyPreset_Mushroom", "EnemyPreset_StormCreature", "EnemyPreset_StormCreature_Virus",
            "EnemyPreset_Storm_MonsterClimb", "SpawnPreset_Animal_Jinitaimei",

            // 常规敌人 (Scav/USEC/Raider/Prison)
            "EnemyPreset_Scav", "EnemyPreset_Scav_Elete", "EnemyPreset_Scav_Farm",
            "EnemyPreset_Scav_low", "EnemyPreset_Scav_low_ak74", "EnemyPreset_Scav_Melee",
            "EnemyPreset_USEC_Farm", "EnemyPreset_USEC_HiddenWareHouse", "EnemyPreset_USEC_Low",
            "EnemyPreset_JLab_Raider", "EnemyPreset_Prison_Melee", "EnemyPreset_Prison_Pistol",

            // 机械与特殊
            "EnemyPreset_Spider_Rifle", "EnemyPreset_Spider_Rifle_JLab", "EnemyPreset_Spider_Rifle_Strong",
            "EnemyPreset_Spider_Ring", "EnemyPreset_Spider_RotateShoot", "EnemyPreset_Drone_Rifle",
            "EnemyPreset_Football_1", "EnemyPreset_Football_2", "EnemyPreset_JLab_Melee_Invisable",

            // Boss 随从
            "EnemyPreset_Boss_3Shot_Child", "EnemyPreset_Boss_BALeader_Child", "EnemyPreset_Boss_Fly_Child",
            "EnemyPreset_Boss_Speedy_Child", "EnemyPreset_Boss_Storm_1_Child", "EnemyPreset_Boss_XING_Child",
            "EnemyPreset_BossMelee_SchoolBully_Child"
        };

        /// <summary>
        /// Boss 级预设
        /// </summary>
        internal static readonly HashSet<string> BossPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EnemyPreset_Boss_3Shot", "EnemyPreset_Boss_Arcade", "EnemyPreset_Boss_BALeader",
            "EnemyPreset_Boss_Deng", "EnemyPreset_Boss_Fly", "EnemyPreset_Boss_Grenade",
            "EnemyPreset_Boss_Red", "EnemyPreset_Boss_Roadblock", "EnemyPreset_Boss_RPG",
            "EnemyPreset_Boss_SenorEngineer", "EnemyPreset_Boss_ServerGuardian", "EnemyPreset_Boss_ShortEagle",
            "EnemyPreset_Boss_ShortEagle_Elete", "EnemyPreset_Boss_Shot", "EnemyPreset_Boss_Speedy",
            "EnemyPreset_Boss_Storm_1_BreakArmor", "EnemyPreset_Boss_Storm_2_Poison",
            "EnemyPreset_Boss_Storm_3_Fire", "EnemyPreset_Boss_Storm_4_Electric", "EnemyPreset_Boss_Storm_5_Space",
            "EnemyPreset_Boss_Vida", "EnemyPreset_Boss_XING", "EnemyPreset_BossMelee_SchoolBully",
            "EnemyPreset_Melee_UltraMan", "EnemyPreset_Spider_Scare", "EnemyPreset_Prison_Boss"
        };

        /// <summary>
        /// 商家/NPC 预设
        /// </summary>
        internal static readonly HashSet<string> MerchantPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EnemyPreset_Merchant_Myst", "EnemyPreset_Merchant_Myst0"
        };

        /// <summary>
        /// 强制忽略的通用预设
        /// </summary>
        internal static readonly HashSet<string> IgnoredGenericPresets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // 占位符/测试怪
                "DummyEnemyCharacterRandomPresetLv 0", "DummyEnemyCharacterRandomPresetLv 1",
                "DummyEnemyCharacterRandomPresetLv 2", "DummyEnemyCharacterRandomPresetLv 3",
                "DummyEnemyCharacterRandomPresetLv 4", "DummyEnemyCharacterRandomPresetLv 5",
                "EnemyPreset_Basement", "EnemyPreset_LittleBoss",

                // 队友与宠物
                "MatePreset_PMC", "PetPreset_NormalPet",
                
                // 其他NPC
                "EnemyPreset_Merchant_Test", 
                "EnemyPreset_QuestGiver_Fo", 
                "EnemyPreset_QuestGiver_XiaoMing"
            };

        internal static readonly HashSet<string> ExternalEligiblePresets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static readonly Dictionary<string, HashSet<string>> AffixPresetWhitelist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MimicTear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "EnemyPreset_Scav", 
                    "EnemyPreset_Scav_Elete", 
                    "EnemyPreset_Scav_Farm", 
                    "EnemyPreset_Scav_low", 
                    "EnemyPreset_Scav_low_ak74",
                    "EnemyPreset_USEC_Farm", 
                    "EnemyPreset_USEC_HiddenWareHouse", 
                    "EnemyPreset_USEC_Low",
                    "EnemyPreset_JLab_Raider",
                    "EnemyPreset_Boss_BALeader_Child", 
                    "EnemyPreset_Boss_3Shot_Child", 
                    "EnemyPreset_Boss_Speedy_Child",
                    "EnemyPreset_Boss_Storm_1_Child"
                },
            };

        internal static readonly Dictionary<string, HashSet<string>> AffixPresetBlacklist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mimic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "EnemyPreset_Boss_Kamakoto_Special"
                },
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

        /// <summary>
        /// 检查预设是否满足精英化基础条件
        /// </summary>
        internal static bool IsEligiblePreset(CharacterRandomPreset preset)
        {
            if (preset == null) return false;
            string rName = preset.name;

            // 1. 优先排除强制忽略列表
            if (IgnoredGenericPresets.Contains(rName)) return false;

            // 2. 检查是否在普通敌人或 Boss 列表中
            return EligiblePresets.Contains(rName) || BossPresets.Contains(rName);
        }

        // ========== 词缀选择 ==========

        public static List<string> SelectRandomAffixes(int maxCount, CharacterMainControl cmc)
        {
            //1. Combo 系统拦截
            if (Config.EnableComboSystem && UnityEngine.Random.value < Config.ComboSystemChance)
            {
                string currentPresetName = cmc?.characterPreset?.name ?? string.Empty;
                
                var availableCombos = EliteComboRegistry.ComboPool.FindAll(c => 
                    !Config.DisabledCombos.Contains(c.ComboId) && 
                    (c.AllowedPresets == null || c.AllowedPresets.Count == 0 || c.AllowedPresets.Contains(currentPresetName))
                );

                if (availableCombos.Count > 0)
                {
                    EliteComboDefinition combo = GetRandomFromPool(availableCombos);
                    if (combo != null)
                    {
                        var marker = cmc.GetComponent<EliteMarker>();
                        if (!marker) marker = cmc.gameObject.AddComponent<EliteMarker>();
                        marker.CustomDisplayName = combo.GetColoredTitle(); 
                        return new List<string>(combo.AffixIds);
                    }
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
                    extraAvailable.RemoveAll(affix => EliteAffixes.IsAffixConflictingWithList(affix, selected));

                    if (extraAvailable.Count > 0)
                    {
                        int extraRewardCount = UnityEngine.Random.Range(1, 4);
                        int actualAddCount = Mathf.Min(extraRewardCount, extraAvailable.Count, SafetyHardLimit - selected.Count);

                        if (actualAddCount > 0)
                        {
                            Debug.Log($"{LogTag} [封弊者] 生效！额外添加 {actualAddCount} 个词条。");
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
            
            string resourceName = cmc?.characterPreset != null ? cmc.characterPreset.name : string.Empty;

            // 1. 过滤预设白名单
            pool.RemoveAll(n => !IsAffixAllowedForPreset(n, resourceName));

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
        
        private static EliteComboDefinition GetRandomFromPool(List<EliteComboDefinition> pool)
        {
            float totalWeight = 0;
            foreach (var c in pool) totalWeight += c.Weight;
            float roll = UnityEngine.Random.Range(0, totalWeight);
            float currentSum = 0;
            foreach (var c in pool)
            {
                currentSum += c.Weight;
                if (roll <= currentSum) return c;
            }
            return pool[0];
        }

        private static bool IsAffixAllowedForPreset(string affixName, string resourceName)
        {
            if (string.IsNullOrEmpty(affixName) || string.IsNullOrEmpty(resourceName))
                return false;

            if (AffixPresetBlacklist.TryGetValue(affixName, out var blacklist))
            {
                if (blacklist != null && blacklist.Contains(resourceName))
                    return false;
            }

            if (AffixPresetWhitelist.TryGetValue(affixName, out var whitelist))
                return whitelist == null || whitelist.Count == 0 || whitelist.Contains(resourceName);

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
        
        public static string GetEliteFullDisplayName(CharacterMainControl cmc)
        {
            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null) return ResolveBaseName(cmc);

            // 如果是 Combo 怪，优先显示 CustomDisplayName
            if (!string.IsNullOrEmpty(marker.CustomDisplayName))
            {
                return $"{marker.CustomDisplayName} {marker.BaseName}";
            }

            // 普通精英怪：[词缀标签] 名字
            string prefix = BuildColoredPrefix(marker.Affixes);
            return $"{prefix} {marker.BaseName}";
        }
        

        // ========== 外部预设注册 ==========

        /// <summary>
        /// 尝试自动注册未知的外部敌人预设
        /// </summary>
        internal static bool TryAutoRegisterExternalPreset(CharacterRandomPreset preset)
        {
            if (preset == null) return false;
    
            string rName = preset.name; 
            if (EligiblePresets.Contains(rName) ||
                BossPresets.Contains(rName) ||
                MerchantPresets.Contains(rName) ||
                IgnoredGenericPresets.Contains(rName))
                return false;

            if (!LooksLikeEnemyPreset(rName)) return false;
            if (rName.IndexOf("NonElite", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (!ExternalEligiblePresets.Add(rName)) return true;
            
            EligiblePresets.Add(rName);
            Debug.Log($"{LogTag} 自动发现并注册外部敌人类型: {rName}");
            return true;
        }

        /// <summary>
        /// 判定该资源名是否具有敌人的基本特征
        /// </summary>
        private static bool LooksLikeEnemyPreset(string rName)
        {
            if (string.IsNullOrEmpty(rName)) return false;
            if (rName.Contains("Dummy")) return false;
            if (rName.Contains("MatPreset")) return false;
            if (rName.Contains("PetPreset")) return false;
            if (rName.Contains("Merchant")) return false;
            if (rName.Contains("QuestGiver")) return false;
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