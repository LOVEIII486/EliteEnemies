using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
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
            // （标记由框架解析好，不再自己 GetComponent——见 AffixContext）
            var marker = Ctx?.Marker;
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
            // 与 Berserk 统一走同一个助手（它就是"按 PercentageMultiply 改 MaxHealth"）。
            // `healToFull: false` **保持原语义**——本词条不补血；要不要补血是玩法问题，
            // 不该在统一调用路径时顺手改掉。
            CharacterModifiers.Quick.ModifyHealth(character, actualHealthMultiplier, AffixName, healToFull: false);
            // 移速走 ModifySpeed：MoveSpeed 不是游戏的属性 key，该方法同时写 WalkSpeed 与 RunSpeed
            CharacterModifiers.Quick.ModifySpeed(character, SpeedMultiplier, AffixName);
        }
        
        private void ApplySizeChange(CharacterMainControl character)
        {
            if (character != null)
            {
                character.transform.localScale = Vector3.one * _actualSizeMultiplier;

                // 体型是 Transform，**不在** 联机模组的 AISyncEntry 里 ⇒ 不转交的话客机看不到。

                PlayerEffectRelay.RelayEliteVisual(character, character.transform.localScale, false);
            }
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            if (character != null)
            {
                character.transform.localScale = Vector3.one;

                PlayerEffectRelay.RelayEliteVisual(character, character.transform.localScale, false);
            }
            
            _actualSizeMultiplier = 1.0f;
        }
    }
}