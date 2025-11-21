using System;
using System.Reflection;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.BuffsSystem;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【致盲】词缀 - 攻击命中玩家时使其视野受限
    /// </summary>
    public class BlindnessBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Blindness";

        private static readonly string BuffName = "EliteBuff_Blindness";
        private static readonly int BuffId = 99901;
        private static readonly float BuffDuration = 7f;
        
        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new (BuffName, BuffId, BuffDuration);
        private static Buff _sharedBuff;
        
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            // if (AffixBehaviorUtils.IsPlayerHitByAttacker(character))
            // {
            //     EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, character);
            // }
        }
        
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
             EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, attacker);
        }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}