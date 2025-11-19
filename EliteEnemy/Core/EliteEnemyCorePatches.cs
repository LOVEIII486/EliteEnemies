using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Duckov.Scenes;
using Duckov.UI;
using EliteEnemies.AffixBehaviors;
using EliteEnemies.DebugTool;

namespace EliteEnemies
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
       
                // 跳过被标记禁止精英化的敌人
                if (EliteEnemyCore.HasNonEliteSuffix(presetName))
                {
                    EliteEnemyTracker.RecordDecision(presetName, processedFlag: false);
                    return;
                }
                
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
            if (ColorOverAmountField == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
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

            string baseName;
            if (!string.IsNullOrEmpty(marker.CustomDisplayName))
            {
                baseName = marker.CustomDisplayName;  // 优先使用自定义名称
            }
            else if (!string.IsNullOrEmpty(marker.BaseName))
            {
                baseName = marker.BaseName;
            }
            else
            {
                baseName = EliteEnemyCore.ResolveBaseName(cmc);
            }
            
            if (baseName.Contains("_"))
            {
                baseName = "o?o";
            }

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

    /// <summary>
    /// 玩家受伤Hook - 准确触发精英词缀的命中效果
    /// 方法名是 Hurt 不是 OnHurt
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