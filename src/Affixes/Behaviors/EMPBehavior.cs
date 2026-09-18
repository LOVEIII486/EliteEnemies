using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【电磁干扰】词缀 - 攻击命中玩家时禁用其 HUD
    /// </summary>
    public class EMPBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "EMP";

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<EMPBuff>(attacker);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }


    }
}
