using System;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 扭曲效果实现 - 降低子弹速度并使其偏转
    /// </summary>
    public class DistortionEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_Distortion";
        private static readonly float BulletSpeedReduction = -0.7f; // 减少 70% 速度
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            try
            {
                int buffId = buff.GetInstanceID();
                
                EliteBuffModifierManager.Instance.ApplyAndTrack(
                    player, 
                    buff, 
                    StatModifier.Attributes.BulletSpeedMultiplier, 
                    BulletSpeedReduction
                );

                BulletDeflectionTracker.Instance.RegisterDistortedPlayer(player, buffId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DistortionEffect] 应用失败: {ex.Message}");
            }
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            try
            {
                int buffId = buff.GetInstanceID();
                
                EliteBuffModifierManager.Instance.CleanupModifiers(buffId);
                BulletDeflectionTracker.Instance.UnregisterDistortedPlayer(player);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DistortionEffect] 清理失败: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 追踪受扭曲影响的玩家
    /// </summary>
    public class BulletDeflectionTracker
    {
        private static BulletDeflectionTracker _instance;
        public static BulletDeflectionTracker Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new BulletDeflectionTracker();
                return _instance;
            }
        }

        private System.Collections.Generic.HashSet<CharacterMainControl> _distortedPlayers 
            = new System.Collections.Generic.HashSet<CharacterMainControl>();

        public void RegisterDistortedPlayer(CharacterMainControl player, int buffId)
        {
            _distortedPlayers.Add(player);
        }

        public void UnregisterDistortedPlayer(CharacterMainControl player)
        {
            _distortedPlayers.Remove(player);
        }

        public bool IsPlayerDistorted(CharacterMainControl player)
        {
            return _distortedPlayers.Contains(player);
        }

        public void Clear()
        {
            _distortedPlayers.Clear();
        }
    }
}