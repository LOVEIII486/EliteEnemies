using System.Collections.Generic;
using EliteEnemies.EliteEnemy.AttributeModifier;
using ItemStatsSystem;
using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.BuffsSystem
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
        /// 应用属性修改并自动开启追踪
        /// </summary>
        /// <param name="player">目标玩家</param>
        /// <param name="buff">Buff 实例</param>
        /// <param name="statName">属性常量 (来自 StatModifier.Attributes)</param>
        /// <param name="value">增量数值</param>
        /// <param name="type">修改模式 (默认为百分比乘法)</param>
        public void ApplyAndTrack(CharacterMainControl player, Duckov.Buffs.Buff buff, string statName, float value, ModifierType type = ModifierType.PercentageMultiply)
        {
            if (player?.CharacterItem == null || buff == null) return;

            var modifier = StatModifier.AddModifier(player, statName, value, type);

            if (modifier != null)
            {
                var stat = player.CharacterItem.GetStat(statName);
                if (stat != null)
                {
                    TrackModifier(buff.GetInstanceID(), stat, modifier);
                }
            }
        }
        
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