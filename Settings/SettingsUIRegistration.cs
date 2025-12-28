using System.Collections.Generic;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.EliteEnemy.ComboSystem;
using EliteEnemies.Localization;
using EliteEnemies.ModSettingsApi;
using UnityEngine;

namespace EliteEnemies.Settings
{
    public static class SettingsUIRegistration
    {
        private const string LogTag = "[EliteEnemies.SettingsUI]";
        private const float GroupScale = 0.7f;
        private const bool GroupTopInsert = false;

        // 注册 ModSetting UI 控件
        public static void RegisterUI()
        {
            if (!ModSettingAPI.IsInit)
            {
                Debug.LogWarning($"{LogTag} ModSettingAPI 未初始化，无法注册UI");
                return;
            }

            RegisterBasicSettings();
            RegisterVisualSettings();
            RegisterGlobalMultipliers();
            RegisterAffixCountWeights();

            var affixKeys = RegisterAffixToggles();
            RegisterAffixSpecialSettings();

            var comboKeys = RegisterComboToggles();

            // 必须最后调用
            RegisterGroups(affixKeys, comboKeys);
            Debug.Log($"{LogTag} UI 注册完成");
        }

        // 基础设置
        private static void RegisterBasicSettings()
        {
            ModSettingAPI.AddSlider(
                key: "NormalEliteChance",
                description: LocalizationManager.GetText("Settings_NormalEliteChance"),
                defaultValue: GameConfig.NormalEliteChance,
                sliderRange: new Vector2(0f, 1f),
                onValueChange: GameConfig.SetNormalEliteChance,
                decimalPlaces: 2,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "BossEliteChance",
                description: LocalizationManager.GetText("Settings_BossEliteChance"),
                defaultValue: GameConfig.BossEliteChance,
                sliderRange: new Vector2(0f, 1f),
                onValueChange: GameConfig.SetBossEliteChance,
                decimalPlaces: 2,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "MerchantEliteChance",
                description: LocalizationManager.GetText("Settings_MerchantEliteChance"),
                defaultValue: GameConfig.MerchantEliteChance,
                sliderRange: new Vector2(0f, 1f),
                onValueChange: GameConfig.SetMerchantEliteChance,
                decimalPlaces: 2,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "MaxAffixCount",
                description: LocalizationManager.GetText("Settings_MaxAffixCount"),
                defaultValue: GameConfig.MaxAffixCount,
                minValue: 1,
                maxValue: 5,
                onValueChange: GameConfig.SetMaxAffixCount
            );

            ModSettingAPI.AddSlider(
                key: "DropRateMultiplier",
                description: LocalizationManager.GetText("Settings_DropRateMultiplier"),
                defaultValue: GameConfig.DropRateMultiplier,
                sliderRange: new Vector2(0f, 3f),
                onValueChange: GameConfig.SetDropRateMultiplier,
                decimalPlaces: 2,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "ItemQualityBias",
                description: LocalizationManager.GetText("Settings_ItemQualityBias"),
                defaultValue: GameConfig.ItemQualityBias,
                sliderRange: new Vector2(-3f, 3f),
                onValueChange: GameConfig.SetItemQualityBias,
                decimalPlaces: 1,
                characterLimit: 5
            );

            ModSettingAPI.AddToggle(
                key: "EnableBonusLoot",
                description: LocalizationManager.GetText("Settings_EnableBonusLoot"),
                enable: GameConfig.EnableBonusLoot,
                onValueChange: GameConfig.SetEnableBonusLoot
            );
        }


        private static void RegisterVisualSettings()
        {
            ModSettingAPI.AddToggle(
                key: "ShowEliteName",
                description: LocalizationManager.GetText("Settings_ShowEliteName"),
                enable: GameConfig.ShowEliteName,
                onValueChange: GameConfig.SetShowEliteName
            );

            ModSettingAPI.AddToggle(
                key: "ShowDetailedHealth",
                description: LocalizationManager.GetText("Settings_ShowDetailedHealth"),
                enable: GameConfig.ShowDetailedHealth,
                onValueChange: GameConfig.SetShowDetailedHealth
            );

            var displayPositionOptions = new List<string>
            {
                "Overhead",
                "Underfoot"
            };

            string currentPosition = GameConfig.AffixDisplayPosition.ToString();

            ModSettingAPI.AddDropdownList(
                key: "AffixDisplayPosition",
                description: LocalizationManager.GetText("Settings_AffixDisplayPosition"),
                options: displayPositionOptions,
                defaultValue: currentPosition,
                onValueChange: GameConfig.SetAffixDisplayPosition
            );

            ModSettingAPI.AddSlider(
                key: "AffixFontSize",
                description: LocalizationManager.GetText("Settings_AffixFontSize"),
                defaultValue: GameConfig.AffixFontSize,
                minValue: 20,
                maxValue: 40,
                onValueChange: GameConfig.SetAffixFontSize
            );
        }

        // 全局属性调整
        private static void RegisterGlobalMultipliers()
        {
            ModSettingAPI.AddSlider(
                key: "GlobalHealthMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalHealthMultiplier"),
                defaultValue: GameConfig.GlobalHealthMultiplier,
                sliderRange: new Vector2(1f, 10f),
                onValueChange: GameConfig.SetGlobalHealthMultiplier,
                decimalPlaces: 1,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "GlobalDamageMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalDamageMultiplier"),
                defaultValue: GameConfig.GlobalDamageMultiplier,
                sliderRange: new Vector2(1f, 10f),
                onValueChange: GameConfig.SetGlobalDamageMultiplier,
                decimalPlaces: 1,
                characterLimit: 5
            );

            ModSettingAPI.AddSlider(
                key: "GlobalSpeedMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalSpeedMultiplier"),
                defaultValue: GameConfig.GlobalSpeedMultiplier,
                sliderRange: new Vector2(1f, 10f),
                onValueChange: GameConfig.SetGlobalSpeedMultiplier,
                decimalPlaces: 1,
                characterLimit: 5
            );
        }

        // 词条数量权重设置
        private static void RegisterAffixCountWeights()
        {
            ModSettingAPI.AddSlider(
                key: "AffixWeight1",
                description: LocalizationManager.GetText("Settings_AffixWeight1"),
                defaultValue: GameConfig.AffixWeight1,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight1
            );

            ModSettingAPI.AddSlider(
                key: "AffixWeight2",
                description: LocalizationManager.GetText("Settings_AffixWeight2"),
                defaultValue: GameConfig.AffixWeight2,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight2
            );

            ModSettingAPI.AddSlider(
                key: "AffixWeight3",
                description: LocalizationManager.GetText("Settings_AffixWeight3"),
                defaultValue: GameConfig.AffixWeight3,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight3
            );

            ModSettingAPI.AddSlider(
                key: "AffixWeight4",
                description: LocalizationManager.GetText("Settings_AffixWeight4"),
                defaultValue: GameConfig.AffixWeight4,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight4
            );

            ModSettingAPI.AddSlider(
                key: "AffixWeight5",
                description: LocalizationManager.GetText("Settings_AffixWeight5"),
                defaultValue: GameConfig.AffixWeight5,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight5
            );
        }

        // 词缀开关
        private static List<string> RegisterAffixToggles()
        {
            var affixKeys = new List<string>();

            foreach (var kvp in EliteAffixes.Pool)
            {
                string affixKey = kvp.Key;
                string description = $"{kvp.Value.ColoredTag} : {kvp.Value.Description}";

                ModSettingAPI.AddToggle(
                    key: affixKey,
                    description: description,
                    enable: GameConfig.IsAffixEnabled(affixKey),
                    onValueChange: (bool value) => GameConfig.SetAffixEnabled(affixKey, value)
                );

                affixKeys.Add(affixKey);
            }

            return affixKeys;
        }

        // 词缀特殊设置
        private static void RegisterAffixSpecialSettings()
        {
            ModSettingAPI.AddSlider(
                key: "SplitAffixMaxCloneCount",
                description: LocalizationManager.GetText("Settings_SplitAffixMaxCloneCount"),
                defaultValue: GameConfig.SplitAffixMaxCloneCount,
                minValue: 10,
                maxValue: 100,
                onValueChange: GameConfig.SetSplitAffixMaxCloneCount
            );

            ModSettingAPI.AddSlider(
                key: "SplitAffixMinFPSThreshold",
                description: LocalizationManager.GetText("Settings_SplitAffixMinFPSThreshold"),
                defaultValue: GameConfig.SplitAffixMinFPSThreshold,
                sliderRange: new Vector2(10f, 60f),
                onValueChange: GameConfig.SetSplitAffixMinFPSThreshold,
                decimalPlaces: 0,
                characterLimit: 5
            );
        }

        // combo开关
        private static List<string> RegisterComboToggles()
        {
            var comboKeys = new List<string>();

            ModSettingAPI.AddToggle(
                key: "EnableComboSystem",
                description: LocalizationManager.GetText("Settings_EnableComboSystem"),
                enable: GameConfig.EnableComboSystem,
                onValueChange: GameConfig.SetEnableComboSystem
            );

            ModSettingAPI.AddSlider(
                key: "ComboSystemChance",
                description: LocalizationManager.GetText("Settings_ComboSystemChance"),
                defaultValue: GameConfig.ComboSystemChance,
                sliderRange: new Vector2(0f, 1f),
                onValueChange: GameConfig.SetComboSystemChance,
                decimalPlaces: 2,
                characterLimit: 5
            );

            comboKeys.Add("EnableComboSystem");
            comboKeys.Add("ComboSystemChance");

            foreach (var combo in EliteComboRegistry.ComboPool)
            {
                string key = combo.ComboId;
                string dynamicDesc = combo.GetFormattedDescription();
                ModSettingAPI.AddToggle(
                    key: key,
                    description: dynamicDesc,
                    enable: GameConfig.IsComboEnabled(key),
                    onValueChange: (bool value) => GameConfig.SetComboEnabled(key, value)
                );

                comboKeys.Add(key);
            }

            return comboKeys;
        }

        // 注册分组
        private static void RegisterGroups(List<string> affixKeys, List<string> comboKeys)
        {
            ModSettingAPI.AddGroup(
                key: "BasicSettings",
                description: LocalizationManager.GetText("Settings_BasicSettings_Group"),
                keys: new List<string>
                {
                    "NormalEliteChance",
                    "BossEliteChance",
                    "MerchantEliteChance",
                    "MaxAffixCount",
                    "DropRateMultiplier",
                    "ItemQualityBias",
                    "EnableBonusLoot",
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: true
            );

            ModSettingAPI.AddGroup(
                key: "VisualSettings",
                description: LocalizationManager.GetText("Settings_VisualSettings_Group"),
                keys: new List<string>
                {
                    "ShowEliteName",
                    "ShowDetailedHealth",
                    "AffixDisplayPosition",
                    "AffixFontSize"
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );

            ModSettingAPI.AddGroup(
                key: "GlobalMultipliers",
                description: LocalizationManager.GetText("Settings_GlobalMultipliers_Group"),
                keys: new List<string>
                {
                    "GlobalHealthMultiplier",
                    "GlobalDamageMultiplier",
                    "GlobalSpeedMultiplier"
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );

            ModSettingAPI.AddGroup(
                key: "AffixCountWeights",
                description: LocalizationManager.GetText("Settings_AffixCountWeights_Group"),
                keys: new List<string>
                {
                    "AffixWeight1",
                    "AffixWeight2",
                    "AffixWeight3",
                    "AffixWeight4",
                    "AffixWeight5"
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );

            ModSettingAPI.AddGroup(
                key: "AffixToggles",
                description: LocalizationManager.GetText("Settings_AffixToggles_Group"),
                keys: affixKeys,
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );

            ModSettingAPI.AddGroup(
                key: "ComboSettings",
                description: LocalizationManager.GetText("Settings_ComboSettings_Group"),
                keys: comboKeys,
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );

            ModSettingAPI.AddGroup(
                key: "AffixSpecialSettings",
                description: LocalizationManager.GetText("Settings_AffixSpecialSettings_Group"),
                keys: new List<string>
                {
                    "SplitAffixMaxCloneCount",
                    "SplitAffixMinFPSThreshold"
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );
        }
    }
}