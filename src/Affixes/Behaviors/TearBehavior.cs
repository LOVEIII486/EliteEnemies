using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【撕裂】词缀 - 攻击命中玩家时削弱其护甲
    /// </summary>
    public class TearBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Tear";

        // 攻击命中玩家时触发
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            EliteBuffs.ApplyToPlayer<TearBuff>(attacker);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }


    }
}
