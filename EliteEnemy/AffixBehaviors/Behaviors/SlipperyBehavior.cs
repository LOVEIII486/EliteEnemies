using Duckov.Buffs;
using EliteEnemies.EliteEnemy.BuffsSystem;

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

        private static readonly EliteBuffFactory.BuffConfig BuffConfig = 
            new (BuffName, BuffId, BuffDuration);
            
        private static Buff _sharedBuff;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
        }

        public override void OnHitPlayer(CharacterMainControl player, DamageInfo damageInfo)
        {
            EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, player);
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}