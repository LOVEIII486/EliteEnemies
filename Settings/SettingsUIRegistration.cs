using System.Collections.Generic;
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
            RegisterGroups(affixKeys);

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
                description: LocalizationManager.GetText("Settings_DropRateMultiplier", "精英掉落倍率"),
                defaultValue: GameConfig.DropRateMultiplier,
                sliderRange: new Vector2(0f, 3f),
                onValueChange: GameConfig.SetDropRateMultiplier,
                decimalPlaces: 2,
                characterLimit: 5
            );
            
            ModSettingAPI.AddSlider(
                key: "ItemQualityBias",
                description: LocalizationManager.GetText("Settings_ItemQualityBias", "物品品质偏好"),
                defaultValue: GameConfig.ItemQualityBias,
                sliderRange: new Vector2(-3f, 3f),
                onValueChange: GameConfig.SetItemQualityBias,
                decimalPlaces: 1,
                characterLimit: 5
            );
            
            ModSettingAPI.AddToggle(
                key: "EnableBonusLoot",
                description: LocalizationManager.GetText("Settings_EnableBonusLoot", "启用奖励掉落（关闭后仅保留词缀特定掉落）"),
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
        }
        
        // 全局属性调整
        private static void RegisterGlobalMultipliers()
        {
            ModSettingAPI.AddSlider(
                key: "GlobalHealthMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalHealthMultiplier", "全局血量倍率"),
                defaultValue: GameConfig.GlobalHealthMultiplier,
                sliderRange: new Vector2(1f, 10f),
                onValueChange: GameConfig.SetGlobalHealthMultiplier,
                decimalPlaces: 1,
                characterLimit: 5
            );
            
            ModSettingAPI.AddSlider(
                key: "GlobalDamageMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalDamageMultiplier", "全局伤害倍率"),
                defaultValue: GameConfig.GlobalDamageMultiplier,
                sliderRange: new Vector2(1f, 10f),
                onValueChange: GameConfig.SetGlobalDamageMultiplier,
                decimalPlaces: 1,
                characterLimit: 5
            );
            
            ModSettingAPI.AddSlider(
                key: "GlobalSpeedMultiplier",
                description: LocalizationManager.GetText("Settings_GlobalSpeedMultiplier", "全局速度倍率"),
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
                description: LocalizationManager.GetText("Settings_AffixWeight1", "1个词条的权重（推荐：30-70）"),
                defaultValue: GameConfig.AffixWeight1,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight1
            );
            
            ModSettingAPI.AddSlider(
                key: "AffixWeight2",
                description: LocalizationManager.GetText("Settings_AffixWeight2", "2个词条的权重（推荐：20-50）"),
                defaultValue: GameConfig.AffixWeight2,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight2
            );
            
            ModSettingAPI.AddSlider(
                key: "AffixWeight3",
                description: LocalizationManager.GetText("Settings_AffixWeight3", "3个词条的权重（推荐：10-30）"),
                defaultValue: GameConfig.AffixWeight3,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight3
            );
            
            ModSettingAPI.AddSlider(
                key: "AffixWeight4",
                description: LocalizationManager.GetText("Settings_AffixWeight4", "4个词条的权重（推荐：1-10）"),
                defaultValue: GameConfig.AffixWeight4,
                minValue: 0,
                maxValue: 100,
                onValueChange: GameConfig.SetAffixWeight4
            );
            
            ModSettingAPI.AddSlider(
                key: "AffixWeight5",
                description: LocalizationManager.GetText("Settings_AffixWeight5", "5个词条的权重（推荐：0-5）"),
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
                string displayName = kvp.Value.Name;
                string description = $"[{displayName}] : {kvp.Value.Description}";

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

        // 注册分组
        private static void RegisterGroups(List<string> affixKeys)
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
                },
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: false
            );
            
            ModSettingAPI.AddGroup(
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
            );
            
            ModSettingAPI.AddGroup(
                key: "AffixCountWeights",
                description: LocalizationManager.GetText("Settings_AffixCountWeights_Group", "词条数量权重（数值越大越常见）"),
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
        }
    }
}