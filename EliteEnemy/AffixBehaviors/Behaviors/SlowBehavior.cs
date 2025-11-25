using Duckov.Buffs;
using EliteEnemies.EliteEnemy.BuffsSystem;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    // 迟缓
    public class SlowBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Slow";

        private static readonly string BuffName = "EliteBuff_Slow";
        private static readonly int BuffId = 99902;
        private static readonly float BuffDuration = 5f;

        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new (BuffName, BuffId, duration: BuffDuration);
    
        private static Buff _sharedBuff;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
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