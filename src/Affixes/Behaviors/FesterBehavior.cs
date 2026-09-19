using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 溃伤：命中玩家后 10 秒内，玩家的**治疗效果减半**。
    ///
    /// <para>不增加任何伤害，只是让玩家"打药回不满"——所以它永远留了
    /// 「先脱离交火再治」这条路给玩家，不属于一击致命那类设计。</para>
    /// </summary>
    public class FesterBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Fester";

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            // 刻意**不做节流**：同 ID 再施加只是刷新时长（不像「寒冷」那样要叠层），
            // 而施加本身只是 buffManager 里一次 List.Find——霰弹枪一轮多颗弹丸也无所谓。
            EliteBuffs.ApplyToPlayer<FesterBuff>(victim, attacker);
        }
    }
}
