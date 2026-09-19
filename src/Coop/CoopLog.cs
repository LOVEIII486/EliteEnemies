using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 联机模块的诊断输出。
    ///
    /// <para><b>为什么不能直接用 <c>Debug.Log</c></b>：联机模组会<b>换掉整个 Unity 日志处理器</b>。
    /// <c>CoopLogSystem.Install()</c>（它自己的 <c>Main\Loader\Mod.cs:124</c>）把
    /// <c>Debug.unityLogger.logHandler</c> 包成 <c>ReleaseLogHandler</c>，而那个包装器的放行条件是：</para>
    ///
    /// <code>
    /// BuildInfo.RuntimeVerboseLoggingEnabled == true
    ///   || (RuntimeCriticalLoggingEnabled &amp;&amp; (Error || Exception || Assert))
    /// </code>
    ///
    /// <para>发布版里实测是 <c>Verbose = false</c> / <c>Critical = true</c>，
    /// 于是 <b><c>Debug.Log</c> 与 <c>Debug.LogWarning</c> 全部被丢弃</b>——
    /// 影响的不是本模组一个，而是进程内所有模组的普通日志。</para>
    ///
    /// <para><b>实测证据</b>（2026-09-19）：在一次联机局（<c>ModActive_EscapeFromDuckovCoopMod: True</c>）
    /// 的 <c>Player.log</c> 里，<c>[EliteEnemies</c> 前缀命中 <b>0 次</b>——
    /// 而模组本身是正常加载的。它的启动日志全被这层过滤吃掉了。</para>
    ///
    /// <para>⚠ <b>这同时意味着本工程的 <c>SessionStats</c>（本局统计）在联机局里是看不见的</b>——
    /// 那个模块的设计目的是"提高玩家出错时那份日志的可分析率"，而在联机局里它的输出
    /// 恰恰到不了日志。<b>这是联机兼容要单独解决的一笔账</b>，见
    /// <c>docs\联机兼容可行性分析.md</c>。当前<b>不</b>动 <c>SessionStats</c>——
    /// 那是生产代码，不该为了探针改它。</para>
    ///
    /// <para><b>本类的取舍</b>：为了穿透过滤，这里一律走 <c>Debug.LogError</c>。
    /// 这**确实是在借用错误级别**，控制台里会显示为红色——这是<b>刻意的临时手段</b>，
    /// 因为第 0 期的探针必须能被看见，而看不见的探针等于没做。
    /// 前缀固定带 <c>[probe]</c> 标记，便于事后一键检索与删除。</para>
    ///
    /// <para>⚠ <b>第 1 期应当用一个真正属于本模组的日志文件取代它</b>
    /// （写到 <c>Application.persistentDataPath</c> 或模组目录），
    /// 那时就不必再借错误级别，也不再受联机模组的过滤影响。</para>
    /// </summary>
    internal static class CoopLog
    {
        /// <summary>固定前缀。<c>[probe]</c> 是本模块诊断输出的检索标记。</summary>
        public const string Prefix = "[EliteEnemies.Coop][probe]";

        public static void Emit(string message)
        {
            // 刻意用 LogError：只有 Error/Exception/Assert 能穿透联机模组的日志过滤。
            Debug.LogError($"{Prefix} {message}");
        }
    }
}
