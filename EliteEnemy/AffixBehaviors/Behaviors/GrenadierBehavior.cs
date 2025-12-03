using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 掷弹手词缀
    /// </summary>
    public class GrenadierBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Grenadier";
        
        /// <summary>
        /// 66	2	优秀	闪光手雷	Item_FlashGrenade
        /// 67	2	优秀	手雷	    Item_Grenade
        /// 660	3	精良	烟雾弹	Item_SmokeGrenade
        /// 933	4	史诗	毒雾弹	Item_ToxGrenade
        /// 941	4	史诗	燃烧弹	Item_FireGrenade
        /// 942	4	史诗	电击手雷	Item_ElecGrenade
        /// 24	5	传说	集束管状炸弹	Item_DynamiteMultiple
        /// 100	3	精良	捕兽陷阱	Item_Trap
        /// 23	1	普通	管状炸弹	Item_Dynamite
        /// </summary>
        private static readonly int[] GrenadePool = { 66, 67, 933, 941, 942 };

        private static readonly float HitCooldown = 3.0f;     // 命中玩家触发CD
        private static readonly float DamageCooldown = 12.0f;  // 受伤反击触发CD
        
        private float _lastHitTime = -999f;
        private float _lastDamageTime = -999f;
        
        /// <summary>
        /// 命中玩家时触发
        /// </summary>
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time - _lastHitTime < HitCooldown) return;
            if (attacker == null) return;

            int randomItemId = GetRandomGrenadeId();
            
            EliteBehaviorHelper.LaunchGrenadeAtPlayer(attacker, randomItemId);
            
            _lastHitTime = Time.time;
        }
        
        /// <summary>
        /// 受到伤害时触发
        /// </summary>
        public void OnDamaged(CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time - _lastDamageTime < DamageCooldown) return;
            
            CharacterMainControl attacker = damageInfo.fromCharacter;
            
            if (attacker == null || attacker == victim || attacker.Team == victim.Team) return;

            int randomItemId = GetRandomGrenadeId();
            EliteBehaviorHelper.LaunchGrenade(victim, randomItemId, attacker.transform.position);

            _lastDamageTime = Time.time;
        }
        
        public void OnAttack(CharacterMainControl attacker, DamageInfo damageInfo) 
        { 
        }
        
        private int GetRandomGrenadeId()
        {
            return GrenadePool[Random.Range(0, GrenadePool.Length)];
        }

        public override void OnEliteInitialized(CharacterMainControl character) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}