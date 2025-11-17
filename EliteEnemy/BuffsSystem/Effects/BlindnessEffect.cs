using System;
using UnityEngine;
using Duckov.Buffs;
using ItemStatsSystem.Stats;

namespace EliteEnemies.BuffsSystem.Effects
{
    /// <summary>
    /// 致盲效果实现
    /// </summary>
    public class BlindnessEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.BlindnessEffect]";
        public string BuffName => "EliteBuff_Blindness";

        private static readonly float ViewDistanceReduction = -0.9f; // 减少90%
        private static readonly float ViewAngleReduction = -0.6f;     // 减少60%
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} 玩家或CharacterItem为null");
                return;
            }

            try
            {
                // AddModifier + 追踪
                var viewDistanceModifier = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.ViewDistance,
                    ViewDistanceReduction,
                    ModifierType.PercentageMultiply
                );

                var viewAngleModifier = StatModifier.AddModifier(
                    player,
                    StatModifier.Attributes.ViewAngle,
                    ViewAngleReduction,
                    ModifierType.PercentageMultiply
                );

                // 检查是否成功添加
                if (viewDistanceModifier == null || viewAngleModifier == null)
                {
                    Debug.LogWarning($"{LogTag} 添加Modifier失败");
                    return;
                }

                // 追踪Modifier以便后续清理
                int buffId = buff.GetInstanceID();
                var viewDistanceStat = player.CharacterItem.GetStat(StatModifier.Attributes.ViewDistance);
                var viewAngleStat = player.CharacterItem.GetStat(StatModifier.Attributes.ViewAngle);
                
                EliteBuffModifierManager.Instance.TrackModifier(buffId, viewDistanceStat, viewDistanceModifier);
                EliteBuffModifierManager.Instance.TrackModifier(buffId, viewAngleStat, viewAngleModifier);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用效果失败: {ex.Message}\n{ex.StackTrace}");
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