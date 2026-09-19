using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    // 食粪者
    public class DungEaterBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "DungEater";

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<DungEaterBuff>(victim, attacker);
        }



    }
}
