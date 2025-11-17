using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Duckov.Scenes;
using Duckov.UI;
using EliteEnemies.DebugTool;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
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
            // Debug.Log($"{LogTag} DropRateMultiplier: {newConfig.DropRateMultiplier}");
            // Debug.Log($"{LogTag} ItemQualityBias: {newConfig.ItemQualityBias}");
            // Debug.Log($"{LogTag} EnableBonusLoot: {newConfig.EnableBonusLoot}");

            _config = newConfig;
        }

        public static void ForceMakeElite(CharacterMainControl cmc, IReadOnlyList<string> affixes)
        {
            if (!cmc) return;

            AccumulateFromAffixes(affixes, out float hp, out float dmg, out float spd);
            ApplyEliteMultipliers(cmc, hp, dmg, spd);
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

            // 如果总权重为0，使用均匀分布
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

        // ========== 倍率计算与应用 ==========

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

        private static void ApplyEliteMultipliers(CharacterMainControl cmc, float hpMult, float dmgMult, float spdMult)
        {
            if (!cmc) return;

            var health = cmc.Health;
            if (!health) return;

            try
            {
                var itemField = typeof(Health).GetField("item",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var item = itemField?.GetValue(health);

                if (item == null)
                {
                    Debug.LogWarning($"{LogTag} {cmc.name} 没有有效的 item，跳过倍率应用");
                    return;
                }

                var getStatMethod = item.GetType().GetMethod("GetStat", new[] { typeof(string) });
                if (getStatMethod == null)
                {
                    Debug.LogError($"{LogTag} 找不到 GetStat(string) 方法");
                    return;
                }

                void AddModifier(string statKey, float multiplier)
                {
                    try
                    {
                        var statObj = getStatMethod.Invoke(item, new object[] { statKey });
                        if (statObj == null) return;

                        var addModMethod = statObj.GetType().GetMethod("AddModifier", new[] { typeof(Modifier) });
                        if (addModMethod == null) return;

                        float delta = multiplier - 1f;
                        if (Mathf.Approximately(delta, 0f)) return;

                        addModMethod.Invoke(statObj,
                            new object[] { new Modifier(ModifierType.PercentageMultiply, delta, cmc) });
                    }
                    catch
                    {
                    }
                }

                AddModifier("MaxHealth", hpMult);
                AddModifier("WalkSpeed", spdMult);
                AddModifier("RunSpeed", spdMult);
                AddModifier("GunDamageMultiplier", dmgMult);
                AddModifier("MeleeDamageMultiplier", dmgMult);

                health.SetHealth(health.MaxHealth);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用倍率失败: {ex.Message}");
            }
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

    // ========== Harmony 补丁 ==========

    /// <summary>
    /// AI 角色初始化时应用精英效果
    /// </summary>
    [HarmonyPatch(typeof(AICharacterController), nameof(AICharacterController.Init))]
    internal static class AICharacterController_Init_Postfix
    {
        private const string LogTag = "[EliteEnemies.Patch]";

        static void Postfix(AICharacterController __instance, CharacterMainControl _characterMainControl)
        {
            try
            {
                var cmc = _characterMainControl;
                if (!cmc || cmc.IsMainCharacter) return;

                var main = LevelManager.Instance?.MainCharacter;
                if (main && cmc.Team == main.Team) return;

                if (cmc.GetComponent<EliteEnemyCore.EliteMarker>() != null) return;

                string presetName = cmc.characterPreset?.nameKey ?? string.Empty;

                bool isBoss = EliteEnemyCore.BossPresets.Contains(presetName);
                bool isMerchant = EliteEnemyCore.MerchantPresets.Contains(presetName);
                bool isNormal = EliteEnemyCore.IsEligiblePreset(presetName);

                if (!isBoss && !isMerchant && !isNormal)
                {
                    if (EliteEnemyCore.TryAutoRegisterExternalPreset(presetName))
                    {
                        isNormal = true;
                    }
                }

                float chance = 0f;
                if (isBoss)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.BossEliteChance);
                else if (isMerchant)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.MerchantEliteChance);
                else if (isNormal)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.NormalEliteChance);
                else
                {
                    EliteEnemyTracker.RecordDecision(presetName, processedFlag: false);
                    return;
                }

                if (UnityEngine.Random.value > chance)
                {
                    EliteEnemyTracker.RecordDecision(presetName, processedFlag: false);
                    return;
                }

                int maxCount = Mathf.Max(1, EliteEnemyCore.Config.MaxAffixCount);
                var affixes = EliteEnemyCore.SelectRandomAffixes(maxCount, cmc);

                EliteEnemyCore.ForceMakeElite(cmc, affixes);

                string baseName = EliteEnemyCore.ResolveBaseName(cmc);
                EliteEnemyCore.TagAsElite(cmc, affixes, baseName);

                AttachBehaviorComponent(cmc, affixes);
                CreateEliteAura(cmc, affixes);

                EliteEnemyTracker.RecordDecision(presetName, processedFlag: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 处理失败: {ex.Message}");
            }
        }

        private static void AttachBehaviorComponent(CharacterMainControl cmc, List<string> affixes)
        {
            if (cmc == null || affixes == null || affixes.Count == 0) return;

            bool needsBehavior = false;
            foreach (string affixName in affixes)
            {
                if (AffixBehaviors.AffixBehaviorManager.IsRegistered(affixName))
                {
                    needsBehavior = true;
                    break;
                }
            }

            if (!needsBehavior) return;

            var component = cmc.gameObject.AddComponent<AffixBehaviors.EliteBehaviorComponent>();
            component.Initialize(cmc, affixes);

            cmc.BeforeCharacterSpawnLootOnDead += (damageInfo) => { component?.OnDeath(damageInfo); };
        }

        private static void CreateEliteAura(CharacterMainControl cmc, List<string> affixes)
        {
            if (cmc == null || affixes == null || affixes.Count == 0) return;

            try
            {
                Color auraColor = GetAuraColor(affixes.Count);
                var aura = Effects.EliteAuraManager.Instance.CreateAura(cmc, auraColor, affixes);

                if (aura != null)
                {
                    cmc.BeforeCharacterSpawnLootOnDead += (damageInfo) =>
                    {
                        Effects.EliteAuraManager.Instance.ReleaseAura(aura);
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 创建光环失败: {ex.Message}");
            }
        }

        private static Color GetAuraColor(int affixCount)
        {
            if (affixCount <= 0) return ParseColor("#FF4D4D");
            if (affixCount == 1) return ParseColor("#A673FF");
            if (affixCount == 2) return ParseColor("#FFD700");
            return ParseColor("#FFA500");
        }

        private static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }

    /// <summary>
    /// 血条颜色补丁
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), "Refresh")]
    internal static class HealthBar_EliteColor_Patch
    {
        private static readonly FieldInfo
            ColorOverAmountField = AccessTools.Field(typeof(HealthBar), "colorOverAmount");

        private static bool _loggedMissing;

        [HarmonyPrefix]
        private static void Postfix(HealthBar __instance)
        {
            try
            {
                if (ColorOverAmountField == null)
                {
                    if (!_loggedMissing)
                    {
                        _loggedMissing = true;
                        Debug.LogWarning("[EliteEnemies.Patch] HealthBar.colorOverAmount 字段未找到");
                    }

                    return;
                }

                var cmc = __instance?.target?.TryGetCharacter();
                if (cmc == null) return;

                var main = LevelManager.Instance?.MainCharacter;
                if (main && cmc.Team == main.Team) return;

                if (!TryGetEliteAffixCount(cmc, out int affixCount)) return;

                Color color = GetHealthBarColor(affixCount);
                var gradient = CreateSolidGradient(color);

                ColorOverAmountField.SetValue(__instance, gradient);
            }
            catch
            {
            }
        }

        private static bool TryGetEliteAffixCount(Component cmc, out int count)
        {
            count = 0;
            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker == null) return false;

            count = marker.Affixes?.Count ?? 0;
            return true;
        }

        private static Color GetHealthBarColor(int count)
        {
            if (count <= 0) return ParseColor("#FF4D4D");
            if (count == 1) return ParseColor("#A673FF");
            if (count == 2) return ParseColor("#FFD700");
            return ParseColor("#FFA500");
        }

        private static Gradient CreateSolidGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }

    /// <summary>
    /// 血条名称补丁
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), "LateUpdate")]
    internal static class HealthBar_EliteName_Patch
    {
        static void Prefix(HealthBar __instance, TextMeshProUGUI ___nameText)
        {
            var cmc = __instance?.target?.TryGetCharacter();
            if (!cmc) return;

            var main = LevelManager.Instance?.MainCharacter;
            if (main && cmc.Team == main.Team) return;

            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker == null) return;

            if (cmc.characterPreset != null && !cmc.characterPreset.showName)
            {
                cmc.characterPreset.showName = true;
            }

            if (___nameText == null) return;

            string baseName = string.IsNullOrEmpty(marker.BaseName)
                ? EliteEnemyCore.ResolveBaseName(cmc)
                : marker.BaseName;

            string prefix = EliteEnemyCore.BuildColoredPrefix(marker.Affixes);

            if (EliteEnemyCore.Config.ShowDetailedHealth)
            {
                var health = cmc.Health;
                if (health != null)
                {
                    int currentHP = Mathf.CeilToInt(health.CurrentHealth);
                    int maxHP = Mathf.CeilToInt(health.MaxHealth);
                    ___nameText.text = $"{prefix}{baseName} <color=#FFD700>[{currentHP}/{maxHP}]</color>";
                }
                else
                {
                    ___nameText.text = $"{prefix}{baseName}";
                }
            }
            else
            {
                ___nameText.text = $"{prefix}{baseName}";
            }
        }
    }
}