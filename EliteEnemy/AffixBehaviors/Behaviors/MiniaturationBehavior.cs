using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【迷你化】词缀 - 敌人体型随机缩小，血量略微降低但速度提升
    /// </summary>
    public class MiniaturationBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Mini";

        private static readonly float MinSizeMultiplier = 0.4f; 
        private static readonly float MaxSizeMultiplier = 0.8f; 
        private static readonly float MinHealthMultiplier = 0.6f; 
        private static readonly float MaxHealthMultiplier = 0.9f; 
        private static readonly float SpeedMultiplier = 1.2f; 

        private float _actualSizeMultiplier = 1.0f; 

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            // 1. 随机生成体型倍率并计算血量比例
            _actualSizeMultiplier = Random.Range(MinSizeMultiplier, MaxSizeMultiplier);
            float sizeRatio = (_actualSizeMultiplier - MinSizeMultiplier) / (MaxSizeMultiplier - MinSizeMultiplier);
            float healthMul = Mathf.Lerp(MinHealthMultiplier, MaxHealthMultiplier, sizeRatio);

            // 2. 应用物理形变
            ApplySizeChange(character);
            
            // 3. 应用数值修改
            Modify(character, StatModifier.Attributes.MaxHealth, healthMul, true);
            Modify(character, StatModifier.Attributes.MoveSpeed, SpeedMultiplier, true);
        }

        private void ApplySizeChange(CharacterMainControl character)
        {
            if (character != null)
            {
                character.transform.localScale = Vector3.one * _actualSizeMultiplier;
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            if (character != null)
            {
                character.transform.localScale = Vector3.one;
            }

            _actualSizeMultiplier = 1.0f;
        }
    }
}