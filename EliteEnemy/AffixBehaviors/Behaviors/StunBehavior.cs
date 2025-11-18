using System;
using System.Reflection;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.BuffsSystem;

namespace EliteEnemies.AffixBehaviors
{
    // 震慑
    public class StunBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        private const string LogTag = "[EliteEnemies.StunBehavior]";
        public override string AffixName => "Stun";

        // 都是static函数 要么 const 要么 static readonly
        private static readonly string BuffName = "EliteBuff_Stun";
        private static readonly int BuffId = 99903;
        private static readonly float BuffDuration = 5f;
        private static readonly bool BuffLimitedLifeTime = true;

        private static readonly EliteBuffFactory.BuffConfig BuffConfig =
            new (BuffName, BuffId, BuffDuration, BuffLimitedLifeTime);

        private static Buff _sharedBuff;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, attacker);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}