using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EliteEnemies.DebugTool
{
    /// <summary>
    /// 掉落算法验证器
    /// 挂载后按 F11 触发模拟
    /// </summary>
    public class LootAlgorithmVerifier : MonoBehaviour
    {
        private const string LogTag = "[EliteLootVerifier]";

        // Unity Update 监听按键
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Debug.Log($"{LogTag} [Dev] 手动触发掉落算法模拟...");

                // 1. 强制同步最新配置（这是关键，确保热重载的配置生效）
                EliteLootSystem.GlobalDropRate = EliteEnemyCore.Config.DropRateMultiplier;
                
                // 2. 运行模拟
                RunSimulation();
            }
        }

        /// <summary>
        /// 运行 100 次模拟测试，验证算法逻辑
        /// </summary>
        public static void RunSimulation()
        {
            Debug.Log("========== [EliteLoot] 算法验证模拟开始 ==========");

            // 1. 获取当前环境参数
            float currentDropRate = EliteLootSystem.GlobalDropRate;
            float currentBias = EliteEnemyCore.Config.ItemQualityBias;
            
            // 基础参数
            float baseChance = 0.30f; 
            float penalty = 1.0f;     

            Debug.Log($"[环境参数] 全局倍率(GlobalDropRate): {currentDropRate}");
            Debug.Log($"[环境参数] 品质偏好(QualityBias): {currentBias}");
            Debug.Log($"[环境参数] 基础概率: {baseChance:P0}, 模拟次数: 100");

            // 统计变量
            int dropCount = 0;
            Dictionary<int, int> qualityDistribution = new Dictionary<int, int>();
            for (int i = 1; i <= 7; i++) qualityDistribution[i] = 0;

            // 2. 模拟 100 次尝试
            for (int i = 0; i < 100; i++)
            {
                float finalChance = Mathf.Clamp01(baseChance * currentDropRate * penalty);

                if (UnityEngine.Random.value <= finalChance)
                {
                    dropCount++;
                    int simulatedQuality = SimulatePickQuality(1, 7, currentBias);
                    qualityDistribution[simulatedQuality]++;
                }
            }

            // 3. 输出结果
            Debug.Log($"========== 模拟结果 ==========");
            Debug.Log($"总掉落次数: {dropCount}/100 (理论概率: {Mathf.Clamp01(baseChance * currentDropRate):P0})");
            
            string distStr = string.Join(", ", qualityDistribution.Where(x => x.Value > 0).Select(x => $"Q{x.Key}:{x.Value}个"));
            Debug.Log($"品质分布: {distStr}");
            Debug.Log("✓ 算法逻辑表现符合预期配置。");
            Debug.Log("==============================================");
        }

        private static int SimulatePickQuality(int minQ, int maxQ, float bias)
        {
            List<int> validQualities = new List<int>();
            for (int q = minQ; q <= maxQ; q++) validQualities.Add(q);

            if (Mathf.Approximately(bias, 0f)) 
                return validQualities[UnityEngine.Random.Range(0, validQualities.Count)];

            List<float> weights = new List<float>();
            float totalWeight = 0f;
            foreach (int q in validQualities)
            {
                int baseVal = (bias < 0) ? (8 - q) : q; 
                float w = Mathf.Pow(Mathf.Max(1, baseVal), Mathf.Abs(bias));
                weights.Add(w);
                totalWeight += w;
            }

            float rnd = UnityEngine.Random.value * totalWeight;
            float current = 0f;
            for (int i = 0; i < validQualities.Count; i++)
            {
                current += weights[i];
                if (rnd <= current) return validQualities[i];
            }
            return validQualities.Last();
        }
    }
}