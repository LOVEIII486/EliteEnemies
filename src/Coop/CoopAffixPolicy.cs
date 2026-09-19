using System;
using System.Collections.Generic;
using EliteEnemies.Core;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// **联机模式下禁止出现**的词条名单。
    ///
    /// <para><b>为什么需要一份"禁"而不是"逐个修"</b>：有些词条的效果在联机模型下
    /// <b>没有正确的语义</b>——不是"实现有 bug"，而是"这件事本身没法在两台机器上同时成立"。
    /// 硬修要么做不到、要么会引出更难查的问题，那就不如<b>干脆不让它出现</b>。</para>
    ///
    /// <para>名单只在**主机侧选词条时**生效（客户端本来就不选词条）。
    /// 单机下整个机制不安装 ⇒ <b>单机行为一字不差</b>。</para>
    ///
    /// <para>⚠ <b>往名单里加条目时必须写明理由</b>——"在联机下坏了"不够，
    /// 要写清是哪一层坏（全局资源？到不了真人？），否则日后没人敢删。</para>
    /// </summary>
    internal static class CoopAffixPolicy
    {
        /// <summary>
        /// 联机下禁用的词条。键用 <c>OrdinalIgnoreCase</c>，与词条名的实际大小写无关。
        /// </summary>
        private static readonly HashSet<string> DisabledInCoop =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // ── 全局时间流速 ──
                //
                // 时停直接写全局 `TimeScaleManager.devCambulletTimeScale`（TimeStopBehavior.cs:104）。
                // 而时间流速是**每台机器各自的全局资源**：
                //   · 不同步 ⇒ 主机停了、客机照常跑，**词条对客机等于不存在**
                //   · 同步   ⇒ 连**没参战的那个玩家**也一起被拖进时停
                // 两种都不是想要的效果，而且联机模组那边的 `EnvClockStateRpc` 也带 TimeScale
                // 字段（管的是游戏时钟），两边谁覆盖谁**未核实**。
                //
                // 这条词条在联机下**没有正确的语义**，故禁用（作者 2026-09-19 定）。
                "TimeStop",
            };

        public static void Install()
        {
            EliteEnemyCore.RuntimeDisabledAffixesProvider = Current;
            EliteEnemyCore.SpawnedPropsAreShared = LocalPropsShared;
        }

        public static void Uninstall()
        {
            EliteEnemyCore.RuntimeDisabledAffixesProvider = null;
            EliteEnemyCore.SpawnedPropsAreShared = null;
        }

        /// <summary>
        /// 本机临时生成的场景物件**过不了网**（联机模组只同步官方建箱路径造出来的箱子）。
        ///
        /// <para><b>为什么不是把拟态整个禁掉</b>：它只有"补给箱"那种形态过不了网，
        /// 另一种（地上的物品）走的是游戏自己的 <c>ItemExtensions.Drop</c>，
        /// 而联机模组**补丁了那条路** ⇒ 客机看得到诱饵、触发在主机侧判距离、现形后取消隐身。
        /// 详见 <c>EliteEnemyCore.SpawnedPropsAreShared</c> 与 <c>MimicBehavior</c> 的形态注释。</para>
        ///
        /// <para>判据同 <see cref="Current"/>：<c>Active &amp;&amp; NetworkStarted</c>。
        /// <b>少了 NetworkStarted 那一项</b>，"装了联机模组却自己单机玩"的玩家
        /// 就会莫名其妙只遇到一种拟态形态——不报错、不留日志。</para>
        /// </summary>
        private static bool LocalPropsShared()
            => !CoopApi.Active || !CoopApi.NetworkStarted;

        /// <summary>
        /// **现问现答**：只有"联机**真的已启动**"时才返回禁用名单。
        ///
        /// <para>⚠️ <b>不能只判 <c>CoopApi.Active</c></b>——那只表示"联机模组的 API 接上了"。
        /// 玩家<b>装了联机模组却自己单机玩</b>时它同样为 true，
        /// 那样这些词条会被**莫名其妙地禁掉**，而且不报错、不留日志。
        /// 判据与"精英逻辑权威"用的是同一个：<c>Active &amp;&amp; NetworkStarted</c>。</para>
        /// </summary>
        private static ICollection<string> Current()
        {
            if (!CoopApi.Active || !CoopApi.NetworkStarted) return null;

            return DisabledInCoop;
        }
    }
}
