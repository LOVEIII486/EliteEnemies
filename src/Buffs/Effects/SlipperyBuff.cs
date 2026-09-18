using EliteEnemies.Localization;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>打滑：大幅降低加速度（像在冰面上），同时走得更快、跑得更慢。</summary>
    public sealed class SlipperyBuff : EliteBuffBase
    {
        private const float AccReduction = -0.9f;
        private const float SpeedMultiplier = 0.7f;

        public override string BuffName => "EliteBuff_Slippery";
        public override int BuffId => 99908;
        public override float Duration => 5f;

        protected override void ApplyStats()
        {
            AddStat(StatKeys.WalkAcc, AccReduction);
            AddStat(StatKeys.RunAcc, AccReduction);

            // 走速 +40%（= 1.4x）、跑速 -30%（= 0.7x）
            AddStat(StatKeys.WalkSpeed, SpeedMultiplier * 2f - 1f);
            AddStat(StatKeys.RunSpeed, SpeedMultiplier - 1f);
        }

        protected override void OnApplied()
        {
            // 直接在这里取文本，而不是存进静态字段：静态初始化会冻死兜底文本（AGENT.md §3.5），
            // 而这个钩子跑在"用的时候"，本地化早已就绪。
            var text = LocalizationManager.GetText("EliteEnemies_Affix_Slippery_PopText_1");
            if (!string.IsNullOrEmpty(text)) Character?.PopText(text);
        }
    }
}
