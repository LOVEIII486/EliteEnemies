using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.EliteEnemy.BuffsSystem;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【撕裂】词缀 - 攻击命中玩家时削弱其护甲
    /// </summary>
    public class TearBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Tear";

        private static readonly string BuffName = "EliteBuff_Tear";
        private static readonly int BuffId = 99907;
        private static readonly float BuffDuration = 8f;
        
        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new EliteBuffFactory.BuffConfig(BuffName, BuffId, BuffDuration);
        
        private static Buff _sharedBuff;
        
        private static bool _hasCheckStats = false;
        private static bool _hasCheckStatsSelf = false;
        
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
            if (!_hasCheckStatsSelf)
            {
                _hasCheckStatsSelf = true;
            }
        }

        // 攻击命中玩家时触发
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