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

                // 排除友军
                var main = LevelManager.Instance?.MainCharacter;
                if (main && cmc.Team == main.Team) return;

                var preset = cmc.characterPreset;
                if (preset == null) return;

                // 获取资源名作为唯一标识符
                string rName = preset.name;
                string baseName = EliteEnemyCore.ResolveBaseName(cmc);
                
                // 1. 忽略逻辑判定
                if (EliteEnemyCore.IsIgnored(cmc.gameObject) || 
                    EliteEnemyCore.IsIgnoredPreset(preset) ||
                    EliteEnemyCore.IgnoredGenericPresets.Contains(rName))
                {
                    EliteEnemyTracker.RecordDecision(rName, baseName, processedFlag: false);
                    return;
                }
                
                // 2. 类型分类判定
                bool isBoss = EliteEnemyCore.BossPresets.Contains(rName);
                bool isMerchant = EliteEnemyCore.MerchantPresets.Contains(rName);
                bool isNormal = EliteEnemyCore.IsEligiblePreset(preset);
                
                // 3. 自动注册逻辑
                if (!isBoss && !isMerchant && !isNormal)
                {
                    if (EliteEnemyCore.TryAutoRegisterExternalPreset(preset))
                    {
                        isNormal = true;
                    }
                }

                // 4. 精英化概率计算
                float chance = 0f;
                if (isBoss)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.BossEliteChance);
                else if (isMerchant)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.MerchantEliteChance);
                else if (isNormal)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.NormalEliteChance);
                else
                {
                    EliteEnemyTracker.RecordDecision(rName, baseName, processedFlag: true);
                    return;
                }

                // 随机判定
                if (UnityEngine.Random.value > chance)
                {
                    EliteEnemyTracker.RecordDecision(rName, baseName, processedFlag: true);
                    return;
                }

                // 5. 应用精英化
                int maxCount = Mathf.Max(1, EliteEnemyCore.Config.MaxAffixCount);
                var affixes = EliteEnemyCore.SelectRandomAffixes(maxCount, cmc);

                // 修改属性并标记
                EliteEnemyCore.ForceMakeElite(cmc, affixes);

                // 6. 附加行为组件
                AttachBehaviorComponent(cmc, affixes);

                EliteEnemyTracker.RecordDecision(rName, baseName, processedFlag: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} AI初始化补丁执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void AttachBehaviorComponent(CharacterMainControl cmc, List<string> affixes)
        {
            if (cmc == null || affixes == null || affixes.Count == 0) return;

            // 检查是否有任何词缀需要特殊的逻辑监听
            bool needsBehavior = false;
            foreach (string affixName in affixes)
            {
                if (AffixBehaviorManager.IsRegistered(affixName))
                {
                    needsBehavior = true;
                    break;
                }
            }

            if (!needsBehavior) return;

            // 添加核心逻辑驱动组件
            var component = cmc.gameObject.AddComponent<EliteBehaviorComponent>();
            component.Initialize(cmc, affixes);

            // 注册死亡清理回调
            cmc.BeforeCharacterSpawnLootOnDead += (damageInfo) => { 
                if(component != null) component.OnDeath(damageInfo); 
            };
        }
    }

    /// <summary>
    /// 强制精英敌人显示血条
    /// </summary>
    [HarmonyPatch(typeof(Health), "Start")]
    internal static class Health_ForceShowEliteHealthBar_Patch
    {
        static void Postfix(Health __instance)
        {
            var cmc = __instance.TryGetCharacter();
            if (cmc == null || cmc.IsMainCharacter) return;
            
            // 特殊UI黑名单怪物不显示血条
            if (cmc.characterPreset != null && EliteEnemyCore.IsUIHidden(cmc.characterPreset.name))
            {
                return;
            }

            // 仅对精英怪强制开启血条
            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker != null && !__instance.showHealthBar)
            {
                __instance.showHealthBar = true;
            }
        }
    }

    /// <summary>
    /// 血条颜色与外观补丁
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), "Refresh")]
    internal static class HealthBar_EliteColor_Patch
    {
        private static readonly FieldInfo ColorOverAmountField = AccessTools.Field(typeof(HealthBar), "colorOverAmount");

        [HarmonyPrefix]
        private static void Prefix(HealthBar __instance)
        {
            if (ColorOverAmountField == null || __instance.target == null) return;

            var cmc = __instance.target.TryGetCharacter();
            if (cmc == null) return;

            // 检查是否有精英标记组件
            var marker = cmc.GetComponent<EliteEnemyCore.EliteMarker>();

            // 如果标记不存在，或者标记里的词缀列表为空，将颜色还原为原生红色
            if (marker == null || marker.Affixes == null || marker.Affixes.Count == 0)
            {
                // 原生红色：#FF4D4D
                var defaultGradient = CreateSolidGradient(ParseColor("#FF4D4D"));
                ColorOverAmountField.SetValue(__instance, defaultGradient);
                return; 
            }

            // 如果是精英怪，按词缀数量染色
            int affixCount = marker.Affixes.Count;
            Color eliteColor = GetHealthBarColor(affixCount);
            var gradient = CreateSolidGradient(eliteColor);

            ColorOverAmountField.SetValue(__instance, gradient);
        }

        private static Color GetHealthBarColor(int count)
        {
            return count switch
            {
                1 => ParseColor("#A673FF"), // 紫色
                2 => ParseColor("#FFD700"), // 金色
                3 => ParseColor("#FF10F0"), // 霓虹粉
                4 => ParseColor("#00FFFF"), // 青色
                5 => ParseColor("#8B0000"), // 血月色
                >= 6 => ParseColor("#1A1A1A"), // 深渊黑
                _ => ParseColor("#FF4D4D")  // 默认红
            };
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
    /// 词缀显示组件挂载
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), "Awake")]
    internal static class HealthBar_Awake_Patch
    {
        static void Postfix(HealthBar __instance)
        {
            if (__instance.GetComponent<VisualEffects.EliteHealthBarUI>() == null)
            {
                __instance.gameObject.AddComponent<VisualEffects.EliteHealthBarUI>();
            }
        }
    }

    /// <summary>
    /// 玩家受伤检测逻辑 (用于触发词缀的战斗逻辑)
    /// </summary>
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    public static class PlayerHitDetectionPatch
    {
        static void Postfix(DamageReceiver __instance, DamageInfo damageInfo)
        {
            // 必须是由角色造成的伤害
            if (damageInfo.fromCharacter == null) return;

            CharacterMainControl attacker = damageInfo.fromCharacter;

            // 检查攻击者是否是拥有行为组件的精英怪
            var behaviorComponent = attacker.GetComponent<EliteBehaviorComponent>();
            if (behaviorComponent == null) return;

            // 检查受击者是否是玩家
            var receiver = __instance.GetComponentInParent<CharacterMainControl>();
            if (receiver == null || !receiver.IsMainCharacter) return;

            // 确保不是同队伤害
            if (attacker.Team != receiver.Team)
            {
                behaviorComponent.TriggerHitPlayer(attacker, damageInfo);
            }
        }
    }
}