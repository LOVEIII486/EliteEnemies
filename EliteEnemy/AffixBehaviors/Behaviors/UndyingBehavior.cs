using System;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：不死（Undying）
    /// 首次跌破阈值时：瞬间回血 + 无敌2秒（仅一次）
    /// </summary>
    public class UndyingBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Undead";
        
        private static readonly float LowHpThresholdRatio = 0.20f;  // 触发阈值：≤20%
        private static readonly float HealRatio           = 0.50f;  // 回血到 50%
        private static readonly float InvincibleDuration  = 2.0f;   // 无敌 2 秒
        
        private readonly Lazy<string> _popLineStartLazy = new Lazy<string>(() =>
            LocalizationManager.GetText("Affix_Undead_PopText_1", "还没那么容易倒下！"));
        
        private readonly Lazy<string> _popLineEndLazy = new Lazy<string>(() =>
            LocalizationManager.GetText("Affix_Undead_PopText_2", "萎了！"));
        
        private string PopLineStart => _popLineStartLazy.Value;
        private string PopLineEnd   => _popLineEndLazy.Value;
        
        private CharacterMainControl _owner;
        private bool  _triggered;              // 是否已触发过（只触发一次）
        private bool  _isInvincible;           // 当前是否处于无敌状态
        private bool  _wasInvincible;          // 触发前的无敌状态
        private float _invincibleEndTime;      // 无敌结束时间
        private float _lastHp = -1f;           // 上一帧血量

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (!character || character.Health == null)
                return;

            _owner  = character;
            _lastHp = character.Health.CurrentHealth;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!character || character.Health == null)
                return;

            var health = character.Health;
            if (health.IsDead)
                return;

            // 1. 检查是否需要触发“不死”
            if (!_triggered)
            {
                TryTriggerUndying(character, _lastHp, health.CurrentHealth, health.MaxHealth);
            }

            // 2. 更新无敌状态
            if (_isInvincible)
            {
                UpdateInvincibility(character);
            }

            // 3. 记录上一帧血量
            _lastHp = health.CurrentHealth;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_isInvincible && character && character.Health != null)
            {
                character.Health.SetInvincible(_wasInvincible);
                _isInvincible = false;
            }

            _owner = null;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }

        /// <summary>
        /// 判断是否从阈值上方“跌破”到阈值以下，如果是则触发一次
        /// </summary>
        private void TryTriggerUndying(
            CharacterMainControl character,
            float lastHp,
            float currentHp,
            float maxHp)
        {
            if (_triggered) return;
            if (maxHp <= 0f) return;

            float threshold = maxHp * LowHpThresholdRatio;

            // 只在 “上一帧 > 阈值 && 当前 ≤ 阈值” 时触发一次
            if (lastHp > threshold && currentHp <= threshold)
            {
                DoTriggerUndying(character);
            }
        }

        private void DoTriggerUndying(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null || health.IsDead)
                return;

            _triggered = true;

            // 把当前血量拉到指定比例（不改最大生命）
            float targetHp = health.MaxHealth * HealRatio;
            if (health.CurrentHealth < targetHp)
            {
                health.SetHealth(targetHp);
            }

            character.PopText(PopLineStart);
            StartInvincibility(character);
            AIFieldModifier.ModifyImmediate(character,AIFieldModifier.Fields.ShootCanMove,1f);
            AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.CanDash,1f);
        }

        private void StartInvincibility(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null)
                return;

            _wasInvincible     = health.Invincible;
            _isInvincible      = true;
            _invincibleEndTime = Time.time + InvincibleDuration;

            health.SetInvincible(true);
        }

        private void UpdateInvincibility(CharacterMainControl character)
        {
            if (Time.time >= _invincibleEndTime)
            {
                EndInvincibility(character);
            }
        }

        private void EndInvincibility(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null)
                return;
            
            health.SetInvincible(_wasInvincible);
            _isInvincible = false;
            
            if (!health.IsDead)
            {
                character.PopText(PopLineEnd);
            }
        }
    }
}
