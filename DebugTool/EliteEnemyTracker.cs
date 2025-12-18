using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EliteEnemies.DebugTool
{
    /// <summary>
    /// 精英敌人调试与统计工具
    /// 用于汇总当前地图中被处理或略过的敌人资源名、显示名及数量。
    /// </summary>
    internal static class EliteEnemyTracker
    {
        private const string LogTag = "[EliteEnemies.EliteEnemyTracker]";

        // 修改 Value 结构，同时存储 计数 和 最新的显示名称
        private class StatEntry
        {
            public int Count;
            public string DisplayLabel;
        }

        private static readonly Dictionary<string, StatEntry> Processed =
            new Dictionary<string, StatEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, StatEntry> Skipped =
            new Dictionary<string, StatEntry>(StringComparer.OrdinalIgnoreCase);
        
        public static int TotalProcessedCount => Processed.Values.Sum(v => v.Count);
        
        internal static void Reset()
        {
            Processed.Clear();
            Skipped.Clear();
        }

        /// <summary>
        /// 记录一次敌人处理结果
        /// </summary>
        /// <param name="technicalName">资源名 (rName)</param>
        /// <param name="displayLabel">可读的名称 (ResolveBaseName 后的结果)</param>
        /// <param name="processedFlag">是否成功处理为精英</param>
        internal static void RecordDecision(string technicalName, string displayLabel, bool processedFlag)
        {
            string key = string.IsNullOrEmpty(technicalName) ? "(empty)" : technicalName;
            var dict = processedFlag ? Processed : Skipped;

            if (dict.TryGetValue(key, out var entry))
            {
                entry.Count++;
                // 如果之前没拿到显示名，现在补上
                if (string.IsNullOrEmpty(entry.DisplayLabel)) entry.DisplayLabel = displayLabel;
            }
            else
            {
                dict[key] = new StatEntry { Count = 1, DisplayLabel = displayLabel };
            }
        }

        /// <summary>
        /// 打印统计摘要
        /// </summary>
        internal static void DumpSummary(string sceneName = "")
        {
            int totalProcessed = Processed.Values.Sum(v => v.Count);
            int totalSkipped = Skipped.Values.Sum(v => v.Count);
            if (totalProcessed + totalSkipped == 0) return;

            // 格式化输出：资源名 [显示名]: 数量
            IEnumerable<string> FormatBlock(Dictionary<string, StatEntry> d)
                => d.OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => 
                    {
                        string label = string.IsNullOrEmpty(kv.Value.DisplayLabel) ? "" : $" [{kv.Value.DisplayLabel}]";
                        return $"  - {kv.Key}{label}: {kv.Value.Count}";
                    });

            Debug.Log("========================================");
            Debug.Log($"{LogTag} 统计报告 - 场景: {sceneName}");

            Debug.Log($"{LogTag} 已变为精英 ({Processed.Count} 种预设 / 共 {totalProcessed} 个单位):");
            foreach (var line in FormatBlock(Processed)) Debug.Log(line);

            Debug.Log($"{LogTag} 已跳过精英化 ({Skipped.Count} 种预设 / 共 {totalSkipped} 个单位):");
            foreach (var line in FormatBlock(Skipped)) Debug.Log(line);
            Debug.Log("========================================");
        }
    }
}