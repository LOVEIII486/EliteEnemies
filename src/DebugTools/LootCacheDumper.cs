using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EliteEnemies.Loot;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.DebugTools
{
    /// <summary>
    /// 掉落系统调试工具
    /// 按 F10 导出当前 LootSystem 缓存的所有物品信息到游戏根目录的 CSV 文件
    /// </summary>
    public class LootCacheDumper : MonoBehaviour
    {
        private bool _isDumping = false;

        private void Update()
        {
            // 防止重复触发
            if (Input.GetKeyDown(KeyCode.F10) && !_isDumping)
            {
                StartCoroutine(DumpLootCacheRoutine());
            }
        }

        private IEnumerator DumpLootCacheRoutine()
        {
            _isDumping = true;
            Debug.Log("[EliteEnemies] 开始导出掉落系统缓存...");

            // 1. 获取 LootItemHelper 实例
            var lootHelper = ModBehaviour.LootHelper;
            if (lootHelper == null)
            {
                Debug.LogError("[EliteEnemies] LootHelper 未初始化，无法导出。请先进入游戏场景。");
                _isDumping = false;
                yield break;
            }

            // 2. 取缓存快照
            //
            // 这里原先用反射读 LootItemHelper 的两个**私有字段**——那是 D5 类违规
            // （对自家程序集内的类型用反射）。它当时的 TODO 写得很准：正确修法是
            // **在 LootItemHelper 上加 internal 只读访问器**。已照此改掉。
            //
            // 收益不只是「合规」：反射那条路**字段改名即静默失效**（GetField 返回 null，
            // 只在运行到导出功能时才报一句），而现在改名会**编译报错**。
            var qualityCache = lootHelper.QualityItemCache;
            var tagCache = lootHelper.ItemTagCache;

            if (qualityCache == null || tagCache == null)
            {
                Debug.LogWarning("[EliteEnemies] 缓存数据尚未构建完成。");
                _isDumping = false;
                yield break;
            }

            // 3. 构建 CSV 内容
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ItemID,Name,Quality,Tags"); // CSV Header

            int processedCount = 0;
            
            // 遍历每个品质等级
            foreach (var kvp in qualityCache)
            {
                int quality = kvp.Key;
                List<int> itemIds = kvp.Value;

                foreach (int itemId in itemIds)
                {
                    string itemName = "Unknown";
                    string tagsStr = "";

                    // 获取 Tags
                    if (tagCache.TryGetValue(itemId, out var tags))
                    {
                        tagsStr = string.Join(";", tags);
                    }

                    // 获取名称 (需要实例化物品，比较耗时，所以分帧处理)
                    // 使用 InstantiateSync 是为了确保能获取到准确的 DisplayName
                    try
                    {
                        var item = ItemAssetsCollection.InstantiateSync(itemId);
                        if (item != null)
                        {
                            itemName = item.DisplayName ?? item.name;
                            // 处理CSV中的英文逗号，防止格式错乱
                            if (itemName.Contains(",")) itemName = $"\"{itemName}\"";
                            
                            Destroy(item.gameObject); // 用完即毁
                        }
                    }
                    catch (Exception)
                    {
                        itemName = "ErrorLoading";
                    }

                    sb.AppendLine($"{itemId},{itemName},{quality},{tagsStr}");

                    processedCount++;
                    // 性能优化：每处理 20 个物品暂停一帧，防止游戏卡死
                    if (processedCount % 20 == 0)
                    {
                        yield return null;
                    }
                }
            }

            // 4. 写入文件到游戏根目录
            try
            {
                string fileName = $"EliteLootDump_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                // 获取游戏根目录路径 (Assets 同级)
                string rootPath = Directory.GetParent(Application.dataPath).ToString();
                string fullPath = Path.Combine(rootPath, fileName);
                
                File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[EliteEnemies] 导出成功！\n文件路径: {fullPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EliteEnemies] 写入 CSV 失败: {ex.Message}");
            }
            finally
            {
                _isDumping = false;
            }
        }
    }
}