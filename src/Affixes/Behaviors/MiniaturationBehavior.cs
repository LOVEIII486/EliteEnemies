using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
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
            // 与 Berserk / Gigantification 统一走同一个助手；`healToFull: false` 保持原语义
            CharacterModifiers.Quick.ModifyHealth(character, healthMul, AffixName, healToFull: false);
            // 移速走 ModifySpeed：MoveSpeed 不是游戏的属性 key，该方法同时写 WalkSpeed 与 RunSpeed
            CharacterModifiers.Quick.ModifySpeed(character, SpeedMultiplier, AffixName);
        }

        private void ApplySizeChange(CharacterMainControl character)
        {
            if (character != null)
            {
                character.transform.localScale = Vector3.one * _actualSizeMultiplier;
            }
        }



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