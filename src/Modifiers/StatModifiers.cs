using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// Stat 修改器
    /// </summary>
    public static class StatModifiers
    {
        private const string LogTag = "[EliteEnemies.StatModifiers]";

        /// <summary>
        /// 为角色添加属性修改器并返回实例
        /// </summary>
        /// <param name="enemy">目标角色</param>
        /// <param name="statKey">属性 Key</param>
        /// <param name="value">修改数值 (例如 0.5f 代表 +50%)</param>
        /// <param name="type">修改类型 (默认百分比叠加)</param>
        /// <param name="source">来源标识 (用于后续批量移除，默认使用 EliteAffix 前缀)</param>
        /// <returns>创建的 Modifier 实例，用于精准移除</returns>
        public static Modifier AddModifier(CharacterMainControl enemy, string statKey, float value, ModifierType type = ModifierType.PercentageMultiply, object source = null)
        {
            if (enemy == null || enemy.CharacterItem == null) return null;

            if (source == null)
            {
                // 两个理由，缺一不可：
                //   1. 没有 source 就永远撤不掉——这是「属性修改永久残留」的根因之一。
                //   2. 游戏的 Modifier.ToString() 会调 source.ToString()，null 会在任何
                //      打印/调试路径上抛空引用。
                // 历史版本这里用 "EliteAffix_" + statKey 兜底，那是**运行时拼接**出来的新字符串，
                // 而游戏的撤销走的是引用比较（Stat.cs:167-172），照样撤不掉。
                Debug.LogError($"{LogTag} 未提供 source，该修改将无法撤销，已拒绝。statKey={statKey}");
                return null;
            }

            var modifier = new Modifier(type, value, source);

            // 用 Item.AddModifier 的返回值做**注册期校验**：key 不存在时它返回 false 而不抛异常
            // （Item.cs:1336-1349）。历史版本先 GetStat 再判空、判不到只打一条 warning，
            // 于是「常量写错」退化成运行时一条容易被忽略的日志——MoveSpeed 就是这样静默失效了很久。
            if (!enemy.CharacterItem.AddModifier(statKey, modifier))
            {
                Debug.LogError($"{LogTag} 角色 {enemy.name} 上没有属性 Key: {statKey}，这次修改不会生效。请核对 StatKeys 里的常量。");
                return null;
            }

            if (statKey == StatKeys.MaxHealth && enemy.Health != null)
            {
                // AddHealth 自身会 clamp 到 MaxHealth（Health.cs:480-483），
                // 所以这里等价于「补满到新的上限」。
                enemy.Health.AddHealth(enemy.Health.MaxHealth);
            }

            return modifier;
        }
        
        public static void RemoveModifier(CharacterMainControl enemy, string statKey, Modifier modifier)
        {
            if (enemy == null || enemy.CharacterItem == null || modifier == null) return;
            enemy.CharacterItem.GetStat(statKey)?.RemoveModifier(modifier);
        }

        /// <summary>
        /// 撤销某个来源加上的全部 Stat 修改——走游戏自带的 source 机制，不要自己记账。
        ///
        /// <para>⚠ 必须与 <see cref="AddModifier"/> 对称：历史版本里 Add 会退而写
        /// <c>StatCollection</c> 组件、而 Remove 只清 <c>CharacterItem</c>，
        /// 于是走那条分支加上的修改**永远清不掉**——当时 Buff 侧还得自己拿一张
        /// <c>(Stat, Modifier)</c> 表追踪（那个管理器已在 Buff 重构中删除，
        /// 因为现在只需一句 <c>RemoveAllModifiersFrom(buff)</c>）。
        /// 现在两条路都只认 <c>CharacterItem</c>。</para>
        /// </summary>
        public static void RemoveAllModifiersFromSource(CharacterMainControl enemy, object source)
        {
            if (enemy == null || enemy.CharacterItem == null || source == null) return;
            enemy.CharacterItem.RemoveAllModifiersFrom(source);
        }

    }
}
