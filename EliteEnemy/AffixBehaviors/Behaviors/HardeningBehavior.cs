using System;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 硬化词条 - 受伤时随机降低物理伤害
    /// <para>机制：受伤积累减伤 -> 达到上限(50%) -> 持续20秒 -> 疲软10秒(0%) -> 重新积累</para>
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
        private static readonly float MaxTotalReduction = 0.5f;   // 总减伤上限 50%
        private static readonly float TriggerCooldown = 0.5f;     // 触发CD
        
        private static readonly float DurationMaxHardened = 20f;  // 满层持续时间
        private static readonly float DurationWeakened = 10f;     // 疲软持续时间
        
        private static readonly float MinPhysicsFactorLimit = 0.01f;
        
        private HardeningState _currentState = HardeningState.Accumulating;
        private float _accumulatedReduction = 0f;
        private float _lastTriggerTime = -999f;
        private float _stateTimer = 0f;
        
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
            // 利用新框架助手方法一键清理该词缀产生的所有修改
            ClearBaseModifiers(character);
            ResetState(null);
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
                ClearBaseModifiers(character);
            }
        }

        private void ApplyPhysicsFactorChange(CharacterMainControl character)
        {
            // 每次应用前清理旧的修改，确保计算基准值的准确性
            ClearBaseModifiers(character);

            if (_accumulatedReduction <= 0.001f) { return; }
            
            if (character.CharacterItem == null) return;
            var physicsStat = character.CharacterItem.GetStat(StatModifier.Attributes.ElementFactor_Physics);
            
            if (physicsStat == null) return;
            
            float currentFactor = physicsStat.Value;
            float maxAllowedReduction = currentFactor - MinPhysicsFactorLimit;

            // 如果当前系数已经很低，则不允许再减
            if (maxAllowedReduction < 0f) { maxAllowedReduction = 0f; }
            
            float finalReduction = Mathf.Min(_accumulatedReduction, maxAllowedReduction);

            if (finalReduction > 0.001f)
            {
                // 应用新框架方法：传入最终倍率 (1.0 - 减伤率)
                // 内部会自动处理为负值的 PercentageMultiply 修改
                Modify(character, StatModifier.Attributes.ElementFactor_Physics, 1.0f - finalReduction, true);
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
    }
}