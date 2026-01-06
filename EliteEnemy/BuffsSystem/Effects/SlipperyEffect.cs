using System;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;
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
            if (player == null) return;

            try
            {
                var manager = EliteBuffModifierManager.Instance;

                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.WalkAcc, AccReduction);
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.RunAcc, AccReduction);

                // WalkSpeed 增加 40% (即 1.4x)，RunSpeed 降低 30% (即 0.7x)
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.WalkSpeed, SpeedMultiplier * 2 - 1f); 
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.RunSpeed, SpeedMultiplier - 1f);

                player.PopText(_slipperyPopText.Value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用效果失败: {ex.Message}");
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
                Debug.LogError($"{LogTag} 清理效果失败: {ex.Message}");
            }
        }
    }
}