using System.Collections.Generic;

namespace EliteEnemies
{
    /// <summary>
    /// 精英敌人配置数据类
    /// </summary>
    public class EliteEnemiesConfig
    {
        public float NormalEliteChance { get; set; } = 1.0f;
        public float BossEliteChance { get; set; } = 0.0f;
        public float MerchantEliteChance { get; set; } = 0.0f;
        public int MaxAffixCount { get; set; } = 2;
        public bool ShowDetailedHealth { get; set; } = true;
        public float DropRateMultiplier { get; set; } = 1.0f;
        public float ItemQualityBias { get; set; } = -0.8f;
        public bool EnableBonusLoot { get; set; } = true;
        public float GlobalHealthMultiplier { get; set; } = 1.0f;
        public float GlobalDamageMultiplier { get; set; } = 1.0f;
        public float GlobalSpeedMultiplier { get; set; } = 1.0f;
        public bool ShowAffixFootText { get; set; } = true;
        public HashSet<string> DisabledAffixes { get; set; } = new HashSet<string>();
    }
}