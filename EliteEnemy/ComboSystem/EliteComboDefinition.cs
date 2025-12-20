using System.Collections.Generic;
using System.Text;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.Localization;
using System;

namespace EliteEnemies.EliteEnemy.ComboSystem
{
    public class EliteComboDefinition
    {
        public string ComboId { get; set; }           
        public string DisplayName { get; set; }
        public List<string> AffixIds { get; set; }    
        public float Weight { get; set; }              
        public string CustomColorHex { get; set; }     
        
        // 白名单集合：存储允许生成的敌人预设名
        public HashSet<string> AllowedPresets { get; set; } 

        public EliteComboDefinition(string id, string name, List<string> affixes, float weight = 1f, string colorHex = "FFD700") 
        {
            ComboId = id;
            DisplayName = name;
            AffixIds = affixes;
            Weight = weight;
            CustomColorHex = colorHex.Replace("#", "");
            AllowedPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // 链式调用：添加白名单
        public EliteComboDefinition WithWhitelist(params string[] presetNames)
        {
            if (presetNames != null)
            {
                foreach (var name in presetNames) AllowedPresets.Add(name);
            }
            return this;
        }
        
        // 获取本地化且带颜色的标题（用于 UI 和 血条）
        public string GetColoredTitle()
        {
            string localizedName = LocalizationManager.GetText(ComboId, DisplayName);
            return $"<color=#{CustomColorHex}>【{localizedName}】</color>";
        }

        // 获取格式化描述（用于设置菜单）
        public string GetFormattedDescription()
        {
            StringBuilder sb = new StringBuilder();
            if (AffixIds != null)
            {
                foreach (string aid in AffixIds)
                {
                    if (EliteAffixes.TryGetAffix(aid, out var affixData))
                        sb.Append(affixData.ColoredTag);
                    else
                        sb.Append($"[{aid}]");
                }
            }
            return $"{GetColoredTitle()}：{sb}";
        }
    }
}