using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Combos;
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

            // 静态设置项**全部由规格表派生**——键、默认值、取值范围、描述、控件类型
            // 都只写在 GameConfig.Specs 里那一遍。见 SettingSpec.cs 的类注释。
            foreach (var spec in GameConfig.Specs) spec.RegisterUI();

            // 动态部分：条目由数据（词条表 / combo 表）生成，形态与静态设置不同，
            // 所以仍是手写注册 + 把键交给分组。
            var affixKeys = RegisterAffixToggles();
            var comboKeys = RegisterComboToggles();

            // 必须最后调用
            RegisterGroups(affixKeys, comboKeys);
            Debug.Log($"{LogTag} UI 注册完成");

            // 旧配置迁移的收尾：**必须在控件注册之后**——ModSetting 的 SetValue 要落到
            // 已注册的控件上（ModSetting/ModBehaviour.cs:236-242），键没注册时无处可写。
            // 写失败也不影响正确性，见 QualityTierSetting.WriteBackIfMigrated。
            GameConfig.QualityTier.WriteBackIfMigrated();
        }

        /// <summary>
        /// 重建设置界面。**换语言时用**——这是 ModSetting 的**唯一特殊处理**。
        ///
        /// <para><b>为什么它需要特殊处理</b>：ModSetting 把 <c>description</c> 当**纯文本**用，
        /// 直接 <c>label.text = description;</c>（`ModSetting/UI/ToggleUI.cs:31` 等七处），
        /// **从不解析为 key**。所以"文本注册进游戏本地化器"这条统一路径对它无效——
        /// 它拿到什么就永远显示什么。而它换语言时也只刷新自己的几个字符串
        /// （`ToggleUI.OnLanguageChanged` 只重取 ENABLE/DISABLE、`PanelUI` 只重取页签名），
        /// **不会替模组重取文案**。</para>
        ///
        /// <para>而 ModSetting 自己正是这么做的——<c>Setting.cs:125-128</c>：
        /// <c>builder.Clear(); AddUI();</c>。我们照它的做法。</para>
        ///
        /// <para>⚠ <b>不会丢玩家保存的值</b>：<c>Clear()</c> 走
        /// <c>ModBehaviour.Clear</c> → <c>ModConfig.Clear</c> → <c>activeKey.Clear()</c>
        /// ——只清"界面当前激活的键"这个集合（`ModSetting/Config/ModConfig.cs:75-78`），
        /// 玩家保存的值在 <c>Saver</c> 里，这条路径完全不碰。
        /// 而且重新注册时传的 <c>defaultValue</c> 是**当前配置值**（如
        /// <c>GameConfig.NormalEliteChance</c>）而不是硬编码默认值，所以界面读到的仍是玩家的值。</para>
        /// </summary>
        public static void Reregister()
        {
            if (!ModSettingAPI.IsInit) return;

            ModSettingAPI.Clear();
            RegisterUI();
            Debug.Log($"{LogTag} 已因语言变化重建设置界面");
        }

        // 词缀开关
        private static List<string> RegisterAffixToggles()
        {
            var affixKeys = new List<string>();

            foreach (var kvp in EliteAffixes.Pool)
            {
                string affixKey = kvp.Key;
                string description = $"{kvp.Value.ColoredTag} : {kvp.Value.Description.Value}";

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

        // combo开关
        //
        // 只处理**动态**的那部分：每个 combo 一个开关。`EnableComboSystem` /
        // `ComboSystemChance` 是普通静态设置，已并入 GameConfig.Specs——
        // 它们**不再在这里注册**，分组时由 KeysOf(SettingGroup.Combo) 自动带上。
        private static List<string> RegisterComboToggles()
        {
            var comboKeys = new List<string>();

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
        //
        // ⚠ 静态分组的键列表**从规格表按归属取**，不再手抄。
        // 此前同一份键名在三个地方各写一遍（存档读取 / 控件注册 / 这里的列表），
        // 漏改一处的后果是**静默的**：控件注册了但界面不显示。
        // 见 SettingSpec.cs 的类注释。
        private static void RegisterGroups(List<string> affixKeys, List<string> comboKeys)
        {
            AddStaticGroup("BasicSettings", "EliteEnemies_Settings_BasicSettings_Group",
                           SettingGroup.Basic, open: true);
            AddStaticGroup("VisualSettings", "EliteEnemies_Settings_VisualSettings_Group",
                           SettingGroup.Visual, open: false);
            AddStaticGroup("GlobalMultipliers", "EliteEnemies_Settings_GlobalMultipliers_Group",
                           SettingGroup.GlobalMultipliers, open: false);
            AddStaticGroup("AffixCountWeights", "EliteEnemies_Settings_AffixCountWeights_Group",
                           SettingGroup.AffixWeights, open: false);
            AddGroup("AffixToggles", "EliteEnemies_Settings_AffixToggles_Group", affixKeys, open: false);

            // Combo 组 = 静态的（启用开关 + 触发概率）+ 每个 combo 一个开关
            var comboGroupKeys = KeysOf(SettingGroup.Combo);
            comboGroupKeys.AddRange(comboKeys);
            AddGroup("ComboSettings", "EliteEnemies_Settings_ComboSettings_Group", comboGroupKeys, open: false);

            AddStaticGroup("AffixSpecialSettings", "EliteEnemies_Settings_AffixSpecialSettings_Group",
                           SettingGroup.AffixSpecial, open: false);
        }

        /// <summary>按归属取该组的全部键，顺序 = 规格表顺序。</summary>
        private static List<string> KeysOf(SettingGroup group)
        {
            var keys = new List<string>();
            foreach (var spec in GameConfig.Specs)
            {
                if (spec.Group == group) keys.Add(spec.Key);
            }
            return keys;
        }

        private static void AddStaticGroup(string key, string descKey, SettingGroup group, bool open)
            => AddGroup(key, descKey, KeysOf(group), open);

        private static void AddGroup(string key, string descKey, List<string> keys, bool open)
            => ModSettingAPI.AddGroup(
                key: key,
                description: LocalizationManager.GetText(descKey),
                keys: keys,
                scale: GroupScale,
                topInsert: GroupTopInsert,
                open: open);
    }
}