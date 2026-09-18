using EliteEnemies.Modifiers;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>震慑：大幅降低后坐力控制（枪口乱跳）。</summary>
    public sealed class StunBuff : EliteBuffBase
    {
        private const float RecoilControlReduction = -0.7f;   // 降低 70%

        public override string BuffName => "EliteBuff_Stun";
        public override int BuffId => 99903;
        public override float Duration => 8f;

        protected override void ApplyStats()
        {
            AddStat(StatKeys.RecoilControl, RecoilControlReduction);
        }
    }
}
