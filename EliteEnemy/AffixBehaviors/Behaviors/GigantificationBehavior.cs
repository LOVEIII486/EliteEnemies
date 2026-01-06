using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【巨大化】词缀 - 敌人体型随机放大，获得血量加成但速度减半
    /// </summary>
    public class GigantificationBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Giant";
 
        private static readonly float MinSizeMultiplier = 1.5f;   // 最小体型倍率
        private static readonly float MaxSizeMultiplier = 3.5f;   // 最大体型倍率
        private static readonly float MinHealthMultiplier = 1.5f; // 最小血量倍率
        private static readonly float MaxHealthMultiplier = 3.5f; // 最大血量倍率
        private static readonly float SpeedMultiplier = 0.8f;    // 速度减益

        private float _actualSizeMultiplier = 1.0f; 

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            float finalMaxScale = MaxSizeMultiplier;
            
            // 兼容性检查：如果同时拥有史莱姆词缀，限制最大体型防止模型穿模
            var marker = character.GetComponent<EliteEnemies.EliteEnemy.Core.EliteEnemyCore.EliteMarker>();
            if (marker != null && marker.Affixes.Contains("Slime"))
            {
                finalMaxScale = 2.8f;
            }

            // 1. 计算随机缩放
            _actualSizeMultiplier = Random.Range(MinSizeMultiplier, finalMaxScale);
            
            // 2. 应用物理形变
            ApplySizeChange(character);
            
            // 3. 根据缩放比例计算血量加成
            float sizeRatio = (_actualSizeMultiplier - MinSizeMultiplier) / (MaxSizeMultiplier - MinSizeMultiplier);
            float actualHealthMultiplier = Mathf.Lerp(MinHealthMultiplier, MaxHealthMultiplier, sizeRatio);
            Modify(character, StatModifier.Attributes.MaxHealth, actualHealthMultiplier, true);
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