using System.Collections.Generic;
using System.Text;
using EliteEnemies.Affixes;
using EliteEnemies.Localization;
using System;

namespace EliteEnemies.Combos
{
    public class EliteComboDefinition
    {
        public string ComboId { get; set; }
        public List<string> AffixIds { get; set; }
        public float Weight { get; set; }
        public string CustomColorHex { get; set; }
        public HashSet<string> AllowedPresets { get; set; }

        /// <summary>
        /// combo 的显示名。**存键，不存译文**——随语言切换，读的时候是
        /// <see cref="LocalizedText.Value"/> 的一次版本比对。
        ///
        /// <para>⚠ <b>不能加 <c>readonly</c></b>：<see cref="LocalizedText"/> 是**可变 struct**
        /// （内部有 <c>_cached</c> / <c>_cachedVersion</c>），而 <c>readonly</c> 的 struct 字段
        /// **每次访问成员都会先复制一份** ⇒ 缓存的写回落在副本上、当场丢弃，
        /// 那句"一次版本比对"**永远不成立**（每次都在重查）。而且它**是静默的**：
        /// 不报错、不抛异常，只是白做缓存。</para>
        ///
        /// <para>（对照：`AffixData` 里同类字段就是非 readonly 的，它的缓存是真的生效的。）</para>
        /// </summary>
        private LocalizedText _displayName;

        // GetColoredTitle 的版本化缓存。见该方法注释：这个字符串会被**每帧**读到
        // （血条对非精英敌人每帧查一次 CustomDisplayName），所以不能每次现拼。
        private string _titleCached;
        private int _titleCachedVersion;

        public string DisplayName => _displayName.Value;

        public EliteComboDefinition(string id, LocalizedText displayName, List<string> affixes, float weight = 1f, string colorHex = "FFD700")
        {
            ComboId = id;
            _displayName = displayName;
            AffixIds = affixes;
            Weight = weight;
            CustomColorHex = colorHex.Replace("#", "");
            AllowedPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public EliteComboDefinition WithWhitelist(params string[] presetNames)
        {
            if (presetNames != null)
                foreach (var name in presetNames) AllowedPresets.Add(name);
            return this;
        }
        
        /// <summary>
        /// 带色号的标题 <c>&lt;color=#RRGGBB&gt;【名字】&lt;/color&gt;</c>。
        ///
        /// <para><b>为什么带缓存，而不是每次现拼</b>：这个字符串经由
        /// <c>EliteMarker.CustomDisplayName</c> 被血条读到，而血条的
        /// <c>EliteHealthBarUI.LateUpdate</c> 里有一段**对非精英敌人每帧执行**的
        /// marker 判定（<c>EliteHealthBarUI.cs:77-85</c>）。现拼就等于给地图上每个
        /// 非精英敌人每帧分配一个字符串——那是实打实的卡顿来源。</para>
        ///
        /// <para>缓存按语言版本失效：稳态是一次 int 比较 + 一次字段读，零分配；
        /// 语言真变过之后的下一次读取才重拼一次。</para>
        /// </summary>
        public string GetColoredTitle()
        {
            int version = LocalizationManager.LanguageVersion;
            if (_titleCachedVersion != version)
            {
                _titleCached = $"<color=#{CustomColorHex}>【{DisplayName}】</color>";
                _titleCachedVersion = version;
            }
            return _titleCached;
        }

        public string GetFormattedDescription()
        {
            StringBuilder sb = new StringBuilder();
            if (AffixIds != null)
            {
                foreach (string aid in AffixIds)
                {
                    if (EliteAffixes.TryGetAffix(aid, out var affixData))
                        sb.Append(affixData.ColoredTag);
                }
            }
            return $"{GetColoredTitle()}：{sb}";
        }
    }
}