using System.Reflection;
using UnityEngine;
using ItemStatsSystem;
using EliteEnemies.AffixBehaviors;
using ItemStatsSystem.Stats;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【迷你化】词缀 - 敌人体型随机缩小，血量略微降低但速度不变
    /// </summary>
    public class MiniaturationBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Mini";

        private static readonly float MinSizeMultiplier = 0.4f; // 最小体型倍率
        private static readonly float MaxSizeMultiplier = 0.8f; // 最大体型倍率
        private static readonly float MinHealthMultiplier = 0.5f; // 最小血量倍率
        private static readonly float MaxHealthMultiplier = 0.9f; // 最大血量倍率
        private static readonly float SpeedMultiplier = 1.2f; // 速度

        private float _actualSizeMultiplier = 1.0f; // 实际应用的体型倍率
        private float _actualHealthMultiplier = 1.0f; // 实际应用的血量倍率

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            // 随机生成体型倍率
            _actualSizeMultiplier = Random.Range(MinSizeMultiplier, MaxSizeMultiplier);

            // 根据体型计算血量倍率（线性映射）
            float sizeRatio = (_actualSizeMultiplier - MinSizeMultiplier) / (MaxSizeMultiplier - MinSizeMultiplier);
            _actualHealthMultiplier = Mathf.Lerp(MinHealthMultiplier, MaxHealthMultiplier, sizeRatio);

            ApplySizeChange(character);
            ApplyStatChanges(character);

            //Debug.Log($"[MiniaturationBehavior] {character.name} 迷你化完成：" +
            //          $"体型 {_actualSizeMultiplier:F2}x，血量 {_actualHealthMultiplier:F2}x，速度 {SpeedMultiplier:F2}x");
        }

        private void ApplySizeChange(CharacterMainControl character)
        {
            if (character.transform != null)
            {
                character.transform.localScale = Vector3.one * _actualSizeMultiplier;
            }
        }

        private void ApplyStatChanges(CharacterMainControl character)
        {
            var health = character.Health;
            if (health == null) return;


            var itemField = typeof(Health).GetField("item",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);

            var item = itemField?.GetValue(health);
            if (item == null)
            {
                Debug.LogWarning($"[MiniaturationBehavior] {character.characterPreset.nameKey} 没有有效的 item，跳过属性修改");
                return;
            }

            var getStat = item.GetType().GetMethod("GetStat", new[] { typeof(string) });
            if (getStat == null)
            {
                return;
            }

            void AddModifierToStat(string statName, float multiplier)
            {
                var statObj = getStat.Invoke(item, new object[] { statName });
                if (statObj == null) return;

                var addMod = statObj.GetType().GetMethod("AddModifier", new[] { typeof(Modifier) });
                if (addMod == null) return;

                float delta = multiplier - 1f;
                if (Mathf.Approximately(delta, 0f)) return;

                addMod.Invoke(statObj, new object[]
                {
                    new Modifier(ModifierType.PercentageMultiply, delta, character)
                });
            }
            
            AddModifierToStat("MaxHealth", _actualHealthMultiplier);
            AddModifierToStat("WalkSpeed", SpeedMultiplier);
            AddModifierToStat("RunSpeed", SpeedMultiplier);
            health.SetHealth(health.MaxHealth);
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