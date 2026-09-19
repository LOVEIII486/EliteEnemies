using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 联机模块的诊断输出——**写自己的文件，不写 <c>Player.log</c>**。
    ///
    /// <para><b>为什么不写 <c>Player.log</c></b>：</para>
    ///
    /// <para>1. <b>联机模组会把普通日志全部吞掉。</b>
    /// <c>CoopLogSystem.Install()</c>（<c>Main/Diagnostics/CoopLogSystem.cs:9</c>，
    /// 调用点 <c>Main/Loader/Mod.cs:124</c>）把 <c>Debug.unityLogger.logHandler</c> 换成只放行
    /// Error/Exception/Assert 的版本，而发布版实测 <c>RuntimeVerboseLoggingEnabled = false</c>
    /// （<c>BuildInfo.cs:8-9</c>）。所以 <c>Debug.Log</c> / <c>Debug.LogWarning</c> 在联机局里
    /// <b>一个字都进不去</b>。详见 <c>Docs\Coop\05-integration-gotchas.md</c> §1。</para>
    ///
    /// <para>2. <b>借 <c>Debug.LogError</c> 穿透的代价太大。</b>
    /// 实测（2026-09-19）每条 <c>LogError</c> 在 <c>Player.log</c> 里带
    /// <b>约 37 行托管堆栈</b>——42 条消息就吃掉 1691 行日志的 <b>92%</b>，
    /// 把联机模组自己的输出与真正要看的东西全挤掉了。
    /// 本模块要记的是「哪个 aiId 带了哪些词条」这类**短事实**，堆栈毫无价值。</para>
    ///
    /// <para><b>所以</b>：详细诊断一律进本文件，<c>Player.log</c> 里只留**一行指针**
    /// （见 <see cref="Announce"/>），让排查的人知道去哪儿找。</para>
    ///
    /// <para>文件位置：<c>{persistentDataPath}\EliteEnemies.Coop.log</c>
    /// （<c>%LOCALAPPDATA%Low\TeamSoda\Duckov\</c>）。
    /// <b>每次启动覆盖</b>——它记的是"本局发生了什么"，历史局没有价值。
    /// 写入失败一律静默吞掉：诊断不该影响游戏。</para>
    /// </summary>
    internal static class CoopLog
    {
        private const string FileName = "EliteEnemies.Coop.log";

        /// <summary>写入上限。超了就停止写并记一条——防止异常循环把磁盘写满。</summary>
        private const int MaxBytes = 4 * 1024 * 1024;

        private static readonly object s_lock = new object();
        private static StringBuilder s_buffer = new StringBuilder(1024);
        private static string s_path;
        private static int s_written;
        private static bool s_broken;
        private static bool s_announced;

        /// <summary>日志文件的完整路径；尚未初始化时为 null。</summary>
        public static string Path => s_path;

        /// <summary>
        /// 往 <c>Player.log</c> 打**一行**指针。只在第一次调用时打。
        ///
        /// <para>这一行走 <c>Debug.LogError</c> 是有意的：只有它能穿透联机模组的过滤。
        /// 但因为它只有一行，代价可忽略——**不要**再加第二行。</para>
        /// </summary>
        public static void Announce()
        {
            if (s_announced) return;
            s_announced = true;

            EnsurePath();
            Debug.LogError($"[EliteEnemies.Coop] 详细诊断日志：{s_path ?? "(不可用)"}" +
                           "（本模块的其余输出都在那个文件里，Player.log 里不会有）");
        }

        public static void Info(string message) => Write("INFO ", message);

        public static void Warn(string message) => Write("WARN ", message);

        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            if (s_broken) return;

            try
            {
                EnsurePath();
                if (s_path == null) return;

                lock (s_lock)
                {
                    s_buffer.Clear();
                    s_buffer.Append(DateTime.Now.ToString("MM-dd HH:mm:ss.fff"))
                            .Append(" [").Append(level).Append("] ")
                            .Append(message)
                            .Append('\n');

                    string line = s_buffer.ToString();
                    s_written += line.Length;

                    if (s_written > MaxBytes)
                    {
                        s_broken = true;
                        File.AppendAllText(s_path,
                            $"{DateTime.Now:MM-dd HH:mm:ss.fff} [WARN ] 日志超过 {MaxBytes} 字节，已停止记录。\n");
                        return;
                    }

                    File.AppendAllText(s_path, line);
                }
            }
            catch
            {
                // 诊断失败绝不能影响游戏。
                s_broken = true;
            }
        }

        private static void EnsurePath()
        {
            if (s_path != null || s_broken) return;

            try
            {
                string dir = Application.persistentDataPath;
                if (string.IsNullOrEmpty(dir))
                {
                    s_broken = true;
                    return;
                }

                s_path = System.IO.Path.Combine(dir, FileName);

                // 每次启动覆盖：文件记的是本局，历史没有价值。
                File.WriteAllText(s_path,
                    $"{DateTime.Now:MM-dd HH:mm:ss.fff} [INFO ] === EliteEnemies 联机兼容模块 启动 ===\n" +
                    $"{DateTime.Now:MM-dd HH:mm:ss.fff} [INFO ] 游戏版本 {Application.version}；" +
                    $"Unity {Application.unityVersion}\n");
            }
            catch
            {
                s_broken = true;
                s_path = null;
            }
        }
    }
}
