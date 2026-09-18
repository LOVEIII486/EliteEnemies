using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【再生】词缀
    /// </summary>
    public class RegenerationBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Regeneration";

        private const float HealInterval = 0.5f;          // 回血触发 CD
        // ⚠ 数值与文案绑定：每秒回复 ≈ BaseHealAmount / HealInterval，
        // 描述里写的是「每秒回复约 10 点」⇒ 5 / 0.5 = 10。改这里就要同步改
        // ChineseSimplified.csv 的 `EliteEnemies_Affix_Regeneration_Description`
        // 与另外三个语言文件（否则玩家看到的数字与实测对不上）。
        private const float BaseHealAmount = 5f;         // 基础回血固定值
        private const float MaxHealthMultiplier = 5.0f;   // 疲劳阈值倍率
        private const float ExhaustionRatio = 0.2f;       // 疲劳后保留的比例

        private float _timer;
        private float _totalHealedAmount;
        private float _exhaustionThreshold;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null || character.Health == null) return;

            _timer = 0f;
            _totalHealedAmount = 0f;

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
            // 疲劳：累计回复量超过阈值后，之后的每次回复量打折。
            // ⚠ 记账用的是**请求量**（`regenAmount`），而实际回复量会被两件事改写：
            // `AddHealth` 内部先乘 `(1f + HealGain)`（CharacterMainControl.cs:2376）、
            // `Health.AddHealth` 再按 MaxHealth 截断（Health.cs:480-482）。
            // 所以这是**近似**——上界 5×最大生命只是近似值，不是精确的"实际回复量"。
            if (_totalHealedAmount >= _exhaustionThreshold)
            {
                regenAmount *= ExhaustionRatio;
            }

            character.AddHealth(regenAmount);
            _totalHealedAmount += regenAmount;
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            _timer = 0f;
            _totalHealedAmount = 0f;
        }
    }
}