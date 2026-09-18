using EliteEnemies.Modifiers;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>致盲：大幅削减视野距离与视野角度。</summary>
    public sealed class BlindnessBuff : EliteBuffBase
    {
        private const float ViewDistanceReduction = -0.85f;   // 减少 85%
        private const float ViewAngleReduction = -0.6f;       // 减少 60%

        public override string BuffName => "EliteBuff_Blindness";
        public override int BuffId => 99901;
        public override float Duration => 7f;

        protected override void ApplyStats()
        {
            AddStat(StatKeys.ViewDistance, ViewDistanceReduction);
            AddStat(StatKeys.ViewAngle, ViewAngleReduction);
        }
    }
}
