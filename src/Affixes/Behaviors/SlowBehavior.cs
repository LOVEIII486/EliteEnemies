using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    // 迟缓
    public class SlowBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Slow";

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<SlowBuff>(victim, attacker);
        }



    }
}
