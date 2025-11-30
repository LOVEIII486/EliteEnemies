using System;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 打滑效果实现：大幅降低加速度，模拟冰面滑行
    /// </summary>
    public class SlipperyEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.SlipperyEffect]";
        
        public string BuffName => "EliteBuff_Slippery";

        private static readonly float AccReduction = -0.9f; 
        private static readonly float SpeedMultiplier = 0.7f; 
        
        private readonly Lazy<string> _slipperyPopText = new(() =>
            LocalizationManager.GetText("Affix_Slippery_PopText_1")
        );

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} 玩家或CharacterItem为null");
                return;
            }

            try
            {
                int buffId = buff.GetInstanceID();

                ApplyAndTrack(player, buffId, StatModifier.Attributes.WalkAcc, AccReduction);
                ApplyAndTrack(player, buffId, StatModifier.Attributes.RunAcc, AccReduction);
                ApplyAndTrack(player, buffId, StatModifier.Attributes.WalkSpeed, SpeedMultiplier*2);
                ApplyAndTrack(player, buffId, StatModifier.Attributes.RunSpeed, SpeedMultiplier);
                player.PopText(_slipperyPopText.Value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用效果失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ApplyAndTrack(CharacterMainControl player, int buffId, string statName, float value)
        {
            var modifier = StatModifier.AddModifier(
                player,
                statName,
                value,
                ModifierType.PercentageMultiply
            );

            if (modifier != null)
            {
                var stat = player.CharacterItem.GetStat(statName);
                EliteBuffModifierManager.Instance.TrackModifier(buffId, stat, modifier);
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
                Debug.LogError($"{LogTag} 清理效果失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}