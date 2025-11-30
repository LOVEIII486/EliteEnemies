using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.BuffsSystem;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【电磁干扰】词缀 - 攻击命中玩家时禁用其 HUD 10秒
    /// </summary>
    public class EMPBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "EMP";

        private static readonly string BuffName = "EliteBuff_EMP";
        private static readonly int BuffId = 99906;
        private static readonly float BuffDuration = 5f;
        
        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new EliteBuffFactory.BuffConfig(BuffName, BuffId, BuffDuration);
        
        private static Buff _sharedBuff;
        
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, attacker);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}