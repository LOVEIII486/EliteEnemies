using System;
using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 撕裂效果：削弱护甲
    /// </summary>
    public class TearEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.TearEffect]";
        public string BuffName => "EliteBuff_Tear";

        // 削弱幅度：-0.4f 表示削弱 40% 的护甲值
        // 如果想改为固定减少护甲等级（如减少2级），可改为 -2f 并将 ModifierType 改为 Add
        private static readonly float ArmorReduction = -0.4f; 

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null) return;

            try
            {
                // 1. 添加身体护甲削弱
                // 注意：这里假设 StatModifier.Attributes 中包含 BodyArmor。
                // 如果没有，请检查 StatModifier.cs 或使用字符串 "BodyArmor" 获取 Stat
                var bodyArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.BodyArmor, // 对应身体护甲
                    ArmorReduction,
                    ModifierType.PercentageMultiply
                );

                // 2. 添加头部护甲削弱
                var headArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.HeadArmor, // 对应头部护甲
                    ArmorReduction,
                    ModifierType.PercentageMultiply
                );

                // 3. 追踪 Modifier 以便清理
                if (bodyArmorMod != null && headArmorMod != null)
                {
                    int buffId = buff.GetInstanceID();
                    
                    var bodyStat = player.CharacterItem.GetStat(StatModifier.Attributes.BodyArmor);
                    var headStat = player.CharacterItem.GetStat(StatModifier.Attributes.HeadArmor);
                    
                    EliteBuffModifierManager.Instance.TrackModifier(buffId, bodyStat, bodyArmorMod);
                    EliteBuffModifierManager.Instance.TrackModifier(buffId, headStat, headArmorMod);
    
                    player.PopText("护甲撕裂!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用撕裂效果失败: {ex.Message}");
            }
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            try
            {
                int buffId = buff.GetInstanceID();
                EliteBuffModifierManager.Instance.CleanupModifiers(buffId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 清理撕裂效果失败: {ex.Message}");
            }
        }
    }
}