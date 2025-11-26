using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 掷弹手词缀：攻击命中玩家时，向玩家发射手雷
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
        private static readonly int[] GrenadePool = { 66, 67, 660, 933, 941, 942, 24, 23, 100 };
        private static readonly float CooldownTime = 3.0f;
        private float _lastTriggerTime = -999f;

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time - _lastTriggerTime < CooldownTime) return;
            if (attacker == null) return;
            
            int randomItemId = GrenadePool[Random.Range(0, GrenadePool.Length)];

            EliteBehaviorHelper.LaunchGrenadeAtPlayer(attacker, randomItemId);

            _lastTriggerTime = Time.time;
            
            Debug.Log($"[Grenadier] 投掷了手雷 ID: {randomItemId}");
        }
        
        public override void OnEliteInitialized(CharacterMainControl character) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
        public void OnDamaged(CharacterMainControl victim, DamageInfo damageInfo) { }
        public void OnAttack(CharacterMainControl attacker, DamageInfo damageInfo) { }
    }
}