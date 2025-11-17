using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Scenes;
using SodaCraft.Localizations;

namespace EliteEnemies
{
    /// <summary>
    /// 精英敌人核心系统 
    /// </summary>
    public static class EliteEnemyCore
    {
        private const string LogTag = "[EliteEnemies.Core]";

        private static EliteEnemiesConfig _config = new EliteEnemiesConfig();
        public static EliteEnemiesConfig Config => _config;

        // ========== 预设集合 ==========

        internal static readonly HashSet<string> EligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cname_Scav", "Cname_ScavRage", "Cname_Wolf", "Cname_Usec", "Cname_DengWolf",
            "Cname_SpeedyChild", "Cname_RobSpider", "Cname_BALeader_Child", "Cname_Boss_Fly_Child",
            "Cname_Football_1", "Cname_Football_2", "Cname_SchoolBully_Child", "Cname_StormCreature",
            "Cname_StormVirus", "Cname_MonsterClimb", "Cname_Raider", "Cname_LabTestObjective",
            "Cname_StormBoss1_Child", "Cname_Mushroom", "Cname_3Shot_Child"
        };

        internal static readonly HashSet<string> BossPresets = new HashSet<string>
        {
            "Cname_UltraMan", "Cname_ShortEagle", "Cname_Boss_Sniper", "Cname_Speedy", "Cname_Vida",
            "Cname_Grenade", "Cname_ServerGuardian", "Cname_Boss_Fly", "Cname_SenorEngineer",
            "Cname_BALeader", "Cname_Boss_Shot", "Cname_Boss_Arcade", "Cname_CrazyRob",
            "Cname_SchoolBully", "Cname_RPG", "Cname_Boss_3Shot", "Cname_Roadblock",
            "Cname_StormBoss1", "Cname_StormBoss2", "Cname_StormBoss3", "Cname_StormBoss4",
            "Cname_StormBoss5", "Cname_Boss_Red"
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
                    "Cname_Scav", "Cname_Usec", "Cname_Raider",
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
            AttributeModifier.ApplyEliteMultipliers(
                cmc,
                healthMult: hp,
                damageMult: dmg,
                speedMult: spd,
                healToFull: true
            );
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
            var available = new List<string>(EliteAffixes.Pool.Keys);
            string nameKey = cmc?.characterPreset?.nameKey ?? string.Empty;

            // 过滤：预设白名单
            available.RemoveAll(n => !IsAffixAllowedForPreset(n, nameKey));

            // 过滤：用户黑名单
            if (_config.DisabledAffixes != null && _config.DisabledAffixes.Count > 0)
            {
                var disabled = new HashSet<string>(_config.DisabledAffixes, StringComparer.OrdinalIgnoreCase);
                available.RemoveAll(key => disabled.Contains(key));
            }

            if (available.Count == 0) return new List<string>();

            // 基于权重随机选择
            var selected = new List<string>();
            int count = Mathf.Clamp(SelectWeightedAffixCount(maxCount), 1, available.Count);

            for (int i = 0; i < count; i++)
            {
                if (available.Count == 0) break;

                // 过滤：互斥词缀
                if (selected.Count > 0)
                {
                    available.RemoveAll(affix => EliteAffixes.IsAffixConflictingWithList(affix, selected));
                }

                if (available.Count == 0) break;

                string chosen = SelectWeightedRandom(available);
                selected.Add(chosen);
                available.Remove(chosen);
            }

            return selected;
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
            try
            {
                var preset = cmc.characterPreset;
                if (preset != null && !string.IsNullOrEmpty(preset.nameKey))
                {
                    string localized = preset.nameKey.ToPlainText();
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch
            {
            }

            string rawName = cmc.name ?? "未知敌人";
            rawName = rawName.Replace("(Clone)", "").Trim();

            if (string.Equals(rawName, "Character", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(rawName))
                rawName = "敌人";

            return rawName;
        }

        internal static string BuildColoredPrefix(IReadOnlyList<string> affixes)
        {
            if (affixes == null || affixes.Count == 0) return "[精英]";

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
        }
    }
}