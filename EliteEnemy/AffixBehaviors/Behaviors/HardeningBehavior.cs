using System;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 硬化词条 - 受伤时随机降低物理伤害
    /// <para>机制更新：减伤达到上限后持续 60s，随后进入 15s 疲软期（失去加成），之后重新循环。</para>
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
        
        private static readonly float DurationMaxHardened = 30f;  // 满层持续时间
        private static readonly float DurationWeakened = 10f;     // 疲软期持续时间


        private float _accumulatedReduction = 0f; // 当前累计减伤百分比
        private float _lastTriggerTime = -999f;   // 上次触发时间
        private HardeningState _currentState = HardeningState.Accumulating;
        private float _stateTimer = 0f;           // 状态计时器
        private ItemStatsSystem.Stats.Modifier _hardeningModifier;


        private readonly Lazy<string> _hardeningPopTextFmt =
            new(() => LocalizationManager.GetText("Affix_Hardening_PopText_1")); // "硬化 +{0}%"
        private readonly Lazy<string> _maxStateText = 
            new(() => LocalizationManager.GetText("Affix_Hardening_Max")); // "绝对防御"
        private readonly Lazy<string> _weakenedStateText = 
            new(() => LocalizationManager.GetText("Affix_Hardening_Weakened")); // "防御崩溃"
        private readonly Lazy<string> _recoverStateText = 
            new(() => LocalizationManager.GetText("Affix_Hardening_Recover")); // "装甲重组"

        private string HardeningPopTextFmt => _hardeningPopTextFmt.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            ResetState();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null)
                return;

            switch (_currentState)
            {
                case HardeningState.MaxHardened:
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0)
                    {
                        EnterWeakenedState(character);
                    }
                    break;

                case HardeningState.Weakened:
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0)
                    {
                        EnterAccumulatingState(character);
                    }
                    break;
                
                case HardeningState.Accumulating:
                default:
                    break;
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null || character.Health == null)
                return;
            
            if (_currentState != HardeningState.Accumulating)
                return;
            
            float currentTime = Time.time;
            if (currentTime - _lastTriggerTime < TriggerCooldown)
                return;
            
            float reductionGain = UnityEngine.Random.Range(MinDamageReduction, MaxDamageReduction);
            
            bool reachCap = false;
            if (_accumulatedReduction + reductionGain >= MaxTotalReduction)
            {
                reductionGain = MaxTotalReduction - _accumulatedReduction;
                reachCap = true;
            }

            if (reductionGain > 0.001f)
            {
                ApplyPhysicsFactorChange(character);
                
                _accumulatedReduction += reductionGain;
                _lastTriggerTime = currentTime;

                int percentDisplay = Mathf.RoundToInt(reductionGain * 100);
                string popText = string.Format(HardeningPopTextFmt, percentDisplay);
                character.PopText(popText);
            }

            if (reachCap)
            {
                EnterMaxHardenedState(character);
            }
        }

        // 进入满硬化状态
        private void EnterMaxHardenedState(CharacterMainControl character)
        {
            _currentState = HardeningState.MaxHardened;
            _stateTimer = DurationMaxHardened;
            
            character.PopText(_maxStateText.Value); 
        }

        // 进入疲软状态
        private void EnterWeakenedState(CharacterMainControl character)
        {
            if (_accumulatedReduction > 0)
            {
                ApplyPhysicsFactorChange(character);
            }

            _currentState = HardeningState.Weakened;
            _stateTimer = DurationWeakened;
            _accumulatedReduction = 0f; // 清零记录，因为数值已经还原
            character.PopText(_weakenedStateText.Value);
        }

        // 重新进入积累状态
        private void EnterAccumulatingState(CharacterMainControl character)
        {
            _currentState = HardeningState.Accumulating;
            _accumulatedReduction = 0f;
            _lastTriggerTime = -999f;
            _stateTimer = 0f;
            character.PopText(_recoverStateText.Value);
        }

        private void ResetState()
        {
            _accumulatedReduction = 0f;
            _lastTriggerTime = -999f;
            _currentState = HardeningState.Accumulating;
            _stateTimer = 0f;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            // 修复：直接移除修改器，而不是通过反向乘法计算还原
            if (_hardeningModifier != null)
            {
                StatModifier.RemoveModifier(character, StatModifier.Attributes.ElementFactor_Physics, _hardeningModifier);
                _hardeningModifier = null;
            }
            
            ResetState();
        }

        // 辅助方法：修改物理抗性
        private void ApplyPhysicsFactorChange(CharacterMainControl character)
        {
            // 1. 先移除旧的修改器
            if (_hardeningModifier != null)
            {
                StatModifier.RemoveModifier(character, StatModifier.Attributes.ElementFactor_Physics, _hardeningModifier);
                _hardeningModifier = null;
            }

            // 2. 根据当前的减伤总层数，添加新的修改器
            if (_accumulatedReduction > 0.001f)
            {
                _hardeningModifier = StatModifier.AddModifier(
                    character, 
                    StatModifier.Attributes.ElementFactor_Physics, 
                    -_accumulatedReduction, 
                    ItemStatsSystem.Stats.ModifierType.PercentageMultiply
                );
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
    }
}