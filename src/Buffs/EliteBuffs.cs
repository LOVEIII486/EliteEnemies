using System;
using UnityEngine;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// Buff 模块的门面：把「施加一个 Buff」变成**一句强类型调用**。
    ///
    /// <para>调用方不再需要持有模板、不再需要记住名字或数字 ID、不再需要自己判空——
    /// 模板由 <see cref="EliteBuffRegistry"/> 统一持有：</para>
    ///
    /// <code>
    /// // 历史形态（三处字符串/数字耦合 + 手工缓存模板）
    /// private static readonly string BuffName = "EliteBuff_Slow";
    /// private static readonly int    BuffId   = 99902;
    /// private static readonly float  BuffDuration = 5f;
    /// private static readonly EliteBuffFactory.BuffConfig BuffConfig = new(BuffName, BuffId, BuffDuration);
    /// private static Buff _sharedBuff;
    /// ... OnEliteInitialized: _sharedBuff = EliteBuffFactory.GetOrCreateSharedBuff(BuffConfig);
    /// ... OnHitPlayer:        EliteBuffFactory.TryAddBuffToPlayer(_sharedBuff, attacker);
    ///
    /// // 现在
    /// EliteBuffs.Apply&lt;SlowBuff&gt;(attacker);
    /// </code>
    /// </summary>
    public static class EliteBuffs
    {
        private const string LogTag = "[EliteEnemies.EliteBuffs]";

        /// <summary>
        /// 给 <paramref name="target"/> 施加一个 Buff。模板由注册表持有，调用方不需要缓存任何东西。
        /// </summary>
        /// <param name="target">挂到谁身上（通常是玩家）</param>
        /// <param name="from">来源角色，可空（用于"谁给的"这类显示与统计）</param>
        /// <returns>是否成功发起施加</returns>
        public static bool Apply<T>(CharacterMainControl target, CharacterMainControl from = null)
            where T : EliteBuffBase
        {
            if (target == null)
            {
                Debug.LogWarning($"{LogTag} 目标为空，无法施加 {typeof(T).Name}");
                return false;
            }

            var template = EliteBuffRegistry.TemplateOf<T>();
            if (template == null)
            {
                // 没登记 = 构建期漏了（没继承 EliteBuffBase、没有无参构造器、或没通过 ID 自检）。
                // **必须报出来**——静默什么都不做正是本项目一路在清的东西。
                Debug.LogError($"{LogTag} 类型 {typeof(T).Name} 没有登记，无法施加。" +
                               "请确认它继承自 EliteBuffBase，并查看启动日志里 BuffRegistry 的自检输出。");
                return false;
            }

            try
            {
                // 注意这里**只传两个参数**：游戏的签名是
                //   AddBuff(Buff buffPrefab, CharacterMainControl fromWho = null, int overrideWeaponID = 0)
                // 第三个参数是"覆盖武器 ID"，不是层数。历史实现把 stackCount 传了进去——
                // 那是个一直在传 0 的无效参数，此处不再保留。
                target.AddBuff(template, from);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 施加 {typeof(T).Name} 失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 给**玩家**施加一个 Buff。<paramref name="from"/> 只作来源记录。
        ///
        /// <para>⚠ 单独开这个方法是因为历史实现里踩过一个坑：旧的
        /// <c>TryAddBuffToPlayer(buff, attacker)</c> 的**目标永远是 <c>CharacterMainControl.Main</c>**，
        /// 第二个参数只是"谁给的"。而词条回调 <c>OnHitPlayer(attacker, …)</c> 传进来的
        /// <c>attacker</c> 是**精英自己**——若照着"Apply 到第一个参数"直译，debuff 会挂到精英身上。
        /// 把目标写进方法名，这类误译就不容易再发生。</para>
        /// </summary>
        public static bool ApplyToPlayer<T>(CharacterMainControl from = null) where T : EliteBuffBase
        {
            var player = CharacterMainControl.Main;
            if (player == null)
            {
                Debug.LogWarning($"{LogTag} 找不到玩家，无法施加 {typeof(T).Name}");
                return false;
            }

            return Apply<T>(player, from);
        }
    }
}
