using System.Collections.Generic;

namespace EliteEnemies.Settings
{
    /// <summary>
    /// 精英敌人配置数据类
    /// </summary>
    public class EliteEnemiesConfig
    {
        public float NormalEliteChance { get; set; } = 1.0f;
        public float BossEliteChance { get; set; } = 0.4f;
        public float MerchantEliteChance { get; set; } = 0.0f;
        public int MaxAffixCount { get; set; } = 2;
        public float DropRateMultiplier { get; set; } = 0.7f;
        public float ItemQualityBias { get; set; } = -1.5f;
        public bool EnableBonusLoot { get; set; } = true;
        public float GlobalHealthMultiplier { get; set; } = 1.0f;
        public float GlobalDamageMultiplier { get; set; } = 1.0f;
        public float GlobalSpeedMultiplier { get; set; } = 1.0f;
        public HashSet<string> DisabledAffixes { get; set; } = new HashSet<string>();
        public int[] AffixCountWeights { get; set; } = new int[] { 0, 50, 30, 15, 4, 1 };
        
        public bool ShowEliteName { get; set; } = true;
        public bool ShowDetailedHealth { get; set; } = true;
        public GameConfig.AffixTextDisplayPosition AffixDisplayPosition { get; set; } = GameConfig.AffixTextDisplayPosition.Overhead;
        public int AffixFontSize { get; set; } = 20;
    }
}