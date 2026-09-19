using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    // 震慑
    public class StunBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Stun";

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<StunBuff>(victim, attacker);
        }



    }
}
