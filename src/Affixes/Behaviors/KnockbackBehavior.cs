using System;
using ECM2;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    public class KnockbackBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Knockback";

        private static readonly float KnockbackCooldown = 5f; // cd

        private static float _globalLastKnockbackTime = -999f;
        
        private string EnemyPopLine => LocalizationManager.GetText(
            "EliteEnemies_Affix_Knockback_PopText_1",
            "<color=#FF4500>装逼我让你飞起来！</color>");
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time - _globalLastKnockbackTime < KnockbackCooldown)
            {
                return;
            }

            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            var player = victim;
            if (player == null)
            {
                return;
            }

            // ⚠ 方向与距离倍率只有主机算得出来（要用精英与玩家的位置），
            //   所以先在这里算好，再决定是本地做还是交给对方那台机器做。
            Vector3 horizontal = player.transform.position - attacker.transform.position;
            horizontal.y = 0f;

            float distance = horizontal.magnitude;
            float distanceMultiplier = Mathf.Clamp(1.5f / Mathf.Max(distance, 1f), 0.9f, 2f);
            Vector3 scaledDirection = (distance > 0.0001f ? horizontal / distance : Vector3.zero)
                                      * distanceMultiplier;

            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.Knockback, scaledDirection))
            {
                attacker.PopText(EnemyPopLine);
                _globalLastKnockbackTime = Time.time;
                return;
            }

            PlayerEffectActions.Knockback(player, scaledDirection);

            attacker.PopText(EnemyPopLine);
            _globalLastKnockbackTime = Time.time;
        }







    }
}