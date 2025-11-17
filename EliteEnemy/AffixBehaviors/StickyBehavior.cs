using System;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：粘性（Sticky）
    /// </summary>
    public class StickyBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Sticky";
        
        // 使用懒加载避免初始化太早，之后使用缓存
        // 不过这样不支持动态切换语言，但对于我的mod来说应该不重要
        private readonly Lazy<string> _enemyPopLine = new(() =>
            LocalizationManager.GetText(
                "Affix_Sticky_PopText_1"
            )
        );
        private readonly Lazy<string> _playerPopLine = new(() =>
            LocalizationManager.GetText(
                "Affix_Sticky_PopText_2"
            )
        );
        private string EnemyPopLine => _enemyPopLine.Value;
        private string PlayerPopLine => _playerPopLine.Value;
        
        private static readonly bool   ConsumeWhenNoWeapon = true;   // 若玩家当下没有武器，是否也算触发已消耗
        
        private bool _consumed;                               // 是否已触发过
        private CharacterMainControl _owner;                  // 敌人宿主
        private UnityAction<DamageInfo> _hurtHandler;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (!character || character.Health == null) return;

            _owner = character;
            _hurtHandler = OnVictimHurt;
            character.Health.OnHurtEvent.AddListener(_hurtHandler);
        }

        /// <summary>
        /// 扣血后、判死前：检测是否由玩家造成，若是且未触发过，则让玩家掉落当前武器。
        /// </summary>
        private void OnVictimHurt(DamageInfo dmg)
        {
            if (_consumed) return;

            var attacker = dmg.fromCharacter;
            if (attacker == null || !attacker.IsMainCharacter) return;

            var player = attacker;
            if (player == null) return;

            var heldAgent = player.CurrentHoldItemAgent;
            var heldItem  = heldAgent ? heldAgent.Item : null;

            // 判定是否手持“武器”
            bool hasWeapon = (heldItem != null && heldItem.Tags != null && heldItem.Tags.Contains("Weapon"));

            // 若没有武器，是否也消耗触发
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

            // 自动切回可用武器
            // player.SwitchToFirstAvailableWeapon();
            
            _owner?.PopText(EnemyPopLine);
            player.PopText(PlayerPopLine);

            //player.PickupItem(heldItem);

            _consumed = true;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo dmg)
        {
            OnCleanup(character);
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_hurtHandler != null && character && character.Health != null)
            {
                character.Health.OnHurtEvent.RemoveListener(_hurtHandler);
                _hurtHandler = null;
            }
        }
    }
}
