using System.Collections.Generic;
using System.Text;
using EliteEnemies.EliteEnemy.AffixBehaviors;
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

        public EliteComboDefinition WithWhitelist(params string[] presetNames)
        {
            if (presetNames != null)
                foreach (var name in presetNames) AllowedPresets.Add(name);
            return this;
        }
        
        public string GetColoredTitle()
        {
            return $"<color=#{CustomColorHex}>【{DisplayName}】</color>";
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