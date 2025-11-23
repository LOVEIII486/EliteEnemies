using Duckov.Buffs;
using EliteEnemies.BuffsSystem;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【扭曲】词缀 - 攻击使玩家子弹偏转形成弧形
    /// </summary>
    public class DistortionBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Distortion";

        private static readonly string BuffName = "EliteBuff_Distortion";
        private static readonly int BuffId = 99905;
        private static readonly float BuffDuration = 3f;
        private static readonly float Cooldown = 10f;
        
        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new (BuffName, BuffId, BuffDuration);
        private static Buff _sharedBuff;
        
        private float _lastTriggerTime = -999f;
        
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
            _lastTriggerTime = -999f;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            float currentTime = Time.time;
            if (currentTime - _lastTriggerTime < Cooldown)
            {
                return;
            }
            // 命中玩家时施加扭曲buff
            EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, attacker);
            _lastTriggerTime = currentTime;
        }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }
    }
}