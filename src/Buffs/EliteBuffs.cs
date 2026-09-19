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
        /// 给**被打中的那个玩家**施加一个 Buff。<paramref name="from"/> 只作来源记录。
        ///
        /// <para><b><paramref name="victim"/> 是目标，不是来源。</b>
        /// 原型（<c>CharacterMainControl.Main</c>）对单人是对的、对**联机是错的**：
        /// 词条的判定在**主机**上跑，而主机上挨打的可能是**别的玩家**的复制体——
        /// 挂到 <c>Main</c> 就等于"精英打中客机、debuff 却挂到主机玩家身上"，
        /// 客机什么都看不到。所以目标必须由调用方给出（来自
        /// <c>OnHitPlayer</c> 的 <c>victim</c> 参数）。</para>
        ///
        /// <para>⚠ 传入的 <paramref name="victim"/> 若为 <c>null</c>，会**退回 <c>Main</c> 并告警**——
        /// 那是"取不到受害者"的兜底，不是正常路径。单人下它总是本机玩家，行为与从前完全一致。</para>
        /// </summary>
        public static bool ApplyToPlayer<T>(CharacterMainControl victim, CharacterMainControl from = null)
            where T : EliteBuffBase
        {
            var player = victim;

            if (player == null)
            {
                player = CharacterMainControl.Main;
                Debug.LogWarning($"{LogTag} 施加 {typeof(T).Name} 时拿不到受害者，退回本机玩家" +
                                 "（联机下这可能意味着挂错了人，请检查 OnHitPlayer 的 victim 参数）");
            }

            if (player == null)
            {
                Debug.LogWarning($"{LogTag} 找不到玩家，无法施加 {typeof(T).Name}");
                return false;
            }

            return Apply<T>(player, from);
        }
    }
}
