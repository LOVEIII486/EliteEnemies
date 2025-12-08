using System.Collections.Generic;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.EliteEnemy.LootSystem;
using EliteEnemies.ModSettingsApi;
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
            public const int MinAffixCountLimit = 1;
            public const int MaxAffixCountLimit = 5;
            public const int MinAffixWeight = 0;
            public const int MaxAffixWeight = 100;
        }
        
        public enum AffixTextDisplayPosition
        {
            Overhead,
            Underfoot
        }

        // 默认禁用的词缀名单
        private static readonly HashSet<string> DefaultDisabledAffixes = new HashSet<string>
        {
            "Sticky"
        };

        private static Dictionary<string, bool> _affixStates = new Dictionary<string, bool>();

        public static float NormalEliteChance { get; private set; }
        public static float BossEliteChance { get; private set; }
        public static float MerchantEliteChance { get; private set; }
        public static int MaxAffixCount { get; private set; }
        public static float DropRateMultiplier { get; private set; }
        public static float ItemQualityBias { get; private set; }
        public static float GlobalHealthMultiplier { get; private set; }
        public static float GlobalDamageMultiplier { get; private set; }
        public static float GlobalSpeedMultiplier { get; private set; }
        public static bool EnableBonusLoot { get; private set; }

        public static bool ShowEliteName { get; private set; }
        public static bool ShowDetailedHealth { get; private set; }
        public static AffixTextDisplayPosition AffixDisplayPosition { get; private set; } = AffixTextDisplayPosition.Overhead;
        public static int AffixFontSize { get; private set; }

        // 词条数量权重
        public static int AffixWeight1 { get; private set; }
        public static int AffixWeight2 { get; private set; }
        public static int AffixWeight3 { get; private set; }
        public static int AffixWeight4 { get; private set; }
        public static int AffixWeight5 { get; private set; }

        // ==================== 初始化 ====================

        /// <summary>
        /// 从 ModSetting 加载保存的配置
        /// </summary>
        public static void Init()
        {
            if (ModSettingAPI.HasConfig())
            {
                LoadFromConfig();
            }
            else
            {
                Debug.LogWarning($"{LogTag} 未找到保存的配置，使用默认值");
                LoadDefaults();
            }

            Debug.Log($"{LogTag} 配置加载完成");
        }

        private static void LoadFromConfig()
        {
            NormalEliteChance = ModSettingAPI.GetSavedValue<float>("NormalEliteChance", out float normal)
                ? normal
                : 1.0f;
            BossEliteChance = ModSettingAPI.GetSavedValue<float>("BossEliteChance", out float boss) ? boss : 0.4f;
            MerchantEliteChance = ModSettingAPI.GetSavedValue<float>("MerchantEliteChance", out float merchant)
                ? merchant
                : 0.0f;
            MaxAffixCount = ModSettingAPI.GetSavedValue<int>("MaxAffixCount", out int maxCount)
                ? Mathf.Clamp(maxCount, ConfigRanges.MinAffixCountLimit, ConfigRanges.MaxAffixCountLimit)
                : 2;

            DropRateMultiplier = ModSettingAPI.GetSavedValue<float>("DropRateMultiplier", out float dropRate)
                ? Mathf.Clamp(dropRate, ConfigRanges.MinDropRate, ConfigRanges.MaxDropRate)
                : 0.7f;
            ItemQualityBias = ModSettingAPI.GetSavedValue<float>("ItemQualityBias", out float qualityBias)
                ? Mathf.Clamp(qualityBias, ConfigRanges.MinQualityBias, ConfigRanges.MaxQualityBias)
                : -1.5f;
            EnableBonusLoot = ModSettingAPI.GetSavedValue<bool>("EnableBonusLoot", out bool enableBonus)
                ? enableBonus
                : true;

            GlobalHealthMultiplier = ModSettingAPI.GetSavedValue<float>("GlobalHealthMultiplier", out float healthMult)
                ? Mathf.Clamp(healthMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier)
                : 1.0f;
            GlobalDamageMultiplier = ModSettingAPI.GetSavedValue<float>("GlobalDamageMultiplier", out float damageMult)
                ? Mathf.Clamp(damageMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier)
                : 1.0f;
            GlobalSpeedMultiplier = ModSettingAPI.GetSavedValue<float>("GlobalSpeedMultiplier", out float speedMult)
                ? Mathf.Clamp(speedMult, ConfigRanges.MinMultiplier, ConfigRanges.MaxMultiplier)
                : 1.0f;

            ShowEliteName = ModSettingAPI.GetSavedValue<bool>("ShowEliteName", out bool showName) ? showName : true;
            ShowDetailedHealth = ModSettingAPI.GetSavedValue<bool>("ShowDetailedHealth", out bool showHealth)
                ? showHealth
                : true;
            if (ModSettingAPI.GetSavedValue<string>("AffixDisplayPosition", out string adp))
            {
                if (System.Enum.TryParse<AffixTextDisplayPosition>(adp, out var position))
                {
                    AffixDisplayPosition = position;
                }
                else
                {
                    AffixDisplayPosition = AffixTextDisplayPosition.Overhead;
                }
            }
            else
            {
                AffixDisplayPosition = AffixTextDisplayPosition.Overhead;
            }
            AffixFontSize = ModSettingAPI.GetSavedValue<int>("AffixFontSize", out int fontSize)
                ? Mathf.Clamp(fontSize, 20, 50)
                : 20;
            
            AffixWeight1 = ModSettingAPI.GetSavedValue<int>("AffixWeight1", out int w1)
                ? Mathf.Clamp(w1, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight)
                : 50;
            AffixWeight2 = ModSettingAPI.GetSavedValue<int>("AffixWeight2", out int w2)
                ? Mathf.Clamp(w2, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight)
                : 30;
            AffixWeight3 = ModSettingAPI.GetSavedValue<int>("AffixWeight3", out int w3)
                ? Mathf.Clamp(w3, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight)
                : 15;
            AffixWeight4 = ModSettingAPI.GetSavedValue<int>("AffixWeight4", out int w4)
                ? Mathf.Clamp(w4, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight)
                : 4;
            AffixWeight5 = ModSettingAPI.GetSavedValue<int>("AffixWeight5", out int w5)
                ? Mathf.Clamp(w5, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight)
                : 1;
            // 添加新配置项需要修改！！！

            // Debug.Log($"{LogTag} NormalEliteChance: {NormalEliteChance}");
            // Debug.Log($"{LogTag} DropRateMultiplier: {DropRateMultiplier}");
            // Debug.Log($"{LogTag} ItemQualityBias: {ItemQualityBias}");
            // Debug.Log($"{LogTag} EnableBonusLoot: {EnableBonusLoot}");

            LoadAffixStates();
            SyncConfigToComponents(); // 同步到掉落系统
        }

        private static void LoadDefaults()
        {
            NormalEliteChance = 1.0f;
            BossEliteChance = 0.4f;
            MerchantEliteChance = 0.0f;
            MaxAffixCount = 2;

            DropRateMultiplier = 1.0f;
            ItemQualityBias = -1.0f;
            EnableBonusLoot = true;

            GlobalHealthMultiplier = 1.0f;
            GlobalDamageMultiplier = 1.0f;
            GlobalSpeedMultiplier = 1.0f;

            ShowEliteName = true;
            ShowDetailedHealth = true;
            AffixDisplayPosition = AffixTextDisplayPosition.Overhead;
            AffixFontSize = 20;

            AffixWeight1 = 50;
            AffixWeight2 = 30;
            AffixWeight3 = 15;
            AffixWeight4 = 4;
            AffixWeight5 = 1;

            LoadAffixStates();
            SyncConfigToComponents();
        }

        private static void LoadAffixStates()
        {
            foreach (var kvp in EliteAffixes.Pool)
            {
                string key = kvp.Key;
                bool defaultState = !DefaultDisabledAffixes.Contains(key);

                if (ModSettingAPI.GetSavedValue<bool>(key, out bool saved))
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
            float oldValue = DropRateMultiplier;
            DropRateMultiplier = Mathf.Clamp(value, ConfigRanges.MinDropRate, ConfigRanges.MaxDropRate);
            EliteLootSystem.GlobalDropRate = DropRateMultiplier;
            //Debug.Log($"{LogTag} SetDropRateMultiplier: {oldValue} -> {DropRateMultiplier}");
            NotifyConfigChanged();
        }

        public static void SetItemQualityBias(float value)
        {
            float oldValue = ItemQualityBias;
            ItemQualityBias = Mathf.Clamp(value, ConfigRanges.MinQualityBias, ConfigRanges.MaxQualityBias);

            if (ModBehaviour.LootHelper != null)
            {
                ModBehaviour.LootHelper.qualityBiasPower = ItemQualityBias;
            }

            //Debug.Log($"{LogTag} SetItemQualityBias: {oldValue} -> {ItemQualityBias}");
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

        public static void SetShowEliteName(bool value)
        {
            ShowEliteName = value;
            NotifyConfigChanged();
        }

        public static void SetAffixDisplayPosition(string value)
        {
            if (System.Enum.TryParse<AffixTextDisplayPosition>(value, out var position))
            {
                AffixDisplayPosition = position;
                NotifyConfigChanged();
            }
        }
        public static void SetAffixFontSize(int value)
        {
            AffixFontSize = Mathf.Clamp(value, 20, 50);
            NotifyConfigChanged();
        }

        public static void SetAffixWeight1(int value)
        {
            AffixWeight1 = Mathf.Clamp(value, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight);
            NotifyConfigChanged();
        }

        public static void SetAffixWeight2(int value)
        {
            AffixWeight2 = Mathf.Clamp(value, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight);
            NotifyConfigChanged();
        }

        public static void SetAffixWeight3(int value)
        {
            AffixWeight3 = Mathf.Clamp(value, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight);
            NotifyConfigChanged();
        }

        public static void SetAffixWeight4(int value)
        {
            AffixWeight4 = Mathf.Clamp(value, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight);
            NotifyConfigChanged();
        }

        public static void SetAffixWeight5(int value)
        {
            AffixWeight5 = Mathf.Clamp(value, ConfigRanges.MinAffixWeight, ConfigRanges.MaxAffixWeight);
            NotifyConfigChanged();
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

                DropRateMultiplier = DropRateMultiplier,
                ItemQualityBias = ItemQualityBias,
                EnableBonusLoot = EnableBonusLoot,
                GlobalHealthMultiplier = GlobalHealthMultiplier,
                GlobalDamageMultiplier = GlobalDamageMultiplier,
                GlobalSpeedMultiplier = GlobalSpeedMultiplier,

                ShowEliteName = ShowEliteName,
                ShowDetailedHealth = ShowDetailedHealth,
                AffixDisplayPosition = AffixDisplayPosition,
                AffixFontSize = AffixFontSize,

                DisabledAffixes = GetDisabledAffixBlacklist(),
                AffixCountWeights = new int[]
                    { 0, AffixWeight1, AffixWeight2, AffixWeight3, AffixWeight4, AffixWeight5 }
            };
        }

        // 同步配置到运行时组件
        private static void SyncConfigToComponents()
        {
            EliteLootSystem.GlobalDropRate = DropRateMultiplier;
            if (ModBehaviour.LootHelper != null)
            {
                ModBehaviour.LootHelper.qualityBiasPower = ItemQualityBias;
            }
        }

        // 同步设置项更新到核心
        private static void NotifyConfigChanged()
        {
            EliteEnemyCore.UpdateConfig(GetConfig());
        }
    }
}