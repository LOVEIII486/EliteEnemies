using Duckov.Buffs;
using EliteEnemies.EliteEnemy.BuffsSystem;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【打滑】词缀 - 攻击命中玩家时使其脚底打滑
    /// </summary>
    public class SlipperyBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Slippery";

        private static readonly string BuffName = "EliteBuff_Slippery";
        private static readonly int BuffId = 99908; 
        private static readonly float BuffDuration = 5f;
        
        private const float InternalCooldown = 15f;
        private float _lastTriggerTime = -999f;

        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new (BuffName, BuffId, BuffDuration);
            
        private static Buff _sharedBuff;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
            _lastTriggerTime = -999f;
        }

        public override void OnHitPlayer(CharacterMainControl player, DamageInfo damageInfo)
        {
            if (Time.time < _lastTriggerTime + InternalCooldown)
            {
                return;
            }
            EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, player);
            _lastTriggerTime = Time.time;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}