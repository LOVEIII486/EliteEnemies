using System;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 把游戏里真正落地的 Error / Exception / Assert **抄一份**进本模块的日志文件，带堆栈与时间戳。
    ///
    /// <para><b>为什么需要它</b>：<c>Player.log</c> <b>没有时间戳</b>，而且联机模组会把自己的
    /// 普通日志全吞掉（见 <see cref="CoopLog"/> 的类注释）。排查"客机卡死在哪一刻、当时在刷什么"
    /// 这类问题时，光有 <c>Player.log</c> 是不够的——既对不上时间，也看不到上下文。
    /// 抄一份到我们自己的文件，就同时有了**时间戳**与**和本模块事件的相对顺序**。</para>
    ///
    /// <para><b>开销很小，有依据</b>：挂的是 <see cref="Application.logMessageReceived"/>，
    /// 它由 Unity 的**原生**日志处理器触发。联机模组的 <c>ReleaseLogHandler</c> 在更上游就把多数
    /// 日志丢掉了，那些消息<b>根本不会到达原生处理器</b>，本回调也就不会被调用。
    /// 所以实际回调频率 ≈ <c>Player.log</c> 里 Error 出现的频率，不是每帧。</para>
    ///
    /// <para>⚠️ <b>这是排查期的诊断设施</b>。按 <c>AGENT.md §3.8</c>，
    /// 等联机兼容稳定后应当重新评估它是否还需要常开——它记的是"别人抛的错"，
    /// 与本模块的长期职责并不完全重合。</para>
    /// </summary>
    internal static class CoopDiag
    {
        /// <summary>同一条消息最多抄几次（带堆栈），之后的只计数。</summary>
        private const int MaxCopiesPerMessage = 3;

        /// <summary>去重表的容量上限——超了就整体清空重来，避免它自己变成内存泄漏。</summary>
        private const int MaxTrackedMessages = 256;

        private static readonly Dictionary<string, int> s_seen = new Dictionary<string, int>(StringComparer.Ordinal);
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                Application.logMessageReceived += OnMessage;
                CoopLog.Info("[诊断] 已开始抄录游戏 Error / Exception / Assert");
            }
            catch (Exception ex)
            {
                _installed = false;
                CoopLog.Warn($"[诊断] 挂接失败，将不抄录游戏错误：{ex.Message}");
            }
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;

            try
            {
                Application.logMessageReceived -= OnMessage;
            }
            catch
            {
                // 卸载路径不抛
            }

            s_seen.Clear();
        }

        private static void OnMessage(string condition, string stackTrace, LogType type)
        {
            // 只关心真正会被看到的那些；Log/Warning 在联机局里根本到不了这里。
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            try
            {
                // 本模块自己打的那一行指针不要抄回来（会自我复制）。
                if (condition != null && condition.StartsWith("[EliteEnemies.Coop]", StringComparison.Ordinal))
                    return;

                string key = Shorten(condition);

                s_seen.TryGetValue(key, out int count);
                count++;
                s_seen[key] = count;

                if (count <= MaxCopiesPerMessage)
                {
                    CoopLog.Error($"[游戏{type}] {condition}" +
                                  (string.IsNullOrEmpty(stackTrace) ? string.Empty : $"\n{stackTrace}"));
                }
                else if (count == MaxCopiesPerMessage + 1)
                {
                    CoopLog.Error($"[游戏{type}] 「{key}」重复出现，后续同条只计数不再抄录堆栈");
                }

                if (s_seen.Count > MaxTrackedMessages)
                {
                    // 简单粗暴地重置：宁可漏计，也不要让去重表无限长大。
                    s_seen.Clear();
                    CoopLog.Info("[诊断] 去重表已重置（累计消息种类过多）");
                }
            }
            catch
            {
                // 抄录失败绝不能影响游戏。
            }
        }

        private static string Shorten(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return "(空)";
            return condition.Length <= 160 ? condition : condition.Substring(0, 160) + "…";
        }
    }
}
