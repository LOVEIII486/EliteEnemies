using System;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：粘性（Sticky）
    /// 玩家攻击该精英时让玩家掉落当前武器（仅触发一次）
    /// </summary>
    public class StickyBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Sticky";

        private readonly Lazy<string> _enemyPopLine = new(() =>
            LocalizationManager.GetText("Affix_Sticky_PopText_1"));

        private readonly Lazy<string> _playerPopLine = new(() =>
            LocalizationManager.GetText("Affix_Sticky_PopText_2"));

        private string EnemyPopLine => _enemyPopLine.Value;
        private string PlayerPopLine => _playerPopLine.Value;

        private static readonly bool ConsumeWhenNoWeapon = true;

        private bool _consumed = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _consumed = false;
        }
        
        public void OnDamaged(CharacterMainControl owner, DamageInfo dmg)
        {
            if (_consumed || owner == null)
                return;

            var attacker = dmg.fromCharacter;
            if (attacker == null || !attacker.IsMainCharacter)
                return;

            var player = attacker;

            // 当前武器
            var heldAgent = player.CurrentHoldItemAgent;
            var heldItem = heldAgent ? heldAgent.Item : null;

            bool hasWeapon =
                heldItem != null &&
                heldItem.Tags != null &&
                heldItem.Tags.Contains("Weapon");

            // 玩家没有拿武器
            if (!hasWeapon)
            {
                if (ConsumeWhenNoWeapon)
                {
                    _consumed = true;
                }
                return;
            }
            
            var dropPos = player.transform.position + Vector3.up * 0.1f;
            heldItem.Drop(dropPos, true, Vector3.forward, 360f);
            
            if (player.agentHolder != null && player.CurrentHoldItemAgent != null)
            {
                player.agentHolder.ChangeHoldItem(null);
            }
            
            owner.PopText(EnemyPopLine);
            player.PopText(PlayerPopLine);

            _consumed = true;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}
