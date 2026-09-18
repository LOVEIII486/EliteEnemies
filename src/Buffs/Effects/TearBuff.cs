using EliteEnemies.Localization;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>撕裂：随机削弱护甲。</summary>
    public sealed class TearBuff : EliteBuffBase
    {
        private const float MinReduction = -0.4f;
        private const float MaxReduction = -0.1f;

        private float _reduction;
        private bool _rolled;

        public override string BuffName => "EliteBuff_Tear";
        public override int BuffId => 99907;
        public override float Duration => 6f;

        protected override void ApplyStats()
        {
            // 只掷一次骰子。ApplyStats 在**层数变化时也会重跑**，每次都重掷的话，
            // "同 ID 再施加"或"减一层"会顺带把减甲幅度也换掉——那不是我们想要的行为。
            if (!_rolled)
            {
                _reduction = Random.Range(MinReduction, MaxReduction);
                _rolled = true;
            }

            AddStat(StatKeys.BodyArmor, _reduction);
            AddStat(StatKeys.HeadArmor, _reduction);
        }

        protected override void OnApplied()
        {
            var format = LocalizationManager.GetText("EliteEnemies_Affix_Tear_PopText_1");
            if (string.IsNullOrEmpty(format)) return;

            Character?.PopText(string.Format(format, (_reduction * 100f).ToString("F1")));
        }
    }
}
