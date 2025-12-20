using System.Collections.Generic;
using System.Text;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.ComboSystem
{
    public class EliteComboDefinition
    {
        public string ComboId { get; set; }           
        public string DisplayName { get; set; }        
        public List<string> AffixIds { get; set; }    
        public float Weight { get; set; }              
        public string CustomColorHex { get; set; }

        public EliteComboDefinition(string id, string name, List<string> affixes, float weight = 1f, string colorHex = "FFD700") 
        {
            ComboId = id;
            DisplayName = name;
            AffixIds = affixes;
            Weight = weight;
            CustomColorHex = colorHex.Replace("#", "");
        }
        
        /// <summary>
        /// 获取设置项中显示的格式化描述
        /// </summary>
        public string GetFormattedDescription()
        {
            return $"{GetColoredTitle()}：{GetColoredAffixList()}";
        }

        /// <summary>
        /// 获取带颜色标签的 Combo 标题
        /// </summary>
        public string GetColoredTitle()
        {
            string localizedName = LocalizationManager.GetText(ComboId, DisplayName);
            return $"<color=#{CustomColorHex}>【{localizedName}】</color>";
        }

        /// <summary>
        /// 获取带各自稀有度颜色的词缀列表字符串
        /// </summary>
        private string GetColoredAffixList()
        {
            StringBuilder sb = new StringBuilder();
            if (AffixIds != null)
            {
                foreach (string aid in AffixIds)
                {
                    if (EliteAffixes.TryGetAffix(aid, out var affixData))
                    {
                        sb.Append(affixData.ColoredTag);
                    }
                    else
                    {
                        sb.Append($"[{aid}]");
                    }
                }
            }
            return sb.ToString();
        }
    }
}