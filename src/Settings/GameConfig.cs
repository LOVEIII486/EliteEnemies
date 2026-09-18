using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Combos;
using EliteEnemies.Core;
using EliteEnemies.Loot;
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
            public const int MinQualityTier = 0;
            public const int MaxQualityTier = 6;
            public const float MinMultiplier = 1f;
            public const float MaxMultiplier = 10f;
            public const int MinAffixCountLimit = 1;
            public const int MaxAffixCountLimit = 5;
            public const int MinAffixWeight = 0;
            public const int MaxAffixWeight = 100;
            
            public const float MinVerticalOffset = -200f;
            public const float MaxVerticalOffset = 200f;
        }

        /// <summary>
        /// 掉落倍率的默认值。**全工程只有这一处**。
        ///
        /// <para>它曾经在三个地方各写了一份、且值还不一样：
        /// <c>LoadFromConfig</c> 的兜底是 <c>0.7f</c>、<c>LoadDefaults</c> 是 <c>1.0f</c>、
        /// <c>EliteEnemiesConfig</c> 的 DTO 是 <c>0.7f</c>——而设置项描述当时写的是「默认 1.0」。
        /// 三份副本必然漂移，所以收敛成常量。</para>
        ///
        /// <para>取 1.0 是因为它是**恒等元**：倍率 1.0 即「精英掉落就是词条表里逐条设计的那样」，
        /// 任何别的值都会在玩家不知情的情况下覆盖词条表里的逐条调校。</para>
        /// </summary>
        internal const float DefaultDropRateMultiplier = 1.0f;
        
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

        /// <summary>
        /// 品阶分布档位，**0–6 的整数**。<see cref="ItemQualityBias"/> 由它派生。
        ///
        /// <para><b>为什么不再直接暴露那个浮点指数</b>：它是个幂律指数，
        /// 玩家看到「−1.5」无法知道会产生什么分布（实测 −1.5 = 平均品阶 2.66、
        /// 1–3 品阶占 72%）。档位与结果成线性关系，可以直接写进描述里：
        /// <c>平均品阶 = 2.0 + 0.5 × 档位</c>。</para>
        /// </summary>
        public static int ItemQualityTier { get; private set; }

        /// <summary>
        /// 档位 → 内部幂律指数（喂给 <c>LootItemHelper.PickQualityByWeight</c>）。
        ///
        /// <para>这 7 个值是**反解出来的**：让每个档位对应的「平均品阶」恰好等距
        /// （2.0 / 2.5 / 3.0 / 3.5 / 4.0 / 4.5 / 5.0，公差 0.5），
        /// 而不是沿用旧的 −3/−2/−1.5/… 那组（它的效果间隔很不均匀——
        /// 从 −1 到 0 平均品阶一下从 3.0 跳到 4.0，而 0 到 +1 几乎不动）。
        /// 等距才能让「档位」这个整数对玩家有意义。</para>
        ///
        /// <para>档位 4（bias = 0）是**各品阶均等**，正好落在正中，也符合直觉。</para>
        /// </summary>
        private static readonly float[] QualityTierBiases = { -3.13f, -1.79f, -1.00f, -0.44f, 0f, 0.44f, 1.00f };

        /// <summary>档位对应的内部指数。夹取到合法档位，越界不会抛。</summary>
        public static float BiasForTier(int tier)
            => QualityTierBiases[Mathf.Clamp(tier, ConfigRanges.MinQualityTier, ConfigRanges.MaxQualityTier)];

        /// <summary>旧配置迁移用：把一个旧的 bias 值映射到最接近的档位。</summary>
        public static int NearestTierForBias(float bias)
        {
            int best = QualityTierBiases.Length / 2;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < QualityTierBiases.Length; i++)
            {
                float distance = Mathf.Abs(QualityTierBiases[i] - bias);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 当前档位对应的内部指数。**派生值，不再单独配置**——
        /// 消费方（<c>LootItemHelper.qualityBiasPower</c>）无需改动。
        /// </summary>
        public static float ItemQualityBias => BiasForTier(ItemQualityTier);
        public static float GlobalHealthMultiplier { get; private set; }
        public static float GlobalDamageMultiplier { get; private set; }
        public static float GlobalSpeedMultiplier { get; private set; }
        public static bool EnableBonusLoot { get; private set; }

        public static bool ShowEliteName { get; private set; }
        public static bool ShowDetailedHealth { get; private set; }
        public static AffixTextDisplayPosition AffixDisplayPosition { get; private set; } = AffixTextDisplayPosition.Overhead;
        public static int AffixFontSize { get; private set; }
        public static float AffixVerticalOffset { get; private set; }

        // 词条数量权重
        public static int AffixWeight1 { get; private set; }
        public static int AffixWeight2 { get; private set; }
        public static int AffixWeight3 { get; private set; }
        public static int AffixWeight4 { get; private set; }
        public static int AffixWeight5 { get; private set; }
        
        public static int SplitAffixMaxCloneCount { get; private set; }
        public static float SplitAffixMinFPSThreshold { get; private set; }
        
        // combo 系统
        public static bool EnableComboSystem { get; private set; }
        public static float ComboSystemChance { get; private set; }
        private static Dictionary<string, bool> _comboStates = new Dictionary<string, bool>();

        // ==================== 设置项声明 ====================
        //
        // 每一条设置**只在这里声明一次**：键、默认值、取值范围、所属分组、描述键。
        // 读取、默认值、夹取、控件注册、分组归属**全部由这张表派生**。
        //
        // 此前新增一条设置要在 11–12 处同步改动，且键字符串写 3 遍、默认值写 2 遍——
        // 而副本必然漂移（本工程实测漂移过一次：倍率默认值 0.7 与 1.0 并存）。
        // 收敛之后新增设置 = 加一条声明 + 四个 CSV 各一行。详见 SettingSpec.cs。

        /// <summary>旧键。**只用于一次性迁移**，不再注册对应控件。</summary>
        internal const string LegacyQualityBiasKey = "ItemQualityBias";

        /// <summary>默认档位。取 1 是因为它 ≈ 旧版默认值 −1.5（旧默认映射到最近档位的结果）。</summary>
        internal const int DefaultQualityTier = 1;

        /// <summary>品质档位那一条。它需要旧配置迁移，且要单独触发写回，所以留个具名引用。</summary>
        internal static readonly QualityTierSetting QualityTier =
            new QualityTierSetting("ItemQualityTier", SettingGroup.Basic,
                                   "EliteEnemies_Settings_ItemQualityTier",
                                   def: DefaultQualityTier,
                                   min: ConfigRanges.MinQualityTier, max: ConfigRanges.MaxQualityTier,
                                   store: v => ItemQualityTier = v);

        /// <summary>全部静态设置项。顺序 = 界面注册顺序。</summary>
        internal static readonly List<SettingSpec> Specs = new List<SettingSpec>
        {
            // ── 基础 ──
            new FloatSetting("NormalEliteChance", SettingGroup.Basic,
                "EliteEnemies_Settings_NormalEliteChance",
                def: 1.0f, min: 0f, max: 1f, decimalPlaces: 2, characterLimit: 5,
                store: v => NormalEliteChance = v),
            new FloatSetting("BossEliteChance", SettingGroup.Basic,
                "EliteEnemies_Settings_BossEliteChance",
                def: 0.4f, min: 0f, max: 1f, decimalPlaces: 2, characterLimit: 5,
                store: v => BossEliteChance = v),
            new FloatSetting("MerchantEliteChance", SettingGroup.Basic,
                "EliteEnemies_Settings_MerchantEliteChance",
                def: 0.0f, min: 0f, max: 1f, decimalPlaces: 2, characterLimit: 5,
                store: v => MerchantEliteChance = v),
            new IntSetting("MaxAffixCount", SettingGroup.Basic,
                "EliteEnemies_Settings_MaxAffixCount",
                def: 2, min: ConfigRanges.MinAffixCountLimit, max: ConfigRanges.MaxAffixCountLimit,
                store: v => MaxAffixCount = v),
            new FloatSetting("DropRateMultiplier", SettingGroup.Basic,
                "EliteEnemies_Settings_DropRateMultiplier",
                def: DefaultDropRateMultiplier, min: ConfigRanges.MinDropRate, max: ConfigRanges.MaxDropRate,
                decimalPlaces: 2, characterLimit: 5,
                store: v => DropRateMultiplier = v),
            QualityTier,
            new BoolSetting("EnableBonusLoot", SettingGroup.Basic,
                "EliteEnemies_Settings_EnableBonusLoot",
                def: true, store: v => EnableBonusLoot = v),

            // ── 视觉 ──（AffixDisplayPosition 是字符串下拉，单独注册，见 SettingsUIRegistration）
            new BoolSetting("ShowEliteName", SettingGroup.Visual,
                "EliteEnemies_Settings_ShowEliteName",
                def: true, store: v => ShowEliteName = v),
            new BoolSetting("ShowDetailedHealth", SettingGroup.Visual,
                "EliteEnemies_Settings_ShowDetailedHealth",
                def: false, store: v => ShowDetailedHealth = v),
            // 选项名是**枚举名**（语言无关），不是本地化文案——见 EnumSetting 的注释。
            new EnumSetting<AffixTextDisplayPosition>("AffixDisplayPosition", SettingGroup.Visual,
                "EliteEnemies_Settings_AffixDisplayPosition",
                def: AffixTextDisplayPosition.Overhead,
                options: new List<string> { "Overhead", "Underfoot" },
                store: v => AffixDisplayPosition = v),
            new FloatSetting("AffixVerticalOffset", SettingGroup.Visual,
                "EliteEnemies_Settings_AffixVerticalOffset",
                def: 0f, min: ConfigRanges.MinVerticalOffset, max: ConfigRanges.MaxVerticalOffset,
                decimalPlaces: 0, characterLimit: 4,
                store: v => AffixVerticalOffset = v),
            // 范围统一为 20..40。此前 UI 上限 40 而夹取上限 50，两个数不一致——
            // UI 到不了 50，所以没人会存到 41..50 的值，收紧是安全的。
            new IntSetting("AffixFontSize", SettingGroup.Visual,
                "EliteEnemies_Settings_AffixFontSize",
                def: 20, min: 20, max: 40, store: v => AffixFontSize = v),

            // ── 全局属性调整 ──
            new FloatSetting("GlobalHealthMultiplier", SettingGroup.GlobalMultipliers,
                "EliteEnemies_Settings_GlobalHealthMultiplier",
                def: 1.0f, min: ConfigRanges.MinMultiplier, max: ConfigRanges.MaxMultiplier,
                decimalPlaces: 1, characterLimit: 5,
                store: v => GlobalHealthMultiplier = v),
            new FloatSetting("GlobalDamageMultiplier", SettingGroup.GlobalMultipliers,
                "EliteEnemies_Settings_GlobalDamageMultiplier",
                def: 1.0f, min: ConfigRanges.MinMultiplier, max: ConfigRanges.MaxMultiplier,
                decimalPlaces: 1, characterLimit: 5,
                store: v => GlobalDamageMultiplier = v),
            new FloatSetting("GlobalSpeedMultiplier", SettingGroup.GlobalMultipliers,
                "EliteEnemies_Settings_GlobalSpeedMultiplier",
                def: 1.0f, min: ConfigRanges.MinMultiplier, max: ConfigRanges.MaxMultiplier,
                decimalPlaces: 1, characterLimit: 5,
                store: v => GlobalSpeedMultiplier = v),

            // ── 词条数量权重 ──
            new IntSetting("AffixWeight1", SettingGroup.AffixWeights, "EliteEnemies_Settings_AffixWeight1",
                def: 50, min: ConfigRanges.MinAffixWeight, max: ConfigRanges.MaxAffixWeight,
                store: v => AffixWeight1 = v),
            new IntSetting("AffixWeight2", SettingGroup.AffixWeights, "EliteEnemies_Settings_AffixWeight2",
                def: 30, min: ConfigRanges.MinAffixWeight, max: ConfigRanges.MaxAffixWeight,
                store: v => AffixWeight2 = v),
            new IntSetting("AffixWeight3", SettingGroup.AffixWeights, "EliteEnemies_Settings_AffixWeight3",
                def: 15, min: ConfigRanges.MinAffixWeight, max: ConfigRanges.MaxAffixWeight,
                store: v => AffixWeight3 = v),
            new IntSetting("AffixWeight4", SettingGroup.AffixWeights, "EliteEnemies_Settings_AffixWeight4",
                def: 4, min: ConfigRanges.MinAffixWeight, max: ConfigRanges.MaxAffixWeight,
                store: v => AffixWeight4 = v),
            new IntSetting("AffixWeight5", SettingGroup.AffixWeights, "EliteEnemies_Settings_AffixWeight5",
                def: 1, min: ConfigRanges.MinAffixWeight, max: ConfigRanges.MaxAffixWeight,
                store: v => AffixWeight5 = v),

            // ── 词条特殊设置 ──
            new IntSetting("SplitAffixMaxCloneCount", SettingGroup.AffixSpecial,
                "EliteEnemies_Settings_SplitAffixMaxCloneCount",
                def: 40, min: 10, max: 100, store: v => SplitAffixMaxCloneCount = v),
            new FloatSetting("SplitAffixMinFPSThreshold", SettingGroup.AffixSpecial,
                "EliteEnemies_Settings_SplitAffixMinFPSThreshold",
                def: 30f, min: 10f, max: 60f, decimalPlaces: 0, characterLimit: 5,
                store: v => SplitAffixMinFPSThreshold = v),

            // ── combo 系统 ──
            new BoolSetting("EnableComboSystem", SettingGroup.Combo,
                "EliteEnemies_Settings_EnableComboSystem",
                def: false, store: v => EnableComboSystem = v),
            // 默认 0.15：`LoadFromConfig` 与 `LoadDefaults` 两处都是 0.15f，
            // 只有 DTO（EliteEnemiesConfig）写着 0.2f——**这是第二处默认值漂移**，
            // 与倍率那次同类。以 GameConfig 为准（两处一致对一处孤立）。
            new FloatSetting("ComboSystemChance", SettingGroup.Combo,
                "EliteEnemies_Settings_ComboSystemChance",
                def: 0.15f, min: 0f, max: 1f, decimalPlaces: 2, characterLimit: 5,
                store: v => ComboSystemChance = v),
        };

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
            // 全部静态设置项由规格表派生——**键、默认值、夹取范围只写在 Specs 里那一遍**。
            foreach (var spec in Specs) spec.Load();

            LoadAffixStates();
            LoadComboStates();
            SyncConfigToComponents(); // 同步到掉落系统
        }

        private static void LoadDefaults()
        {
            // 恢复默认值同样由规格表派生——**默认值只写在 Specs 里那一遍**。
            // 此前它和 LoadFromConfig 的兜底各写一份，实际漂移过两次
            //（倍率 0.7/1.0、ComboSystemChance 0.15/0.2）。
            foreach (var spec in Specs) spec.ResetToDefault();

            LoadAffixStates();
            LoadComboStates();
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

        public static void SetAffixDisplayPosition(string value)
        {
            if (System.Enum.TryParse<AffixTextDisplayPosition>(value, out var position))
            {
                AffixDisplayPosition = position;
                NotifyChanged();
            }
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
            NotifyChanged();
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
        
        
        // ==================== Combo 状态管理 ====================

        private static void LoadComboStates()
        {
            foreach (var combo in EliteComboRegistry.ComboPool)
            {
                if (ModSettingAPI.GetSavedValue<bool>(combo.ComboId, out bool saved))
                    _comboStates[combo.ComboId] = saved;
                else
                    _comboStates[combo.ComboId] = true;   // 存档里没有该键 ⇒ 默认**启用**
            }
        }

        public static void SetComboEnabled(string comboId, bool enabled)
        {
            _comboStates[comboId] = enabled;
            NotifyChanged();
        }

        /// <summary>
        /// 该组合当前是否启用。
        ///
        /// <para>⚠ <b>miss 返回 <c>false</c> 是刻意的，不是与 <see cref="LoadComboStates"/> 的默认值打架</b>
        /// ——两者回答的是**不同的问题**：</para>
        /// <list type="bullet">
        /// <item><c>LoadComboStates</c> 判的是"**存档里没有这个键**"⇒ 用作者定的默认值（启用）；</item>
        /// <item>这里判的是"<c>_comboStates</c> 里根本没有这一项"⇒ 说明**加载从没跑过**
        /// （`ModSettingAPI.Init` 失败的早退路径，见 <c>设置状况调查报告</c>），
        /// 此时**一律当关闭**（fail-closed）。同一条路径下 <c>EnableComboSystem</c> 也停在
        /// DTO 默认的 <c>false</c>，所以整体行为是自洽的。</item>
        /// </list>
        ///
        /// <para>写成显式注释而不是共用一个常量：两者**语义不同**，共用反而会让人以为
        /// 它们是同一个默认值。若将来把 miss 分支改成"也用默认值"，那就是**改行为**
        /// （未初始化时会突然冒出组合），要单独决定。</para>
        /// </summary>
        public static bool IsComboEnabled(string comboId)
        {
            return _comboStates.TryGetValue(comboId, out bool enabled) && enabled;
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
                AffixVerticalOffset = AffixVerticalOffset,

                DisabledAffixes = GetDisabledAffixBlacklist(),
                AffixCountWeights = new int[]
                    { 0, AffixWeight1, AffixWeight2, AffixWeight3, AffixWeight4, AffixWeight5 },
                
                SplitAffixMaxCloneCount = SplitAffixMaxCloneCount,
                SplitAffixMinFPSThreshold = SplitAffixMinFPSThreshold,
                
                EnableComboSystem = EnableComboSystem, 
                ComboSystemChance = ComboSystemChance,
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
        /// <summary>设置变更后把快照推给核心。规格表与遗留的 Setter 都调它。</summary>
        internal static void NotifyChanged()
        {
            EliteEnemyCore.UpdateConfig(GetConfig());
        }
    }
}