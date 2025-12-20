using System.Collections.Generic;

namespace EliteEnemies.EliteEnemy.ComboSystem
{
    public class EliteComboDefinition
    {
        public string ComboId { get; set; }           // Combo唯一标识
        public string DisplayName { get; set; }        // 游戏内显示的特殊头衔
        public List<string> AffixIds { get; set; }    // 强制赋予的词缀Key列表
        public float Weight { get; set; }              // 权重

        public EliteComboDefinition(string id, string name, List<string> affixes, float weight = 1f)
        {
            ComboId = id;
            DisplayName = name;
            AffixIds = affixes;
            Weight = weight;
        }
    }
}