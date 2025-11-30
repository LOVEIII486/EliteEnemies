using System;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using UnityEngine;
using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 硬化词条 - 受伤时随机降低物理伤害
    /// <para>机制：受伤积累减伤 -> 达到上限(60%) -> 持续30秒 -> 疲软10秒(0%) -> 重新积累</para>
    /// </summary>
    public class HardeningBehavior : AffixBehaviorBase, ICombatAffixBehavior, IUpdateableAffixBehavior
    {
        public override string AffixName => "Hardening";

        private enum HardeningState
        {
            Accumulating, // 积累阶段：受伤叠层
            MaxHardened,  // 硬化顶峰：持续保持满层
            Weakened      // 疲软阶段：失去所有层数
        }
        
        private static readonly float MinDamageReduction = 0.05f; // 单次最小减伤 5%
        private static readonly float MaxDamageReduction = 0.15f; // 单次最大减伤 15%
        private static readonly float MaxTotalReduction = 0.60f;  // 总减伤上限 60%
        private static readonly float TriggerCooldown = 0.5f;     // 触发CD
        
        private static readonly float DurationMaxHardened = 20f;  // 满层持续时间
        private static readonly float DurationWeakened = 10f;     // 疲软持续时间
        
        private HardeningState _currentState = HardeningState.Accumulating;
        private float _accumulatedReduction = 0f;
        private float _lastTriggerTime = -999f;
        private float _stateTimer = 0f;
        
        private Modifier _hardeningModifier;
        
        private readonly Lazy<string> _hardeningPopText = new(() => LocalizationManager.GetText("Affix_Hardening_PopText_1")); // "硬化 +{0}%"
        private readonly Lazy<string> _maxStateText = new(() => LocalizationManager.GetText("Affix_Hardening_Max"));
        private readonly Lazy<string> _weakenedStateText = new(() => LocalizationManager.GetText("Affix_Hardening_Weakened"));
        private readonly Lazy<string> _recoverStateText = new(() => LocalizationManager.GetText("Affix_Hardening_Recover"));

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            ResetState(character);
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 只有在积累阶段才叠层
            if (_currentState != HardeningState.Accumulating) return;

            // CD 检查
            if (Time.time - _lastTriggerTime < TriggerCooldown) return;

            // 增加减伤
            float added = UnityEngine.Random.Range(MinDamageReduction, MaxDamageReduction);
            _accumulatedReduction += added;
            _lastTriggerTime = Time.time;

            // 检查是否达到上限
            if (_accumulatedReduction >= MaxTotalReduction)
            {
                _accumulatedReduction = MaxTotalReduction;
                _currentState = HardeningState.MaxHardened;
                _stateTimer = 0f;
                character.PopText(_maxStateText.Value);
            }
            else
            {
                string msg = string.Format(_hardeningPopText.Value, (added * 100f).ToString("F1"));
                character.PopText(msg);
            }

            ApplyPhysicsFactorChange(character);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null) return;
            
            _stateTimer += deltaTime;

            switch (_currentState)
            {
                case HardeningState.Accumulating:
                    // 积累逻辑在 OnDamaged 中被动触发
                    break;

                case HardeningState.MaxHardened:
                    // 持续时间结束后，进入疲软
                    if (_stateTimer >= DurationMaxHardened)
                    {
                        EnterWeakenedState(character);
                    }
                    break;

                case HardeningState.Weakened:
                    // 疲软时间结束后，恢复积累
                    if (_stateTimer >= DurationWeakened)
                    {
                        EnterAccumulatingState(character);
                    }
                    break;
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            RemoveHardeningModifier(character);
            ResetState(null);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }
        

        private void EnterWeakenedState(CharacterMainControl character)
        {
            // 先清零数值，再应用。
            _accumulatedReduction = 0f;
            ApplyPhysicsFactorChange(character);

            _currentState = HardeningState.Weakened;
            _stateTimer = 0f;
            
            character.PopText(_weakenedStateText.Value);
        }

        private void EnterAccumulatingState(CharacterMainControl character)
        {
            _accumulatedReduction = 0f;
            ApplyPhysicsFactorChange(character);

            _currentState = HardeningState.Accumulating;
            _stateTimer = 0f;
            _lastTriggerTime = -999f;

            character.PopText(_recoverStateText.Value);
        }

        private void ResetState(CharacterMainControl character)
        {
            _accumulatedReduction = 0f;
            _currentState = HardeningState.Accumulating;
            _stateTimer = 0f;
            _lastTriggerTime = -999f;
            
            if (character != null)
            {
                RemoveHardeningModifier(character);
            }
        }
        

        private void ApplyPhysicsFactorChange(CharacterMainControl character)
        {
            RemoveHardeningModifier(character);
            
            // 在 StatModifier 系统中，AddModifier(PercentageMultiply, -0.6) 意味着 Base * (1 - 0.6)
            if (_accumulatedReduction > 0.001f)
            {
                _hardeningModifier = StatModifier.AddModifier(
                    character, 
                    StatModifier.Attributes.ElementFactor_Physics, 
                    -_accumulatedReduction, // 负数表示减少系数
                    ModifierType.PercentageMultiply
                );
            }
        }

        private void RemoveHardeningModifier(CharacterMainControl character)
        {
            if (_hardeningModifier != null)
            {
                StatModifier.RemoveModifier(character, StatModifier.Attributes.ElementFactor_Physics, _hardeningModifier);
                _hardeningModifier = null;
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
    }
}