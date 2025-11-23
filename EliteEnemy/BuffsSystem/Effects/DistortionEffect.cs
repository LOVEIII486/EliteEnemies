using System;
using UnityEngine;
using Duckov.Buffs;
using ItemStatsSystem.Stats;

namespace EliteEnemies.BuffsSystem.Effects
{
    /// <summary>
    /// 扭曲效果实现 - 降低子弹速度并使其偏转
    /// </summary>
    public class DistortionEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.DistortionEffect]";
        public string BuffName => "EliteBuff_Distortion";
        
        private static readonly float BulletSpeedReduction = -0.7f;
        
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
                
                var bulletSpeedStat = player.CharacterItem.GetStat("BulletSpeedMultiplier");
                if (bulletSpeedStat != null)
                {
                    var bulletSpeedMod = new Modifier(ModifierType.PercentageMultiply, BulletSpeedReduction, player);
                    bulletSpeedStat.AddModifier(bulletSpeedMod);
                    EliteBuffModifierManager.Instance.TrackModifier(buffId, bulletSpeedStat, bulletSpeedMod);
                }

                BulletDeflectionTracker.Instance.RegisterDistortedPlayer(player, buffId);
                
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
                BulletDeflectionTracker.Instance.UnregisterDistortedPlayer(player);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 清理效果失败: {ex.Message}\n{ex.StackTrace}");
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