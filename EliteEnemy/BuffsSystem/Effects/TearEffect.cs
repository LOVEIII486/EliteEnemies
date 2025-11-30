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
        private static readonly float ArmorReduction = -0.4f; 

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null) return;

            try
            {
                var bodyArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.BodyArmor, // 对应身体护甲
                    ArmorReduction,
                    ModifierType.PercentageMultiply
                );
                var headArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.HeadArmor, // 对应头部护甲
                    ArmorReduction,
                    ModifierType.PercentageMultiply
                );
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