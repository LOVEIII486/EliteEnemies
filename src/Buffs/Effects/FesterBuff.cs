using EliteEnemies.Modifiers;
using ItemStatsSystem.Stats;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>溃伤：治疗效果减半。</summary>
    public sealed class FesterBuff : EliteBuffBase
    {
        /// <summary>
        /// 治疗加成惩罚。
        ///
        /// <para>游戏公式是 <c>回血量 × (1 + HealGain)</c>（<c>CharacterMainControl.cs:2376</c>），
        /// 所以 <c>-0.5</c> 正好是减半。</para>
        /// </summary>
        private const float HealGainPenalty = -0.5f;

        public override string BuffName => "EliteBuff_Fester";
        public override int BuffId => 99910;
        public override float Duration => 10f;

        protected override void ApplyStats()
        {
            // ⚠ 这里**必须**显式用 ModifierType.Add，不能用基类 AddStat 的默认值。
            //
            // 游戏侧的 HealGain 是**加成型**属性（就是上面那条 `× (1 + HealGain)`），基值为 0。
            // 而 PercentageMultiply 的算法是 `结果 *= (1 + 修饰值)`——基值 0 乘任何数还是 0，
            // 等于**一点效果都没有**，而且不报错、不留日志。
            // 这正是本项目一路在防的那类静默失效，所以在这里写死类型并留这段话。
            AddStatFlat(StatKeys.HealGain, HealGainPenalty, ModifierType.Add);
        }
    }
}
