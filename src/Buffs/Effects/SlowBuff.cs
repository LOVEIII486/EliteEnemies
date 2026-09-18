using EliteEnemies.Modifiers;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>迟缓：大幅降低移动速度。</summary>
    public sealed class SlowBuff : EliteBuffBase
    {
        private const float SpeedReduction = -0.5f;   // 降低 50% 速度

        public override string BuffName => "EliteBuff_Slow";
        public override int BuffId => 99902;
        public override float Duration => 5f;

        protected override void ApplyStats()
        {
            AddStat(StatKeys.WalkSpeed, SpeedReduction);
            AddStat(StatKeys.RunSpeed, SpeedReduction);
        }
    }
}
