using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.UI;
using EliteEnemies.DebugTool;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.Core
{
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
                
                string presetName = cmc.characterPreset?.nameKey ?? string.Empty;
                
                if (EliteEnemyCore.IsIgnored(cmc.gameObject) || 
                    EliteEnemyCore.IsIgnoredPreset(cmc.characterPreset) ||
                    presetName.IndexOf("NonElite", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    EliteEnemyTracker.RecordDecision(presetName, processedFlag: false);
                    return;
                }
                
                bool isBoss = EliteEnemyCore.BossPresets.Contains(presetName);
                bool isMerchant = EliteEnemyCore.MerchantPresets.Contains(presetName);
                bool isNormal = EliteEnemyCore.IsEligiblePreset(presetName);
                bool isLove486 = presetName == "Enemy_Custom_Love486";
                
                if (!isBoss && !isMerchant && !isNormal && !isLove486)
                {
                    if (EliteEnemyCore.TryAutoRegisterExternalPreset(presetName))
                    {
                        isNormal = true;
                    }
                }

                float chance = 0f;
                if (isLove486) 
                    chance = 1.0f;
                else if (isBoss)
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
    }
    /// <summary>
    /// 强制精英敌人显示血条
    /// </summary>
    [HarmonyPatch(typeof(Health), "Start")]
    internal static class Health_ForceShowEliteHealthBar_Patch
    {
        private const string LogTag = "[EliteEnemies.HealthBar]";

        static void Postfix(Health __instance)
        {
            var cmc = __instance.TryGetCharacter();
            if (cmc == null || cmc.IsMainCharacter) return;

            var main = LevelManager.Instance?.MainCharacter;
            if (main && cmc.Team == main.Team) return;

            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker == null) return;

            // 强制显示血条
            if (!__instance.showHealthBar)
            {
                __instance.showHealthBar = true;
            }
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

        [HarmonyPrefix]
        private static void Prefix(HealthBar __instance)
        {
            if (ColorOverAmountField == null) return;

            var cmc = __instance?.target?.TryGetCharacter();
            if (cmc == null) return;

            var main = LevelManager.Instance?.MainCharacter;
            if (main && cmc.Team == main.Team) return;

            int affixCount = TryGetEliteAffixCount(cmc);

            Color color = GetHealthBarColor(affixCount);
            var gradient = CreateSolidGradient(color);

            ColorOverAmountField.SetValue(__instance, gradient);
        }

        private static int TryGetEliteAffixCount(Component cmc)
        {
            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker == null)
            {
                return 0;
            }
            int count = marker.Affixes?.Count ?? 0;
            return count;
        }

        private static Color GetHealthBarColor(int count)
        {
            switch (count)
            {
                case <= 0: return ParseColor("#FF4D4D"); // 红色
                case 1:    return ParseColor("#A673FF"); // 紫色
                case 2:    return ParseColor("#FFD700"); // 金色
                case 3:    return ParseColor("#FF10F0"); // 霓虹粉
                case 4:    return ParseColor("#00FFFF"); // 青色
                case 5:    return ParseColor("#8B0000"); // 血月色
                default:   return ParseColor("#1A1A1A"); // 6+ 黑色
            }
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
    /// 词缀名称补丁
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), "Awake")]
    internal static class HealthBar_Awake_Patch
    {
        static void Postfix(HealthBar __instance)
        {
            // 确保组件存在
            if (__instance.GetComponent<VisualEffects.EliteHealthBarUI>() == null)
            {
                __instance.gameObject.AddComponent<VisualEffects.EliteHealthBarUI>();
            }
        }
    }
    /// <summary>
    /// 玩家受伤Hook
    /// </summary>
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    public static class PlayerHitDetectionPatch
    {
        static void Postfix(DamageReceiver __instance, DamageInfo damageInfo)
        {
            if (damageInfo.fromCharacter == null)
                return;

            CharacterMainControl attacker = damageInfo.fromCharacter;

            var behaviorComponent = attacker.GetComponent<EliteBehaviorComponent>();
            if (behaviorComponent == null)
                return;

            var receiver = __instance.GetComponentInParent<CharacterMainControl>();
            if (receiver == null || !receiver.IsMainCharacter)
                return;

            if (attacker.Team == receiver.Team)
                return;

            behaviorComponent.TriggerHitPlayer(attacker, damageInfo);
        }
    }
}