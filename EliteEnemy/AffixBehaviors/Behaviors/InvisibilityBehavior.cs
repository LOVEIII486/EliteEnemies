using System;
using System.Collections.Generic;
using System.Linq;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using UnityEngine;
using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 隐身词缀 - 受伤后触发隐身，然后周期性闪烁
    /// </summary>
    public class InvisibilityBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Invisible";
        
        private const char MessageSeparator = '|';
        
        private static readonly float VisibleInterval = 6.0f;  // 每隔多少秒触发一次显形效果
        private static readonly float FlashInterval   = 0.15f; // 闪烁间隔（秒）
        private static readonly int   FlashCount      = 3;     // 每次闪烁次数
        
        private float _timer;
        private float _flashTimer;
        private int _flashStep;
        private bool _isFlashing;
        private bool _isVisible;
        private bool _isActive;
        private bool _hasBeenHit; 
        
        private List<string> _messages = new List<string>();
        private int _lastMsgIndex = -1;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            InitMessages();

            _isActive = true;
            _isVisible = true;
            _hasBeenHit = false;
            _timer = 0f;
            
            EnhanceAIBehavior(character);
        }

        /// <summary>
        /// 从本地化系统加载嘲讽语句
        /// </summary>
        private void InitMessages()
        {
            _messages.Clear();
            string raw = LocalizationManager.GetText("Affix_Invisible_Messages");
            
            if (!string.IsNullOrEmpty(raw))
            {
                _messages = raw.Split(MessageSeparator).ToList();
            }
        }

        /// <summary>
        /// 强化 AI 能力
        /// </summary>
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            // --- Stat 修改 ---
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewDistance, 0.5f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewAngle, 0.3f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.HearingAbility, 0.5f, ModifierType.PercentageMultiply);
            
            StatModifier.AddModifier(enemy, StatModifier.Attributes.TurnSpeed, 0.3f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.AimTurnSpeed, 0.5f, ModifierType.PercentageMultiply);

            // --- AI 字段修改 ---
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.CanDash, 1f, false);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ShootCanMove, 1f, false);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            // if (attacker != null && _messages.Count > 0 && UnityEngine.Random.value < 0.3f)
            // {
            //     ShowRandomMessage(attacker);
            // }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 首次受击触发隐身
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                _timer = VisibleInterval;
                _isVisible = false;
                character.Hide();
                // ShowRandomMessage(character);
            }
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isActive || !_hasBeenHit) return;

            // 处理闪烁
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                if (_flashTimer <= 0f)
                {
                    _flashTimer = FlashInterval;
                    _flashStep--;

                    if (_flashStep <= 0)
                    {
                        _isFlashing = false;
                        _isVisible = false;
                        character.Hide();
                        _timer = VisibleInterval;
                    }
                    else
                    {
                        // 切换显隐状态
                        if (_isVisible)
                        {
                            character.Hide();
                            _isVisible = false;
                        }
                        else
                        {
                            character.Show();
                            _isVisible = true;
                        }
                    }
                }
                return;
            }

            // 处理周期性显形
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _isFlashing = true;
                _flashStep = FlashCount * 2; // 开+关算一次，所以乘2
                _flashTimer = FlashInterval;
                
                ShowRandomMessage(character);
            }
        }

        private void ShowRandomMessage(CharacterMainControl character)
        {
            if (_messages.Count == 0) return;

            int idx = UnityEngine.Random.Range(0, _messages.Count);
            if (idx == _lastMsgIndex && _messages.Count > 1)
            {
                idx = (idx + 1) % _messages.Count;
            }
            _lastMsgIndex = idx;
            
            character.PopText(_messages[idx]);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character != null)
            {
                character.Show(); // 确保死后或移除时可见
            }
            
            _isActive = false;
            _isVisible = true;
            _isFlashing = false;
            _hasBeenHit = false;
            _lastMsgIndex = -1;
        }
    }
}