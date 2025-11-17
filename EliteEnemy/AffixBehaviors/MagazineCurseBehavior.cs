using System;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：弹匣诅咒（Magazine Curse）
    /// </summary>
    public class MagazineCurseBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "MagazineCurse";
        
        private readonly Lazy<string> _enemyPopLineLazy = new(() =>
            LocalizationManager.GetText(
                "Affix_MagazineCurse_PopText_1",
                "<color=#9B59B6>弹匣诅咒！</color>"
            )
        );

        private readonly Lazy<string> _playerPopLineLazy = new(() =>
            LocalizationManager.GetText(
                "Affix_MagazineCurse_PopText_2",
                "<color=#9B59B6>被迫换弹！</color>"
            )
        );

        private string EnemyPopLine => _enemyPopLineLazy.Value;
        private string PlayerPopLine => _playerPopLineLazy.Value;

        private bool _consumed = false; // 只触发一次

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_consumed) return;

            var attacker = damageInfo.fromCharacter;
            if (attacker == null || !attacker.IsMainCharacter) return;

            var player = attacker;
            if (player == null) return;

            // 检查是否持有枪械
            var gun = player.GetGun();
            if (gun == null)
            {
                _consumed = true; // 没枪也消耗触发次数
                return;
            }
            
            bool reloadStarted = player.TryToReload();

            if (reloadStarted)
            {
                character?.PopText(EnemyPopLine);
                player.PopText(PlayerPopLine);
            }

            _consumed = true;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
            _consumed = false;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _consumed = false;
        }
    }
}