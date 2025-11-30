using System;
using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
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

        // private static readonly float ArmorReduction = -0.4f; 
        
        private readonly Lazy<string> _popTextFmt = new(() => 
            LocalizationManager.GetText("Affix_Tear_PopText_1")
        );
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null) return;

            try
            {
                // 削弱 10% - 40%
                float randomReduction = UnityEngine.Random.Range(-0.4f, -0.1f);

                var bodyArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.BodyArmor, 
                    randomReduction,
                    ModifierType.PercentageMultiply
                );
                var headArmorMod = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.HeadArmor,
                    randomReduction,
                    ModifierType.PercentageMultiply
                );

                if (bodyArmorMod != null && headArmorMod != null)
                {
                    int buffId = buff.GetInstanceID();
                    
                    var bodyStat = player.CharacterItem.GetStat(StatModifier.Attributes.BodyArmor);
                    var headStat = player.CharacterItem.GetStat(StatModifier.Attributes.HeadArmor);
                    
                    EliteBuffModifierManager.Instance.TrackModifier(buffId, bodyStat, bodyArmorMod);
                    EliteBuffModifierManager.Instance.TrackModifier(buffId, headStat, headArmorMod);
                    
                    string msg = string.Format(_popTextFmt.Value, (randomReduction * 100f).ToString("F1"));
                    player.PopText(msg);
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