using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// Modifier追踪和管理器
    /// </summary>
    public class EliteBuffModifierManager
    {
        private static EliteBuffModifierManager _instance;
        public static EliteBuffModifierManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EliteBuffModifierManager();
                return _instance;
            }
        }

        // 存储每个Buff实例的Modifier
        private Dictionary<int, List<(Stat stat, Modifier modifier)>> _buffModifiers 
            = new Dictionary<int, List<(Stat, Modifier)>>();

        /// <summary>
        /// 追踪Modifier
        /// </summary>
        public void TrackModifier(int buffInstanceId, Stat stat, Modifier modifier)
        {
            if (!_buffModifiers.ContainsKey(buffInstanceId))
            {
                _buffModifiers[buffInstanceId] = new List<(Stat, Modifier)>();
            }

            _buffModifiers[buffInstanceId].Add((stat, modifier));
        }

        /// <summary>
        /// 清理Buff的所有Modifier并移除追踪
        /// </summary>
        public void CleanupModifiers(int buffInstanceId)
        {
            if (_buffModifiers.TryGetValue(buffInstanceId, out var modifiers))
            {
                foreach (var (stat, modifier) in modifiers)
                {
                    if (stat != null && modifier != null)
                    {
                        stat.RemoveModifier(modifier);
                    }
                }

                _buffModifiers.Remove(buffInstanceId);
            }
        }

        /// <summary>
        /// 清空所有追踪
        /// </summary>
        public void Clear()
        {
            _buffModifiers.Clear();
        }
    }
}