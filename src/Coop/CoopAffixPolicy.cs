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
        }

        public static void Uninstall()
        {
            EliteEnemyCore.RuntimeDisabledAffixesProvider = null;
        }

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
