using System;
using System.Reflection;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.BuffsSystem;

namespace EliteEnemies.AffixBehaviors
{
    // 食粪者
    public class DungEaterBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        private const string LogTag = "[EliteEnemies.DungEaterBehavior]";
        public override string AffixName => "DungEater";

        private static readonly string BuffName = "EliteBuff_DungEater";
        private static readonly int BuffId = 99904;
        private static readonly float BuffDuration = 5f;
        
        private static readonly EliteBuffFactory.BuffConfig BuffConfig =
            new (BuffName, BuffId, BuffDuration);

        private static Buff _sharedBuff;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (AffixBehaviorUtils.IsPlayerHitByAttacker(character))
            {
                EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, character);
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}