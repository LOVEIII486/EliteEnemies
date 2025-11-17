using System;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：不死（Undying）
    /// 首次跌破阈值时：瞬间回血 + 无敌若干秒（仅一次）
    /// </summary>
    public class UndyingBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        private const string LogTag = "[EliteEnemies.UndyingBehavior]";
        
        public override string AffixName => "Undead";
        
        private static readonly float LowHpThresholdRatio = 0.20f;  // 触发阈值：≤20%
        private static readonly float HealRatio = 0.50f;             // 回血到 50%
        private static readonly float InvincibleDuration = 2.0f;    // 无敌 2 秒
        
        private readonly Lazy<string> _popLineStartLazy = new Lazy<string>(() =>
            LocalizationManager.GetText("Affix_Undead_PopText_1", "还没那么容易倒下！"));
        
        private readonly Lazy<string> _popLineEndLazy = new Lazy<string>(() =>
            LocalizationManager.GetText("Affix_Undead_PopText_2", "萎了！"));
        
        private string PopLineStart => _popLineStartLazy.Value;
        private string PopLineEnd => _popLineEndLazy.Value;
        
        private bool _triggered = false;           // 是否已触发
        private bool _isInvincible = false;        // 是否处于无敌状态
        private bool _wasInvincible = false;       // 触发前的无敌状态
        private float _invincibleEndTime = 0f;     // 无敌结束时间
        private float _lastHp = -1f;               // 上一帧血量
        private CharacterMainControl _owner;
        private UnityAction<DamageInfo> _hurtHandler;
        

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (!character || character.Health == null) return;

            _owner = character;
            _lastHp = character.Health.CurrentHealth;
            _hurtHandler = OnHurtEvent;
            character.Health.OnHurtEvent.AddListener(_hurtHandler);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!character || character.Health == null) return;

            var health = character.Health;
            if (health.IsDead) return;
            
            if (!_triggered)
            {
                CheckTrigger(character);
            }
            
            if (_isInvincible)
            {
                UpdateInvincibility(character);
            }

            _lastHp = health.CurrentHealth;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_hurtHandler != null && character && character.Health != null)
            {
                character.Health.OnHurtEvent.RemoveListener(_hurtHandler);
                _hurtHandler = null;
            }
            
            if (_isInvincible && character && character.Health != null)
            {
                character.Health.SetInvincible(_wasInvincible);
                _isInvincible = false;
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }
        

        private void CheckTrigger(CharacterMainControl character)
        {
            var health = character.Health;
            float maxHp = health.MaxHealth;
            if (maxHp <= 0f) return;

            float currentHp = health.CurrentHealth;
            float threshold = maxHp * LowHpThresholdRatio;

            // 检测是否穿过阈值
            if (_lastHp > threshold && currentHp <= threshold)
            {
                TriggerUndying(character);
            }
        }

        private void OnHurtEvent(DamageInfo damageInfo)
        {
            if (_triggered || _owner == null) return;

            var health = _owner.Health;
            if (health == null || health.IsDead) return;

            float maxHp = health.MaxHealth;
            if (maxHp <= 0f) return;

            float currentHp = health.CurrentHealth;
            float threshold = maxHp * LowHpThresholdRatio;
            
            if (_lastHp > threshold && currentHp <= threshold)
            {
                TriggerUndying(_owner);
            }
        }

        private void TriggerUndying(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null || health.IsDead) return;

            _triggered = true;

            // 回血到目标比例
            float targetHp = health.MaxHealth * HealRatio;
            if (health.CurrentHealth < targetHp)
            {
                health.SetHealth(targetHp);
            }

            character.PopText(PopLineStart);
            StartInvincibility(character);
        }

        private void StartInvincibility(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null) return;

            _wasInvincible = health.Invincible;
            _isInvincible = true;
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
            if (health == null) return;
            
            health.SetInvincible(_wasInvincible);
            _isInvincible = false;
            
            if (!health.IsDead)
            {
                character.PopText(PopLineEnd);
            }
        }
    }
}