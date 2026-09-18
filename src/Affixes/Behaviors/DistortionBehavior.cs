using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【扭曲】词缀 - 攻击使玩家子弹偏转形成弧形
    /// </summary>
    public class DistortionBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Distortion";

        private const float Cooldown = 10f;

        private float _lastTriggerTime = -999f;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            float currentTime = Time.time;
            if (currentTime - _lastTriggerTime < Cooldown) return;

            EliteBuffs.ApplyToPlayer<DistortionBuff>(attacker);
            _lastTriggerTime = currentTime;
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }
    }
}
