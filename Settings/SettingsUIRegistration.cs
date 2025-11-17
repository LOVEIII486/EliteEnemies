using System.Collections.Generic;
using ModSetting.Api;
using UnityEngine;

namespace EliteEnemies.Settings
{
    public static class SettingsUIRegistration
    {
        private const string LogTag = "[EliteEnemies.SettingsUI]";
        private const float GroupScale = 0.7f;
        private const bool GroupTopInsert = false;
        
        // 注册 ModSetting UI 控件
        public static void RegisterUI(SettingsBuilder builder)
        {
            if (builder == null)
            {
                Debug.LogWarning($"{LogTag} SettingsBuilder 未初始化，无法注册UI");
                return;
            }

            RegisterBasicSettings(builder);
            RegisterGlobalMultipliers(builder);
            var affixKeys = RegisterAffixToggles(builder);
            RegisterGroups(builder, affixKeys);

            Debug.Log($"{LogTag} UI 注册完成");
        }

        // 基础设置

        private static void RegisterBasicSettings(SettingsBuilder builder)
        {
            builder
                .AddSlider(
                    key: "NormalEliteChance",
                    description: LocalizationManager.GetText("Settings_NormalEliteChance", "普通怪物精英概率"),
                    defaultValue: GameConfig.NormalEliteChance,
                    sliderRange: new Vector2(0f, 1f),
                    onValueChange: GameConfig.SetNormalEliteChance,
                    decimalPlaces: 2,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "BossEliteChance",
                    description: LocalizationManager.GetText("Settings_BossEliteChance"),
                    defaultValue: GameConfig.BossEliteChance,
                    sliderRange: new Vector2(0f, 1f),
                    onValueChange: GameConfig.SetBossEliteChance,
                    decimalPlaces: 2,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "MerchantEliteChance",
                    description: LocalizationManager.GetText("Settings_MerchantEliteChance"),
                    defaultValue: GameConfig.MerchantEliteChance,
                    sliderRange: new Vector2(0f, 1f),
                    onValueChange: GameConfig.SetMerchantEliteChance,
                    decimalPlaces: 2,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "MaxAffixCount",
                    description: LocalizationManager.GetText("Settings_MaxAffixCount"),
                    defaultValue: GameConfig.MaxAffixCount,
                    minValue: 1,
                    maxValue: 5,
                    onValueChange: GameConfig.SetMaxAffixCount
                )
                .AddToggle(
                    key: "ShowDetailedHealth",
                    description: LocalizationManager.GetText("Settings_ShowDetailedHealth"),
                    enable: GameConfig.ShowDetailedHealth,
                    onValueChange: GameConfig.SetShowDetailedHealth
                )
                .AddSlider(
                    key: "DropRateMultiplier",
                    description: LocalizationManager.GetText("Settings_DropRateMultiplier", "精英掉落倍率"),
                    defaultValue: GameConfig.DropRateMultiplier,
                    sliderRange: new Vector2(0f, 3f),
                    onValueChange: GameConfig.SetDropRateMultiplier,
                    decimalPlaces: 2,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "ItemQualityBias",
                    description: LocalizationManager.GetText("Settings_ItemQualityBias", "物品品质偏好"),
                    defaultValue: GameConfig.ItemQualityBias,
                    sliderRange: new Vector2(-3f, 3f),
                    onValueChange: GameConfig.SetItemQualityBias,
                    decimalPlaces: 1,
                    characterLimit: 5
                )
                .AddToggle(
                    key: "EnableBonusLoot",
                    description: LocalizationManager.GetText("Settings_EnableBonusLoot", "启用奖励掉落（关闭后仅保留词缀特定掉落）"),
                    enable: GameConfig.EnableBonusLoot,
                    onValueChange: GameConfig.SetEnableBonusLoot
                )
                .AddToggle(
                    key: "ShowAffixFootText",
                    description: LocalizationManager.GetText("Settings_ShowAffixFootText", "显示敌人脚底的词缀文本"),
                    enable: GameConfig.ShowAffixFootText,
                    onValueChange: GameConfig.SetShowAffixFootText
                )
                .AddSlider(
                    key: "AffixFootTextFontSize",
                    description: LocalizationManager.GetText("Settings_AffixFootTextFontSize", "脚底词缀字体大小"),
                    defaultValue: GameConfig.AffixFootTextFontSize,
                    sliderRange: new Vector2(20f, 80f),
                    onValueChange: GameConfig.SetAffixFootTextFontSize,
                    decimalPlaces: 0,
                    characterLimit: 3
                );
        }

        // 全局属性调整

        private static void RegisterGlobalMultipliers(SettingsBuilder builder)
        {
            builder
                .AddSlider(
                    key: "GlobalHealthMultiplier",
                    description: LocalizationManager.GetText("Settings_GlobalHealthMultiplier", "全局血量倍率"),
                    defaultValue: GameConfig.GlobalHealthMultiplier,
                    sliderRange: new Vector2(1f, 10f),
                    onValueChange: GameConfig.SetGlobalHealthMultiplier,
                    decimalPlaces: 1,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "GlobalDamageMultiplier",
                    description: LocalizationManager.GetText("Settings_GlobalDamageMultiplier", "全局伤害倍率"),
                    defaultValue: GameConfig.GlobalDamageMultiplier,
                    sliderRange: new Vector2(1f, 10f),
                    onValueChange: GameConfig.SetGlobalDamageMultiplier,
                    decimalPlaces: 1,
                    characterLimit: 5
                )
                .AddSlider(
                    key: "GlobalSpeedMultiplier",
                    description: LocalizationManager.GetText("Settings_GlobalSpeedMultiplier", "全局速度倍率"),
                    defaultValue: GameConfig.GlobalSpeedMultiplier,
                    sliderRange: new Vector2(1f, 10f),
                    onValueChange: GameConfig.SetGlobalSpeedMultiplier,
                    decimalPlaces: 1,
                    characterLimit: 5
                );
        }

        // 词缀开关

        private static List<string> RegisterAffixToggles(SettingsBuilder builder)
        {
            var affixKeys = new List<string>();

            foreach (var kvp in EliteAffixes.Pool)
            {
                string affixKey = kvp.Key;
                string displayName = kvp.Value.Name;
                string description = $"[{displayName}] : {kvp.Value.Description}";

                builder.AddToggle(
                    key: affixKey,
                    description: description,
                    enable: GameConfig.IsAffixEnabled(affixKey),
                    onValueChange: (bool value) => GameConfig.SetAffixEnabled(affixKey, value)
                );

                affixKeys.Add(affixKey);
            }

            return affixKeys;
        }

        // 注册分组

        private static void RegisterGroups(SettingsBuilder builder, List<string> affixKeys)
        {
            builder
                .AddGroup(
                    key: "BasicSettings",
                    description: LocalizationManager.GetText("Settings_BasicSettings_Group"),
                    keys: new List<string>
                    {
                        "NormalEliteChance",
                        "BossEliteChance",
                        "MerchantEliteChance",
                        "MaxAffixCount",
                        "ShowDetailedHealth",
                        "DropRateMultiplier",
                        "ItemQualityBias",
                        "EnableBonusLoot",
                        "ShowAffixFootText",
                        "AffixFootTextFontSize"
                    },
                    scale: GroupScale,
                    topInsert: GroupTopInsert,
                    open: true
                )
                .AddGroup(
                    key: "GlobalMultipliers",
                    description: LocalizationManager.GetText("Settings_GlobalMultipliers_Group", "全局属性调整"),
                    keys: new List<string>
                    {
                        "GlobalHealthMultiplier",
                        "GlobalDamageMultiplier",
                        "GlobalSpeedMultiplier"
                    },
                    scale: GroupScale,
                    topInsert: GroupTopInsert,
                    open: false
                )
                .AddGroup(
                    key: "AffixToggles",
                    description: LocalizationManager.GetText("Settings_AffixToggles_Group"),
                    keys: affixKeys,
                    scale: GroupScale,
                    topInsert: GroupTopInsert,
                    open: false
                );
        }
    }
}