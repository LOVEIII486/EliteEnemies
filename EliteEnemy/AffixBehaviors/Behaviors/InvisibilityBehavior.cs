using System;
using System.Collections.Generic;
using System.Linq;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using UnityEngine;
using ItemStatsSystem.Stats; // 必须引用，用于 ModifierType

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

        // 初始化本地化文本
        private readonly Lazy<string> _msgString = new(() => 
            LocalizationManager.GetText("Affix_Invisible_PopText_1", "你在打哪里？|我就在你后面|太慢了|没用的|看不见我吧"));

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            // 分割并缓存嘲讽语句
            var raw = _msgString.Value;
            if (!string.IsNullOrEmpty(raw))
            {
                _messages = raw.Split(MessageSeparator).ToList();
            }

            _isActive = true;
            _isVisible = true; // 初始默认可见
            _hasBeenHit = false;
            _timer = 0f;
            
            // 强化 AI 属性
            EnhanceAIBehavior(character);
        }

        /// <summary>
        /// 强化 AI 能力 (修复版)
        /// </summary>
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            // --- 1. Stat 修改 (感知与属性) ---
            // 视距 +50%
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewDistance, 0.5f, ModifierType.PercentageMultiply);
            // 视角 +30%
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewAngle, 0.3f, ModifierType.PercentageMultiply);
            // 听觉 +50%
            StatModifier.AddModifier(enemy, StatModifier.Attributes.HearingAbility, 0.5f, ModifierType.PercentageMultiply);
            
            // 转身速度 +30% (替代 PatrolTurnSpeed)
            StatModifier.AddModifier(enemy, StatModifier.Attributes.TurnSpeed, 0.3f, ModifierType.PercentageMultiply);
            // 瞄准转身 +50% (替代 CombatTurnSpeed)
            StatModifier.AddModifier(enemy, StatModifier.Attributes.AimTurnSpeed, 0.5f, ModifierType.PercentageMultiply);

            // --- 2. AI 字段修改 ---
            // 允许移动射击
            AIFieldModifier.ModifyImmediate(enemy, AIFieldModifier.Fields.ShootCanMove, 1f, false);
            // 允许冲刺
            AIFieldModifier.ModifyImmediate(enemy, AIFieldModifier.Fields.CanDash, 1f, false);
            
            // 注意：ReactionTime, ShootDelay, NightReactionTimeFactor 已被移除，因为新框架不支持修改这些非标准字段
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 攻击时显形一小段时间，或者你可以选择不处理
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            // 命中玩家时也可以触发嘲讽
            if (attacker != null && _messages.Count > 0 && UnityEngine.Random.value < 0.3f)
            {
                ShowRandomMessage(attacker);
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 首次受击触发隐身
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                _timer = VisibleInterval; // 立即进入隐身倒计时循环
                _isVisible = false;
                character.Hide();
            }
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isActive || !_hasBeenHit) return;

            // 1. 处理闪烁 (Flashing)
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                if (_flashTimer <= 0f)
                {
                    _flashTimer = FlashInterval;
                    _flashStep--;

                    if (_flashStep <= 0)
                    {
                        // 闪烁结束 -> 彻底隐身
                        _isFlashing = false;
                        _isVisible = false;
                        character.Hide();
                        _timer = VisibleInterval; // 重置显形倒计时
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

            // 2. 处理周期性显形逻辑
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                // 倒计时结束，开始闪烁显形
                _isFlashing = true;
                _flashStep = FlashCount * 2; // 开+关算一次，所以乘2
                _flashTimer = FlashInterval;
                
                // 嘲讽
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
            // _messages.Clear(); // 缓存的文本可以保留，无需清空
        }
    }
}