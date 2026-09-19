using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【打滑】词缀 - 攻击命中玩家时使其脚底打滑
    /// </summary>
    public class SlipperyBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Slippery";

        private const float InternalCooldown = 15f;
        private float _lastTriggerTime = -999f;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time < _lastTriggerTime + InternalCooldown) return;

            EliteBuffs.ApplyToPlayer<SlipperyBuff>(victim, attacker);
            _lastTriggerTime = Time.time;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }



    }
}
