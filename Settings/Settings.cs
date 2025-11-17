using System.Collections.Generic;
using ModSetting.Api;
using UnityEngine;

namespace EliteEnemies.Settings
{
    /// <summary>
    /// 精英敌人配置管理
    /// </summary>
    public static class GameConfig 
    {
        private const string LogTag = "[EliteEnemies.Settings]";
        
        // 配置范围统一定义
        private static class ConfigRanges
        {
            public const float MinDropRate = 0f;
            public const float MaxDropRate = 3f;
            public const float MinQualityBias = -3f;
            public const float MaxQualityBias = 3f;
            public const float MinMultiplier = 1f;
            public const float MaxMultiplier = 10f;
            public const float MinFontSize = 20f;
            public const float MaxFontSize = 80f;
            public const int MinAffixCountLimit = 1;
            public const int MaxAffixCountLimit = 5;
        }

        // 默认禁用的词缀名单
        private static readonly HashSet<string> DefaultDisabledAffixes = new HashSet<string>
        {
            "MimicTear",
            "Sticky"
        };
        
        private static Dictionary<string, bool> _affixStates = new Dictionary<string, bool>();
        
        public static float NormalEliteChance { get; private set; }
        public static float BossEliteChance { get; private set; }
        public static float MerchantEliteChance { get; private set; }
        public static int MaxAffixCount { get; private set; }
        public static bool ShowDetailedHealth { get; private set; }
        public static float DropRateMultiplier { get; private set; }
        public static float ItemQualityBias { get; private set; }
        public static float GlobalHealthMultiplier { get; private set; }
        public static float GlobalDamageMultiplier { get; private set; }
        public static float GlobalSpeedMultiplier { get; private set; }
        public static bool EnableBonusLoot { get; private set; }
        public static bool ShowAffixFootText { get; private set; }
        public static float AffixFootTextFontSize { get; private set; }

        // ==================== 初始化 ====================

        /// <summary>
        /// 从 ModSetting 加载保存的配置
        /// </summary>
        public static void Init(SettingsBuilder settingsBuilder)
        {
            if (settingsBuilder.HasConfig())
            {
                LoadFromConfig(settingsBuilder);
            }
            else
            {
                LoadDefaults();
            }

            Debug.Log($"{LogTag} 配置加载完成");
        }

        private static void LoadFromConfig(SettingsBuilder builder)
        {
            NormalEliteChance = builder.GetSavedValue("NormalEliteChance", out float normal) ? normal : 1.0f;
            BossEliteChance = builder.GetSavedValue("BossEliteChance", out float boss) ? boss : 0.0f;
            MerchantEliteChance = builder.GetSavedValue("MerchantEliteChance", out float merchant) ? merchant : 0.0f;
            MaxAffixCount = builder.GetSavedValue("MaxAffixCount", out int maxCount) ? Mathf.Clamp(maxCount, ConfigRanges.MinAffixCountLimit, ConfigRanges.MaxAffixCountLimit) : 2;
            ShowDetailedHealth = builder.GetSavedValue("ShowDetailedHealth", out bool showHealth) ? showHealth : true;
            DropRateMultiplier = builder.GetSavedValue("DropRateMultiplier", out float dropRate) ? Mathf.Clamp(dropRate, ConfigRanges.MinDropRate, ConfigRanges.MaxDropRate) : 1.0f;
            ItemQualityBias = builder.GetSavedValue("ItemQualityBias", out float qualityBias) ? Mathf.Clamp(qualityBias, ConfigRanges.MinQualityBias, ConfigRanges.MaxQualityBias) : -0.8f;
            EnableBonusLoot = builder.GetSavedValue("EnableBonusLoot", out bool enableBonus) ? enableBonus : true;
            GlobalHealthMultiplier = builder.GetSavedValue("GlobalHealthMultiplier", out float healthMult) ? Mathf.Clamp(healthMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier) : 1.0f;
            GlobalDamageMultiplier = builder.GetSavedValue("GlobalDamageMultiplier", out float damageMult) ? Mathf.Clamp(damageMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier) : 1.0f;
            GlobalSpeedMultiplier = builder.GetSavedValue("GlobalSpeedMultiplier", out float speedMult) ? Mathf.Clamp(speedMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier) : 1.0f;
            ShowAffixFootText = builder.GetSavedValue("ShowAffixFootText", out bool footText) ? footText : true;
            AffixFootTextFontSize = builder.GetSavedValue("AffixFootTextFontSize", out float fontSize) ? Mathf.Clamp(fontSize, ConfigRanges.MinFontSize, ConfigRanges.MaxFontSize) : 35f;
            // 添加新配置项需要修改！！！
            
            LoadAffixStates(builder);
        }

        private static void LoadDefaults()
        {
            NormalEliteChance = 1.0f;
            BossEliteChance = 0.0f;
            MerchantEliteChance = 0.0f;
            MaxAffixCount = 2;
            ShowDetailedHealth = true;
            DropRateMultiplier = 1.0f;
            ItemQualityBias = -0.8f;
            EnableBonusLoot = true;
            GlobalHealthMultiplier = 1.0f;
            GlobalDamageMultiplier = 1.0f;
            GlobalSpeedMultiplier = 1.0f;
            ShowAffixFootText = true;
            AffixFootTextFontSize = 35f;

            LoadAffixStates(null);
        }

        private static void LoadAffixStates(SettingsBuilder builder)
        {
            foreach (var kvp in EliteAffixes.Pool)
            {
                string key = kvp.Key;
                bool defaultState = !DefaultDisabledAffixes.Contains(key);
                
                if (builder != null && builder.GetSavedValue(key, out bool saved))
                {
                    _affixStates[key] = saved;
                }
                else
                {
                    _affixStates[key] = defaultState;
                }
            }
        }

        // ==================== 设置方法 ====================

        public static void SetNormalEliteChance(float value)
        {
            NormalEliteChance = Mathf.Clamp01(value);
            NotifyConfigChanged();
        }

        public static void SetBossEliteChance(float value)
        {
            BossEliteChance = Mathf.Clamp01(value);
            NotifyConfigChanged();
        }

        public static void SetMerchantEliteChance(float value)
        {
            MerchantEliteChance = Mathf.Clamp01(value);
            NotifyConfigChanged();
        }

        public static void SetMaxAffixCount(int value)
        {
            MaxAffixCount = Mathf.Clamp(value, ConfigRanges.MinAffixCountLimit, ConfigRanges.MaxAffixCountLimit);
            NotifyConfigChanged();
        }
        public static void SetShowDetailedHealth(bool value)
        {
            ShowDetailedHealth = value;
            NotifyConfigChanged();
        }

        public static void SetDropRateMultiplier(float value)
        {
            DropRateMultiplier = Mathf.Clamp(value, ConfigRanges.MinDropRate, ConfigRanges.MaxDropRate);
            EliteLootSystem.GlobalDropRate = DropRateMultiplier;
            NotifyConfigChanged();
        }

        public static void SetItemQualityBias(float value)
        {
            ItemQualityBias = Mathf.Clamp(value, ConfigRanges.MinQualityBias, ConfigRanges.MaxQualityBias);
            
            if (ModBehaviour.LootHelper != null)
            {
                ModBehaviour.LootHelper.qualityBiasPower = ItemQualityBias;
            }
            
            NotifyConfigChanged();
        }

        public static void SetEnableBonusLoot(bool value)
        {
            EnableBonusLoot = value;
            NotifyConfigChanged();
        }

        public static void SetGlobalHealthMultiplier(float value)
        {
            GlobalHealthMultiplier = Mathf.Clamp(value, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier);
            NotifyConfigChanged();
        }

        public static void SetGlobalDamageMultiplier(float value)
        {
            GlobalDamageMultiplier = Mathf.Clamp(value, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier);
            NotifyConfigChanged();
        }

        public static void SetGlobalSpeedMultiplier(float value)
        {
            GlobalSpeedMultiplier = Mathf.Clamp(value, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier);
            NotifyConfigChanged();
        }

        public static void SetShowAffixFootText(bool value)
        {
            ShowAffixFootText = value;
            NotifyConfigChanged();
        }

        public static void SetAffixFootTextFontSize(float value)
        {
            AffixFootTextFontSize = Mathf.Clamp(value, ConfigRanges.MinFontSize, ConfigRanges.MaxFontSize);
        }

        // ==================== 词缀管理 ====================

        public static void SetAffixEnabled(string affixKey, bool enabled)
        {
            if (!EliteAffixes.Pool.ContainsKey(affixKey))
            {
                Debug.LogWarning($"{LogTag} 未知的词缀键: {affixKey}");
                return;
            }

            _affixStates[affixKey] = enabled;
            NotifyConfigChanged();
        }

        public static bool IsAffixEnabled(string affixKey)
        {
            return _affixStates.TryGetValue(affixKey, out bool enabled) && enabled;
        }

        public static HashSet<string> GetDisabledAffixBlacklist()
        {
            var blacklist = new HashSet<string>();

            foreach (var kvp in EliteAffixes.Pool)
            {
                if (!IsAffixEnabled(kvp.Key))
                {
                    blacklist.Add(kvp.Key);
                }
            }

            return blacklist;
        }

        public static EliteEnemiesConfig GetConfig()
        {
            return new EliteEnemiesConfig
            {
                NormalEliteChance = NormalEliteChance,
                BossEliteChance = BossEliteChance,
                MerchantEliteChance = MerchantEliteChance,
                MaxAffixCount = MaxAffixCount,
                ShowDetailedHealth = ShowDetailedHealth,
                DropRateMultiplier = DropRateMultiplier,
                ItemQualityBias = ItemQualityBias,
                EnableBonusLoot = EnableBonusLoot,
                GlobalHealthMultiplier = GlobalHealthMultiplier,
                GlobalDamageMultiplier = GlobalDamageMultiplier,
                GlobalSpeedMultiplier = GlobalSpeedMultiplier,
                ShowAffixFootText = ShowAffixFootText,
                DisabledAffixes = GetDisabledAffixBlacklist()
            };
        }
        
        // 同步设置项更新到核心
        private static void NotifyConfigChanged()
        {
            EliteEnemyCore.UpdateConfig(GetConfig());
        }
    }
}