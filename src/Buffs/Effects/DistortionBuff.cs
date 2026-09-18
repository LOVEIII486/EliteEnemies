using EliteEnemies.Modifiers;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>扭曲：降低子弹速度，并让射向该角色的子弹发生偏转。</summary>
    public sealed class DistortionBuff : EliteBuffBase
    {
        private const float BulletSpeedReduction = -0.7f;   // 减少 70% 速度

        public override string BuffName => "EliteBuff_Distortion";
        public override int BuffId => 99905;
        public override float Duration => 3f;

        protected override void ApplyStats()
        {
            AddStat(StatKeys.BulletSpeedMultiplier, BulletSpeedReduction);
        }

        protected override void OnApplied()
        {
            BulletDeflectionTracker.Instance.Register(Character);
        }

        protected override void OnRemoved()
        {
            // 属性撤销由基类自动做了；这里只处理"偏转名单"
            BulletDeflectionTracker.Instance.Unregister(Character);
        }
    }
}
