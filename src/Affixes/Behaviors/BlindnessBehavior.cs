using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【致盲】词缀 - 攻击命中玩家时使其视野受限
    /// </summary>
    public class BlindnessBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Blindness";

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<BlindnessBuff>(victim, attacker);
        }



    }
}
