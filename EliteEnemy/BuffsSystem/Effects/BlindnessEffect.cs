using System;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 致盲效果实现
    /// </summary>
    public class BlindnessEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.BlindnessEffect]";
        public string BuffName => "EliteBuff_Blindness";

        private static readonly float ViewDistanceReduction = -0.85f; // 减少85%
        private static readonly float ViewAngleReduction = -0.6f;     // 减少60%
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            try
            {
                var manager = EliteBuffModifierManager.Instance;
                // 减少视野距离
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.ViewDistance, ViewDistanceReduction);
                // 减少视野角度
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.ViewAngle, ViewAngleReduction);
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