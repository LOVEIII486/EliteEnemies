using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Stats;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【巨大化】词缀 - 敌人体型随机放大，获得血量加成但速度减半
    /// </summary>
    public class GigantificationBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Giant";
 
        private static readonly float MinSizeMultiplier = 1.5f; // 最小体型倍率
        private static readonly float MaxSizeMultiplier = 4.0f; // 最大体型倍率
        private static readonly float MinHealthMultiplier = 1.5f; // 最小血量倍率
        private static readonly float MaxHealthMultiplier = 3.5f; // 最大血量倍率
        private static readonly float SpeedMultiplier = 0.8f; // 速度减益

        private float _actualSizeMultiplier = 1.0f; // 实际应用的体型倍率
        private float _actualHealthMultiplier = 1.0f; // 实际应用的血量倍率

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            // 随机生成初始体型倍率
            _actualSizeMultiplier = Random.Range(MinSizeMultiplier, MaxSizeMultiplier);

            // 根据体型计算血量倍率（线性映射）
            float sizeRatio = (_actualSizeMultiplier - MinSizeMultiplier) / (MaxSizeMultiplier - MinSizeMultiplier);
            _actualHealthMultiplier = Mathf.Lerp(MinHealthMultiplier, MaxHealthMultiplier, sizeRatio);

            ApplySizeChange(character);
            ApplyStatChanges(character);

            // Debug.Log($"[GigantificationBehavior] {character.name} 巨大化完成：" +
            //           $"体型 {_actualSizeMultiplier:F2}x，血量 {_actualHealthMultiplier:F2}x，速度 {SpeedMultiplier:F2}x");
        }
        
        private void ApplySizeChange(CharacterMainControl character)
        {
            // 修改 transform.localScale 来改变体型
            if (character.transform != null)
            {
                character.transform.localScale = Vector3.one * _actualSizeMultiplier;
            }
        }
        
        private void ApplyStatChanges(CharacterMainControl character)
        {
            AttributeModifier.Quick.ModifyHealth(character, _actualHealthMultiplier, healToFull: true);
            AttributeModifier.Quick.ModifySpeed(character, SpeedMultiplier);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _actualSizeMultiplier = 1.0f;
            _actualHealthMultiplier = 1.0f;
        }
    }
}