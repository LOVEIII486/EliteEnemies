using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EliteEnemies.DebugTool
{
    /// <summary>
    /// 掉落算法验证与统计工具
    /// 负责记录游戏过程中的实际掉落数据，用于平衡性分析
    /// </summary>
    public static class LootAlgorithmVerifier
    {
        private const string LogTag = "[EliteEnemies.LootStats]";
        
        // 统计数据结构： [来源池名称] -> [品质(1-7)] -> 数量
        // index 0 未使用，1-7 对应品质
        private static readonly Dictionary<string, int[]> _stats = new Dictionary<string, int[]>();
        
        // 总掉落物品数量
        private static int _totalDrops = 0;
        
        // 总尝试次数（即触发掉落逻辑的次数，通常等于精英怪击杀数）
        private static int _totalAttempts = 0;

        /// <summary>
        /// 记录一次掉落尝试 (在处理精英怪掉落开始时调用)
        /// </summary>
        public static void RecordAttempt()
        {
            _totalAttempts++;
        }

        /// <summary>
        /// 记录一次具体的掉落 (在物品添加到箱子时调用)
        /// </summary>
        /// <param name="sourcePool">掉落来源 (如: 稀有度奖励, 词缀随机)</param>
        /// <param name="quality">物品品质 (1-7)</param>
        /// <param name="count">数量</param>
        public static void RecordDrop(string sourcePool, int quality, int count)
        {
            if (count <= 0) return;
            
            // 确保该来源的统计槽存在
            if (!_stats.ContainsKey(sourcePool))
            {
                _stats[sourcePool] = new int[8]; 
            }

            // 记录数据 (限制品质范围以防数组越界)
            quality = Mathf.Clamp(quality, 1, 7);
            _stats[sourcePool][quality] += count;
            _totalDrops += count;
        }

        /// <summary>
        /// 输出本局统计概览 (场景卸载时调用)
        /// </summary>
        public static void DumpSessionStats()
        {
            // 如果没有数据，且没有尝试过，就不输出日志扰民了
            if (_totalAttempts == 0 && _totalDrops == 0) return;

            // 获取当前配置用于展示（方便截图分析时知道当时的配置）
            float rate = EliteLootSystem.GlobalDropRate;
            float bias = EliteEnemyCore.Config.ItemQualityBias;
            float avgDrops = _totalAttempts > 0 ? (float)_totalDrops / _totalAttempts : 0;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{LogTag} ========== 本局掉落统计 ==========");
            sb.AppendLine($"[环境] 精英击杀数: {_totalAttempts} | 总掉落物: {_totalDrops} (平均 {avgDrops:F1}个/只)");
            sb.AppendLine($"[配置] 全局倍率: {rate:F1} | 品质偏好: {bias:F1}");
            sb.AppendLine("------------------------------------------------------------");

            // 按来源输出详细统计
            foreach (var kvp in _stats)
            {
                string source = kvp.Key;
                int[] counts = kvp.Value;
                int sourceTotal = counts.Sum();
                
                // 格式化品质分布： Q3:2 Q4:5 ...
                string dist = "";
                for (int q = 1; q <= 7; q++)
                {
                    if (counts[q] > 0) dist += $"Q{q}:{counts[q]} ";
                }
                
                // 计算触发率/贡献率
                // 注意：对于固定掉落，这表示平均每个怪掉几个；
                // 对于概率掉落（如稀有度奖励），这近似于触发概率。
                float contributionRate = _totalAttempts > 0 ? (float)sourceTotal / _totalAttempts : 0;

                sb.AppendLine($"   >>> [{source}] 共 {sourceTotal} 个 ({contributionRate:P0}) | 分布: {dist}");
            }
            sb.AppendLine("============================================================");
            
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 清理数据 (新场景加载时调用)
        /// </summary>
        public static void Clear()
        {
            _stats.Clear();
            _totalDrops = 0;
            _totalAttempts = 0;
        }
    }
}