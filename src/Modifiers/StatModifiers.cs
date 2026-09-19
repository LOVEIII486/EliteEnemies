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
        
        /// <summary>
        /// 把「**外部把「含本模块百分比加成的上限」当成了基准**」这件事修正回来。
        ///
        /// <para><b>什么时候会需要它</b>：某些外部代码（实测是联机模组的
        /// <c>DifficultyManager.ApplyHealthMultiplier</c>）会把当前的
        /// <c>Health.MaxHealth</c> <b>当成"基准值"</b>乘以自己的系数，然后**写回
        /// <c>stat.BaseValue</c></b>。但那个"当前上限"里已经含了我们的百分比加成
        /// ⇒ 写回去之后 Stat 重算会**再乘一次**：</para>
        ///
        /// <code>
        /// 设计值：max = B(1+Σ)M      实际：max = B(1+Σ)²M      current = B(1+Σ)M
        /// ⇒ 血量比例恒为 1/(1+Σ)，**永远显示不满血**，而且比设计值肉 (1+Σ) 倍
        /// </code>
        ///
        /// <para><b>修法</b>：把多算的那份除回去（<c>BaseValue /= 修改器把基准放大的倍数</c>）。
        /// 那个倍数<b>直接问游戏要</b>（<c>Stat.Value / Stat.BaseValue</c>），
        /// 不自己按修改器列表算——游戏的 <c>Recalculate</c> 里
        /// <c>PercentageMultiply</c> 是**逐个相乘**（<c>Stat.cs:139</c>）而不是相加，
        /// 自己算极容易写成"求和"（本方法第一版就写错成那样）。
        /// 顺带这样也把**别人**加的百分比修改一起还原了，与"联机模组想要的最终上限"一致。</para>
        ///
        /// <para>⚠ 该还原只对**乘性**修改成立。本模块对 <c>MaxHealth</c> 只加
        /// <c>PercentageMultiply</c>；若日后有人往这个 Stat 上加 <c>Add</c>/<c>PercentageAdd</c>，
        /// 这条除法就不再是精确逆运算，要跟着改。</para>
        ///
        /// <para><b>判据（什么时候才该动手）</b>：<c>Health.defaultMaxHealth &gt; 0</c>。
        /// 该字段<b>游戏自己从不写</b>（全树只有声明与读取，见 <c>Health.cs:30</c>/<c>:113</c>），
        /// 只有 <c>HealthM.ForceSetHealth</c> 在"要改写上限"那条分支里写它。
        /// 所以它非零 ⇔ 有人改写过这只角色的血量基准。**没有这条判据就会把好数据改坏**：
        /// 外部没改写时除一次，等于把词条的血量加成整份抹掉。</para>
        /// </summary>
        /// <para>⚠️ <b>它不幂等</b>：连调两次会把加成反向吃掉一倍（第二次读到的
        /// <c>BaseValue</c> 已经是修好的那份，<c>factor</c> 却不变）。
        /// <b>调用方必须保证每只角色只调一次</b>——判据就只能由调用方记（本工程记在
        /// <c>Coop\CoopEliteSync</c> 的已修集合里）。</para>
        ///
        /// <returns><c>true</c> = 确实修正了（<paramref name="before"/>/<paramref name="after"/> 是基准值）。</returns>
        public static bool TryUnbakeMaxHealthBonus(CharacterMainControl enemy, out float before, out float after)
        {
            before = 0f;
            after = 0f;

            if (enemy == null || enemy.CharacterItem == null) return false;

            var health = enemy.Health;
            var stat = enemy.CharacterItem.GetStat(StatKeys.MaxHealth);
            if (health == null || stat == null) return false;

            if (health.defaultMaxHealth <= 0) return false;   // 基准没被外部改写 ⇒ 本来就对

            float unscaled = stat.BaseValue;
            if (unscaled <= 0.0001f) return false;

            // 修改器把基准放大了多少倍。**问游戏要**，不自己按修改器列表算——
            // 那里面有 order 分组与"逐个相乘"的语义（见方法注释）。
            float factor = stat.Value / unscaled;
            if (factor <= 1.0001f) return false;   // 没有放大 ⇒ 谈不上被重复计入

            before = unscaled;
            stat.BaseValue = unscaled / factor;
            after = stat.BaseValue;

            // ⚠ **必须主动通知**：修好之后 `current` 恰好**没变**（它本来就等于修好后的上限），
            //   所以 `SetHealth` 那种"值变了才触发"的路子在这里不响。而联机模组正是靠
            //   `Health.OnHealthChange` 把上限同步给客机的（`AISyncTracker`）——
            //   漏了这一步，客机会一直停在那个偏大的上限上。
            health.OnHealthChange?.Invoke(health);

            return true;
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
