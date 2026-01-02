using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【再生】词缀
    /// </summary>
    public class RegenerationBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Regeneration";

        private const float HealInterval = 0.5f;          // 回血触发 CD
        private const float BaseHealAmount = 7f;         // 基础回血固定值
        private const float MaxHealthMultiplier = 5.0f;   // 疲劳阈值倍率
        private const float ExhaustionRatio = 0.2f;       // 疲劳后保留的比例

        private float _timer;
        private float _totalHealedAmount;
        private float _exhaustionThreshold;
        private bool _isExhausted;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null || character.Health == null) return;

            _timer = 0f;
            _totalHealedAmount = 0f;
            _isExhausted = false;

            // 计算疲劳阈值
            _exhaustionThreshold = character.Health.MaxHealth * MaxHealthMultiplier;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null || character.Health.IsDead) return;

            _timer += deltaTime;

            if (_timer >= HealInterval)
            {
                _timer = 0f;
                PerformRegeneration(character);
            }
        }

        private void PerformRegeneration(CharacterMainControl character)
        {
            var health = character.Health;
            
            if (health.CurrentHealth >= health.MaxHealth) return;

            float regenAmount = BaseHealAmount;

            // 检查疲劳逻辑
            if (_totalHealedAmount >= _exhaustionThreshold)
            {
                if (!_isExhausted)
                {
                    _isExhausted = true;
                    //Debug.Log($"[EliteEnemies] {character.name} 的再生能力已透支，进入疲劳状态。");
                }
                regenAmount *= ExhaustionRatio;
            }

            character.AddHealth(regenAmount);
            _totalHealedAmount += regenAmount;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            _timer = 0f;
            _totalHealedAmount = 0f;
            _isExhausted = false;
        }
    }
}