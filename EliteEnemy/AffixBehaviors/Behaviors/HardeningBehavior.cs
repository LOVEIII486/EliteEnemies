using System;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 硬化词条 - 受伤时随机降低物理伤害
    /// </summary>
    public class HardeningBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Hardening";

        // 每次触发减少 5%-12% 的物理伤害
        private static readonly float MinDamageReduction = 0.08f; // 最小减伤
        private static readonly float MaxDamageReduction = 0.2f; // 最大减伤
        private static readonly float MaxTotalReduction = 0.60f; // 总减伤上限
        private static readonly float TriggerCooldown = 0.6f; // 触发CD

        private float _accumulatedReduction = 0f; // 累计减伤百分比
        private float _lastTriggerTime = -999f; // 上次触发时间

        private readonly Lazy<string> _hardeningPopTextFmt =
            new(() => LocalizationManager.GetText("Affix_Hardening_PopText_1"));

        private string HardeningPopTextFmt => _hardeningPopTextFmt.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _accumulatedReduction = 0f;
            _lastTriggerTime = -999f;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null || character.Health == null)
                return;
            
            float currentTime = Time.time;
            if (currentTime - _lastTriggerTime < TriggerCooldown)
                return;
            
            if (_accumulatedReduction >= MaxTotalReduction)
                return;

            // 随机减伤增量（5% - 12%）
            float damageReduction = UnityEngine.Random.Range(MinDamageReduction, MaxDamageReduction);
            
            if (_accumulatedReduction + damageReduction > MaxTotalReduction)
            {
                damageReduction = MaxTotalReduction - _accumulatedReduction;
            }

            // 计算新的伤害倍率
            // 如果当前 ElementFactor_Physics 是 1.0
            // 减少 10% 意味着新倍率是 0.90
            float currentFactor = GetCurrentPhysicsFactor(character);
            float newFactor = currentFactor * (1f - damageReduction);
            
            // 使用 Set 方法直接设置新值，而不是 Multiply，避免累乘）
            StatModifier.Set(character, StatModifier.Attributes.ElementFactor_Physics, newFactor);
            
            _accumulatedReduction += damageReduction;
            _lastTriggerTime = currentTime;
            
            int percentDisplay = Mathf.RoundToInt(damageReduction * 100);
            string popText = string.Format(HardeningPopTextFmt, percentDisplay);
            character.PopText(popText);
            
            // Debug.Log($"[HardeningBehavior] 累计减伤: {(_accumulatedReduction * 100):F1}%, 本次: {percentDisplay}%, 当前倍率: {newFactor:F3}");
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _accumulatedReduction = 0f;
            _lastTriggerTime = -999f;
        }

        /// <summary>
        /// 获取当前的物理伤害系数
        /// </summary>
        private float GetCurrentPhysicsFactor(CharacterMainControl character)
        {
            if (character?.CharacterItem == null)
                return 1f;

            try
            {
                var stat = character.CharacterItem.GetStat(StatModifier.Attributes.ElementFactor_Physics);
                return stat?.Value ?? 1f;
            }
            catch
            {
                return 1f;
            }
        }
    }
}