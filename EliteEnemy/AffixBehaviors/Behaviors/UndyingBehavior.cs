using System;
using UnityEngine;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 词缀：不死（Undying）
    /// 效果：当生命值跌破阈值（20%）时，强制锁血并回血至 50%，同时获得 2.5 秒无敌。
    /// </summary>
    public class UndyingBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Undead";
        
        private static readonly float ThresholdRatio = 0.2f;  // 触发阈值 (20%)
        private static readonly float HealTargetRatio = 0.5f; // 回血目标 (50%)
        private static readonly float InvincibleDuration = 2.5f; // 无敌时间
        
        private CharacterMainControl _owner;
        private bool _triggered = false;       // 是否已触发（仅限一次）
        private bool _isInvincible = false;    // 当前是否处于词条赋予的无敌状态
        private bool _originalInvincibleState; // 记录触发前的无敌状态（用于还原）
        private float _invincibleEndTime;      // 无敌结束时间戳
        
        private readonly Lazy<string> _popLineStart = new(() => 
            LocalizationManager.GetText("Affix_Undead_PopText_1"));
        
        private readonly Lazy<string> _popLineEnd = new(() => 
            LocalizationManager.GetText("Affix_Undead_PopText_2"));

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null || character.Health == null) return;

            _owner = character;
            _triggered = false;
            _isInvincible = false;
            
            Health.OnHurt += OnGlobalHealthHurt;      
        }

        private void OnGlobalHealthHurt(Health health, DamageInfo damageInfo)
        {           
            if (_triggered || _owner == null || health != _owner.Health) return;
            
            float threshold = health.MaxHealth * ThresholdRatio;
            
            if (health.CurrentHealth <= threshold)
            {
                TriggerUndying(health);
            }
        }

        private void TriggerUndying(Health health)
        {
            _triggered = true;
    
            float targetHp = health.MaxHealth * HealTargetRatio;
            if (health.CurrentHealth < targetHp)
            {
                health.SetHealth(targetHp);
            }
    
            _originalInvincibleState = health.Invincible; 
            _isInvincible = true;
            _invincibleEndTime = Time.time + InvincibleDuration;
            health.SetInvincible(true);
    
            _owner.PopText(_popLineStart.Value);
    
            Modify(_owner, AIFieldModifier.Fields.ShootCanMove, 1f, false);
            Modify(_owner, AIFieldModifier.Fields.CanDash, 1f, false);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_isInvincible)
            {
                if (Time.time >= _invincibleEndTime)
                {
                    EndInvincibility();
                }
            }
        }

        private void EndInvincibility()
        {
            if (_owner != null && _owner.Health != null)
            {
                _owner.Health.SetInvincible(_originalInvincibleState);
                if (!_owner.Health.IsDead)
                {
                    _owner.PopText(_popLineEnd.Value);
                }
            }
            _isInvincible = false;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            Health.OnHurt -= OnGlobalHealthHurt;
            
            if (_isInvincible && character != null && character.Health != null)
            {
                character.Health.SetInvincible(_originalInvincibleState);
            }

            _owner = null;
            _isInvincible = false;
            _triggered = false;
        }
    }
}