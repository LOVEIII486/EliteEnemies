using System;
using System.Collections.Generic;
using System.Linq;
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

        #region 预设集合

        internal static readonly HashSet<string> EligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> BossPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> MerchantPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> IgnoredGenericPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> ExternalEligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        static EliteEnemyCore()
        {
            BossPresets.UnionWith(NPCPresetNames.Boss.All);
            MerchantPresets.UnionWith(NPCPresetNames.Merchant.All);

            EligiblePresets.UnionWith(NPCPresetNames.Enemies.All);
            EligiblePresets.UnionWith(NPCPresetNames.Animal.All);
            EligiblePresets.UnionWith(NPCPresetNames.Minions.All);
            // 测试炮台是否兼容精英化系统
            EligiblePresets.Add(NPCPresetNames.Special.GunTurret);

            IgnoredGenericPresets.UnionWith(NPCPresetNames.Test.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Unknown.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Special.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Vehicle.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Quest.All);
            // 测试炮台是否兼容精英化系统
            IgnoredGenericPresets.Remove(NPCPresetNames.Special.GunTurret);

            Debug.Log($"{LogTag} 预设白名单初始化完成。普通敌人: {EligiblePresets.Count}, Boss: {BossPresets.Count}, 商人: {MerchantPresets.Count}, 忽略: {IgnoredGenericPresets.Count}");
        }
        
        #endregion
        
        #region 预设白名单与黑名单
        
        internal static readonly Dictionary<string, HashSet<string>> AffixPresetWhitelist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MimicTear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Enemies.Scav,
                    NPCPresetNames.Enemies.ScavElite,
                    NPCPresetNames.Enemies.ScavFarm,
                    NPCPresetNames.Enemies.ScavIce,
                    NPCPresetNames.Enemies.ScavLow,
                    NPCPresetNames.Enemies.ScavLowAK,
                    NPCPresetNames.Enemies.ScavSnow,
                    
                    NPCPresetNames.Enemies.USECFarm,
                    NPCPresetNames.Enemies.USECHidden,
                    NPCPresetNames.Enemies.USECLow,
                    NPCPresetNames.Enemies.USECIce,
                    NPCPresetNames.Enemies.USECIceMilitary,
                    NPCPresetNames.Enemies.USECSnowMilitary,
                    
                    NPCPresetNames.Enemies.Raider,
                    NPCPresetNames.Minions.BALeaderChild,
                    NPCPresetNames.Minions.ThreeShotChild,
                    NPCPresetNames.Minions.SpeedyChild,
                    NPCPresetNames.Minions.Storm1Child,
                    NPCPresetNames.Minions.SpeedyIceChild
                },
                ["MandarinDuck"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Special.GunTurret
                },
                ["Guardian"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Special.GunTurret
                },
                ["Slime"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Special.GunTurret
                }
            };

        internal static readonly Dictionary<string, HashSet<string>> AffixPresetBlacklist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mimic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "EnemyPreset_Boss_Kamakoto_Special"
                },
                ["Explosive"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Enemies.JLabInvisible
                }
            };
        
        public static readonly HashSet<string> UIHiddenPresets = new HashSet<string> 
        { 
            NPCPresetNames.Enemies.JLabInvisible
        };
        
        #endregion

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
        
        public static bool IsUIHidden(string presetName)
        {
            return !string.IsNullOrEmpty(presetName) && UIHiddenPresets.Contains(presetName);
        }

        /// <summary>
        /// 强制将敌人变为精英
        /// </summary>
        public static void ForceMakeElite(CharacterMainControl cmc, IReadOnlyList<string> affixes)
        {
            if (!cmc) return;

            // 1. 计算所有词缀带来的总属性倍率 (hp, dmg, spd 均为最终倍率，如 2.0)
            AccumulateFromAffixes(affixes, out float hp, out float dmg, out float spd);
            
            // 2. 应用基础属性加成
            AttributeModifier.AttributeModifier.Quick.ApplyElitePowerup(cmc, hp, dmg, spd, "EliteBaseStats");

            // 3. 执行精英化标记逻辑
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
            // 1. Combo 系统拦截逻辑
            if (Config.EnableComboSystem && UnityEngine.Random.value < Config.ComboSystemChance)
            {
                string currentPresetName = cmc?.characterPreset != null ? cmc.characterPreset.name : string.Empty;

                var availableCombos = ComboSystem.EliteComboRegistry.ComboPool.FindAll(c => 
                    GameConfig.IsComboEnabled(c.ComboId) && 
                    (c.AllowedPresets.Count == 0 || c.AllowedPresets.Contains(currentPresetName))
                );

                if (availableCombos.Count > 0)
                {
                    float totalWeight = availableCombos.Sum(c => c.Weight);
                    float roll = UnityEngine.Random.Range(0, totalWeight);
                    float currentSum = 0;
                    ComboSystem.EliteComboDefinition selectedCombo = availableCombos[0];

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
                    marker.CustomDisplayName = selectedCombo.GetColoredTitle();

                    //Debug.Log($"{LogTag} [Combo模式] 敌人 {currentPresetName} 匹配成功: {selectedCombo.ComboId}");
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