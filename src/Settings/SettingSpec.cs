using System;
using System.Collections.Generic;
using EliteEnemies.Localization;
using EliteEnemies.ModSettingsApi;
using UnityEngine;

namespace EliteEnemies.Settings
{
    /// <summary>设置项在界面里归属的分组。分组顺序 = 界面顺序。</summary>
    internal enum SettingGroup
    {
        Basic,
        Visual,
        GlobalMultipliers,
        AffixWeights,
        AffixSpecial,
        Combo,
    }

    /// <summary>
    /// 一条设置的**唯一声明**：键、默认值、取值范围、描述键、所属分组。
    ///
    /// <para><b>它要解决什么</b>：此前新增一条设置要在 11–12 处同步改动，而且同一份信息
    /// 存在多份副本——**键字符串写 3 遍**（存档读取 / 控件注册 / 分组列表）、
    /// **默认值写 2 遍**（<c>LoadFromConfig</c> 的兜底与 <c>LoadDefaults</c>）。
    /// 副本必然漂移：本工程实测漂移过一次（倍率默认值 0.7 与 1.0 并存，见
    /// <c>GameConfig.DefaultDropRateMultiplier</c> 的注释）。</para>
    ///
    /// <para>收敛之后，新增一条设置 = **加一条规格声明**（+ 四个 CSV 各一行），
    /// 读取、默认、夹取、控件注册、分组全部由这张表派生。</para>
    ///
    /// <para>⚠ <b>注册控件时传的 <c>defaultValue</c> 必须是「当前值」</b>，不能是硬编码默认——
    /// 这是「换语言重建界面时不丢玩家设置」的关键（见 <c>SettingsUIRegistration.Reregister</c>）。
    /// 规格里存的 <see cref="IntSetting.Value"/> 就是当前值，天然满足。</para>
    /// </summary>
    internal abstract class SettingSpec
    {
        public readonly string Key;
        public readonly SettingGroup Group;
        public readonly string DescKey;

        protected SettingSpec(string key, SettingGroup group, string descKey)
        {
            Key = key;
            Group = group;
            DescKey = descKey;
        }

        /// <summary>描述文本（已本地化）。ModSetting 把 description 当**纯文本**用，从不解析为 key。</summary>
        protected string Description => LocalizationManager.GetText(DescKey);

        /// <summary>从存档读；没有存档值就用默认值。**键与默认值只在这里出现一次。**</summary>
        public abstract void Load();

        /// <summary>全部恢复默认值（没有任何存档时走这条）。</summary>
        public abstract void ResetToDefault();

        /// <summary>往 ModSetting 注册控件。</summary>
        public abstract void RegisterUI();
    }

    /// <summary>整数滑块设置。（非 sealed：<see cref="QualityTierSetting"/> 需要覆盖读取逻辑）</summary>
    internal class IntSetting : SettingSpec
    {
        protected readonly int DefaultValue;
        private readonly int _min, _max;
        private readonly Action<int> _store;

        /// <summary>当前值。注册控件时作为 <c>defaultValue</c> 传出去。</summary>
        public int Value { get; private set; }

        public IntSetting(string key, SettingGroup group, string descKey,
                          int def, int min, int max, Action<int> store)
            : base(key, group, descKey)
        {
            DefaultValue = def;
            _min = min;
            _max = max;
            _store = store;
        }

        /// <summary>
        /// 从存档解析出该用哪个值。默认实现是「有存档就用存档，否则用默认」。
        /// 需要**旧配置迁移**的设置覆盖它（见 <c>QualityTierSetting</c>）。
        /// </summary>
        protected virtual int ReadSavedOrDefault()
            => ModSettingAPI.GetSavedValue<int>(Key, out int saved) ? saved : DefaultValue;

        private void Apply(int value, bool notify)
        {
            Value = Mathf.Clamp(value, _min, _max);
            _store(Value);
            if (notify) GameConfig.NotifyChanged();
        }

        public override void Load() => Apply(ReadSavedOrDefault(), notify: false);
        public override void ResetToDefault() => Apply(DefaultValue, notify: false);

        public override void RegisterUI() =>
            ModSettingAPI.AddSlider(
                key: Key,
                description: Description,
                defaultValue: Value,
                minValue: _min,
                maxValue: _max,
                onValueChange: Set);

        public void Set(int value) => Apply(value, notify: true);
    }

    /// <summary>浮点滑块设置。</summary>
    internal sealed class FloatSetting : SettingSpec
    {
        private readonly float DefaultValue;
        private readonly float _min, _max;
        private readonly int _decimalPlaces;
        private readonly int _characterLimit;
        private readonly Action<float> _store;

        public float Value { get; private set; }

        public FloatSetting(string key, SettingGroup group, string descKey,
                            float def, float min, float max,
                            int decimalPlaces, int characterLimit, Action<float> store)
            : base(key, group, descKey)
        {
            DefaultValue = def;
            _min = min;
            _max = max;
            _decimalPlaces = decimalPlaces;
            _characterLimit = characterLimit;
            _store = store;
        }

        private void Apply(float value, bool notify)
        {
            Value = Mathf.Clamp(value, _min, _max);
            _store(Value);
            if (notify) GameConfig.NotifyChanged();
        }

        public override void Load()
            => Apply(ModSettingAPI.GetSavedValue<float>(Key, out float saved) ? saved : DefaultValue,
                     notify: false);

        public override void ResetToDefault() => Apply(DefaultValue, notify: false);

        public override void RegisterUI() =>
            ModSettingAPI.AddSlider(
                key: Key,
                description: Description,
                defaultValue: Value,
                sliderRange: new Vector2(_min, _max),
                onValueChange: Set,
                decimalPlaces: _decimalPlaces,
                characterLimit: _characterLimit);

        public void Set(float value) => Apply(value, notify: true);
    }

    /// <summary>开关设置。</summary>
    internal sealed class BoolSetting : SettingSpec
    {
        private readonly bool DefaultValue;
        private readonly Action<bool> _store;

        public bool Value { get; private set; }

        public BoolSetting(string key, SettingGroup group, string descKey, bool def, Action<bool> store)
            : base(key, group, descKey)
        {
            DefaultValue = def;
            _store = store;
        }

        private void Apply(bool value, bool notify)
        {
            Value = value;
            _store(Value);
            if (notify) GameConfig.NotifyChanged();
        }

        public override void Load()
            => Apply(ModSettingAPI.GetSavedValue<bool>(Key, out bool saved) ? saved : DefaultValue,
                     notify: false);

        public override void ResetToDefault() => Apply(DefaultValue, notify: false);

        public override void RegisterUI() =>
            ModSettingAPI.AddToggle(
                key: Key,
                description: Description,
                enable: Value,
                onValueChange: Set);

        public void Set(bool value) => Apply(value, notify: true);
    }

    /// <summary>
    /// 枚举下拉设置。
    ///
    /// <para><b>存的是枚举名，不是本地化文案。</b> 这一点是刻意的——ModSetting 的下拉把
    /// 「选项字符串本身」当存档值存（<c>DropDownUI.cs:41</c>），所以选项一旦本地化，
    /// 玩家切语言后旧文案就匹配不上新选项列表，控件显示空白、**玩家的选择静默丢失**。
    /// 枚举名天然与语言无关，绕开了这个坑。</para>
    /// </summary>
    internal sealed class EnumSetting<T> : SettingSpec where T : struct, Enum
    {
        private readonly T DefaultValue;
        private readonly List<string> _options;
        private readonly Action<T> _store;

        public T Value { get; private set; }

        public EnumSetting(string key, SettingGroup group, string descKey,
                           T def, List<string> options, Action<T> store)
            : base(key, group, descKey)
        {
            DefaultValue = def;
            _options = options;
            _store = store;
        }

        private void Apply(T value, bool notify)
        {
            Value = value;
            _store(Value);
            if (notify) GameConfig.NotifyChanged();
        }

        public override void Load()
        {
            // 解析失败就回默认——旧存档里若有我们不认识的名字（或历史遗留的自由文本），
            // 结果与「没有存档」一致，不会留下一个非法值。
            T value = DefaultValue;
            T parsed = default;   // 先声明：短路求值时 TryParse 可能根本没跑
            if (ModSettingAPI.GetSavedValue<string>(Key, out string saved)
                && Enum.TryParse(saved, out parsed))
            {
                value = parsed;
            }
            Apply(value, notify: false);
        }

        public override void ResetToDefault() => Apply(DefaultValue, notify: false);

        public override void RegisterUI() =>
            ModSettingAPI.AddDropdownList(
                key: Key,
                description: Description,
                options: _options,
                defaultValue: Value.ToString(),
                onValueChange: Set);

        public void Set(string optionName)
        {
            if (Enum.TryParse(optionName, out T parsed)) Apply(parsed, notify: true);
        }
    }

    /// <summary>
    /// 品质档位。**唯一需要旧配置迁移的一条**，所以覆盖了 <see cref="IntSetting.ReadSavedOrDefault"/>。
    ///
    /// <para>三级取用，先到先用：新键 <c>ItemQualityTier</c> → 旧键 <c>ItemQualityBias</c>
    /// （映射到最接近的档位）→ 默认档。细节见 <c>掉落模块审查与设计.md §4.1</c>。</para>
    /// </summary>
    internal sealed class QualityTierSetting : IntSetting
    {
        public QualityTierSetting(string key, SettingGroup group, string descKey,
                                  int def, int min, int max, Action<int> store)
            : base(key, group, descKey, def, min, max, store) { }

        /// <summary>迁移得到的档位，待控件注册后写回新键；(小于 0 = 无需写回)。</summary>
        private int _pendingMigration = -1;

        protected override int ReadSavedOrDefault()
        {
            if (ModSettingAPI.GetSavedValue<int>(Key, out int tier)) return tier;

            if (ModSettingAPI.GetSavedValue<float>(GameConfig.LegacyQualityBiasKey, out float legacyBias))
            {
                int migrated = GameConfig.NearestTierForBias(legacyBias);
                _pendingMigration = migrated;
                Debug.Log($"[EliteEnemies.Settings] 旧配置迁移：品质偏好 {legacyBias:F2} → 档位 {migrated}" +
                          $"（平均品阶约 {2.0f + 0.5f * migrated:F1}）");
                return migrated;
            }

            return DefaultValue;
        }

        /// <summary>
        /// 把迁移得到的档位写回新键（一次性），此后启动走三级取用的第一条。
        ///
        /// <para><b>必须等控件注册之后调用</b>：ModSetting 的 <c>SetValue</c> 经 ConfigManager
        /// 落到具体控件上，键还没注册时无处可写。</para>
        ///
        /// <para>⚠ <b>正确性不依赖写回</b>：档位每次启动都从三级重新推导，是幂等的。
        /// 写回只是让旧键不再被读；它失败也只是每次多走一步推导，不会出错。</para>
        /// </summary>
        public void WriteBackIfMigrated()
        {
            if (_pendingMigration < 0) return;

            int tier = _pendingMigration;
            _pendingMigration = -1;

            ModSettingAPI.SetValue(Key, tier, ok =>
                Debug.Log(ok
                    ? $"[EliteEnemies.Settings] 品质档位 {tier} 已写回新键，旧键不再读取"
                    : $"[EliteEnemies.Settings] 品质档位写回失败——不影响使用，每次启动会从旧键重新推导"));
        }
    }
}
