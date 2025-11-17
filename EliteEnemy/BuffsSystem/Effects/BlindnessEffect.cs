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

        private static readonly float ViewDistanceReduction = -0.9f; // 视野距离
        private static readonly float ViewAngleReduction = -0.6f; // 视野角度
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null || player.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} 玩家或CharacterItem为null");
                return;
            }

            try
            {
                // 获取视野相关Stat
                var viewDistanceStat = player.CharacterItem.GetStat("ViewDistance");
                var viewAngleStat = player.CharacterItem.GetStat("ViewAngle");

                if (viewDistanceStat == null || viewAngleStat == null)
                {
                    Debug.LogWarning($"{LogTag} 找不到视野Stat");
                    return;
                }

                // 创建Modifier
                var viewDistanceModifier = new Modifier(
                    ModifierType.PercentageMultiply,
                    ViewDistanceReduction,
                    player
                );

                var viewAngleModifier = new Modifier(
                    ModifierType.PercentageMultiply,
                    ViewAngleReduction,
                    player
                );

                // 应用Modifier
                viewDistanceStat.AddModifier(viewDistanceModifier);
                viewAngleStat.AddModifier(viewAngleModifier);

                // 追踪Modifier以便后续清理
                int buffId = buff.GetInstanceID();
                EliteBuffModifierManager.Instance.TrackModifier(buffId, viewDistanceStat, viewDistanceModifier);
                EliteBuffModifierManager.Instance.TrackModifier(buffId, viewAngleStat, viewAngleModifier);

                //Debug.Log($"{LogTag} 致盲效果已应用 - 视野距离: {viewDistanceStat.Value}, 视野角度: {viewAngleStat.Value}");
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