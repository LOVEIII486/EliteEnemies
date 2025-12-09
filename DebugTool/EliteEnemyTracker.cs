using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EliteEnemies.DebugTool
{
    /// <summary>
    /// 精英敌人调试与统计工具
    /// 用于汇总当前地图中被处理或略过的敌人 preset 名称及数量。
    /// </summary>
    internal static class EliteEnemyTracker
    {
        private const string LogTag = "[EliteEnemies.EliteEnemyTracker]";
        private static readonly Dictionary<string, int> Processed =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> Skipped =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        public static int TotalProcessedCount => Processed.Values.Sum();
        
        /// <summary> 清空统计数据 </summary>
        internal static void Reset()
        {
            Processed.Clear();
            Skipped.Clear();
        }

        /// <summary>
        /// 记录一次敌人处理结果
        /// </summary>
        internal static void RecordDecision(string presetName, bool processedFlag)
        {
            string key = string.IsNullOrEmpty(presetName) ? "(empty)" : presetName;
            var dict = processedFlag ? Processed : Skipped;

            if (dict.TryGetValue(key, out int n))
                dict[key] = n + 1;
            else
                dict[key] = 1;
        }

        /// <summary>
        /// 打印当前统计结果
        /// </summary>
        internal static void DumpSummary(string sceneName = "")
        {
            int totalProcessed = Processed.Values.Sum();
            int totalSkipped = Skipped.Values.Sum();
            if (totalProcessed + totalSkipped == 0)
                return;

            IEnumerable<string> FormatBlock(Dictionary<string, int> d)
                => d.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"  - {kv.Key}: {kv.Value}");

            Debug.Log("========================================");
            Debug.Log($"{LogTag} {sceneName}");

            Debug.Log($"{LogTag} 处理了 ({Processed.Count} 种类型 / {totalProcessed} 个单位):");
            foreach (var line in FormatBlock(Processed)) Debug.Log(line);

            Debug.Log($"{LogTag} 跳过了 ({Skipped.Count} 种类型 / {totalSkipped} 个单位):");
            foreach (var line in FormatBlock(Skipped)) Debug.Log(line);
            Debug.Log("========================================");
        }
    }
}