using System;
using ECM2;
using UnityEngine;
using EliteEnemies.AffixBehaviors;

namespace EliteEnemies.AffixBehaviors
{
    public class KnockbackBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Knockback";

        private static readonly float KnockbackForce = 8f; // 垂直力
        private static readonly float HorizontalForce = 15f; // 横向力
        private static readonly float GroundPauseDuration = 0.3f; // 地面约束暂停时间
        private static readonly float KnockbackCooldown = 5f; // cd

        private static float _globalLastKnockbackTime = -999f;
        
        private readonly Lazy<string> _enemyPopLineLazy = new(() =>
            LocalizationManager.GetText(
                "Affix_Knockback_PopText_1",
                "<color=#FF4500>装逼我让你飞起来！</color>"
            )
        );

        private string EnemyPopLine => _enemyPopLineLazy.Value;
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time - _globalLastKnockbackTime < KnockbackCooldown)
            {
                return;
            }

            var player = CharacterMainControl.Main;
            if (player == null)
            {
                return;
            }

            PerformKnockback(player, attacker);

            _globalLastKnockbackTime = Time.time;
        }

        private unsafe void PerformKnockback(CharacterMainControl player, CharacterMainControl attacker)
        {
            var movement = player.movementControl;
            if (movement == null)
            {
                return;
            }

            CharacterMovement component = movement.GetComponent<CharacterMovement>();
            if (component == null)
            {
                return;
            }

            // 延长地面约束暂停时间
            component.PauseGroundConstraint(GroundPauseDuration);

            Vector3 toPlayer = player.transform.position - attacker.transform.position;
            float distance = toPlayer.magnitude;
            Vector3 direction = toPlayer.normalized;
            direction.y = 0;

            // 根据距离动态调整力度
            float distanceMultiplier = Mathf.Clamp(1.5f / Mathf.Max(distance, 1f), 0.9f, 2f);

            Vector3 vector = component.velocity;

            // 增强横向力，并添加距离倍率
            vector.x = direction.x * HorizontalForce * distanceMultiplier;
            vector.y = KnockbackForce;
            vector.z = direction.z * HorizontalForce * distanceMultiplier;

            component.velocity = vector;

            attacker.PopText(EnemyPopLine);
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}