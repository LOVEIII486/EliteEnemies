using System;
using UnityEngine; // 必须引用，用于 Time.time
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 词缀：弹匣诅咒（Magazine Curse）
    /// </summary>
    public class MagazineCurseBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "MagazineCurse";
        
        private static readonly float Cooldown = 15.0f;
        private float _lastTriggerTime = -999f;

        private readonly Lazy<string> _enemyPopLineLazy = new(() => LocalizationManager.GetText("Affix_MagazineCurse_PopText_1"));
        private readonly Lazy<string> _playerPopLineLazy = new(() => LocalizationManager.GetText("Affix_MagazineCurse_PopText_2" ));

        private string EnemyPopLine => _enemyPopLineLazy.Value;
        private string PlayerPopLine => _playerPopLineLazy.Value;

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (Time.time - _lastTriggerTime < Cooldown) return;

            var attacker = damageInfo.fromCharacter;
            if (attacker == null || !attacker.IsMainCharacter) return;

            var player = attacker;
            if (player == null) return;

            var gun = player.GetGun();
            if (gun == null)
            {
                return; // 没枪不触发，也不进入冷却
            }
            
            bool reloadStarted = player.TryToReload();

            if (reloadStarted)
            {
                character?.PopText(EnemyPopLine);
                player.PopText(PlayerPopLine);
                _lastTriggerTime = Time.time;
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
            _lastTriggerTime = -999f;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }
    }
}