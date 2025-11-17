using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Duckov.Utilities;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace EliteEnemies
{
    /// <summary>
    /// 掉落物品辅助工具
    /// 功能：缓存游戏物品池、按品质和标签随机生成物品、调试测试
    /// </summary>
    public class LootItemHelper : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.LootItemHelper]";

        public bool debugMode = false;
        public float qualityBiasPower = 0f;

        // ========== 黑名单配置 ==========
        private readonly string[] _nameDescriptionBlacklist =
        {
            "Item_", "Quest_", "BP_", "水族箱", "比特币矿机", "蛋清能源碎片",
            "口口头盔", "口口防弹衣", "防空系统密钥"
        };

        private readonly string[] _tagBlacklist =
        {
            "DestroyOnLootBox", "DestroyInBase", "Formula", "Formula_Blueprint", "Quest"
        };

        private Dictionary<int, List<int>> _qualityItemCache = new Dictionary<int, List<int>>();
        private bool _isInitialized = false;
        private CharacterMainControl _player;


        private void Start()
        {
            DebugLog("开始初始化");
            InitializeItemCache();
        }

        private void Update()
        {
            if (!debugMode) return;

            if (Input.GetKeyDown(KeyCode.F5))
            {
                TestSpawnRandomItem();
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                TestOutputStatistics();
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                ExportAllItemsToFile();
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                TestLootDistribution();
            }
        }

        // ========== 初始化 ==========

        private void InitializeItemCache()
        {
            DebugLog("开始初始化物品缓存");
            _qualityItemCache.Clear();

            try
            {
                for (int quality = 1; quality <= 7; quality++)
                {
                    ItemFilter filter = new ItemFilter
                    {
                        requireTags = new Tag[0],
                        excludeTags = GetExcludedTags(),
                        minQuality = quality,
                        maxQuality = quality
                    };

                    int[] itemIds = ItemAssetsCollection.Search(filter);
                    List<int> validItems = new List<int>();

                    if (itemIds != null)
                    {
                        foreach (int itemId in itemIds)
                        {
                            if (IsItemValid(itemId))
                            {
                                validItems.Add(itemId);
                            }
                        }
                    }

                    _qualityItemCache[quality] = validItems;
                    DebugLog($"品阶 {quality} ({GetQualityName(quality)}): {validItems.Count} 个可用物品");
                }

                _isInitialized = true;
                DebugLog("物品缓存初始化完成");
            }
            catch (Exception ex)
            {
                LogError($"初始化物品缓存失败: {ex.Message}");
                _isInitialized = false;
            }
        }

        private Tag[] GetExcludedTags()
        {
            List<Tag> excludedTags = new List<Tag>();

            foreach (string tagName in _tagBlacklist)
            {
                if (!string.IsNullOrEmpty(tagName))
                {
                    Tag tag = TagUtilities.TagFromString(tagName);
                    if (tag != null)
                    {
                        excludedTags.Add(tag);
                    }
                }
            }

            return excludedTags.ToArray();
        }

        private bool IsItemValid(int itemId)
        {
            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(itemId);
                if (item == null) return false;

                item.Initialize();

                string name = item.DisplayName ?? "";
                string desc = item.Description ?? "";

                // 检查名称和描述黑名单
                foreach (string prefix in _nameDescriptionBlacklist)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        desc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        DebugLog($"过滤物品（名称/描述）: {name} (ID: {itemId})");
                        UnityEngine.Object.Destroy(item.gameObject);
                        return false;
                    }
                }

                // 检查标签黑名单
                if (item.Tags?.list != null)
                {
                    foreach (var tag in item.Tags.list)
                    {
                        if (tag == null) continue;

                        foreach (string blacklistedTag in _tagBlacklist)
                        {
                            if (!string.IsNullOrEmpty(blacklistedTag) &&
                                tag.name.Equals(blacklistedTag, StringComparison.OrdinalIgnoreCase))
                            {
                                DebugLog($"过滤物品（标签 {tag.name}）: {name} (ID: {itemId})");
                                UnityEngine.Object.Destroy(item.gameObject);
                                return false;
                            }
                        }
                    }
                }

                UnityEngine.Object.Destroy(item.gameObject);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"检查物品有效性异常 (ID: {itemId}): {ex.Message}");
                return false;
            }
        }

        // ========== 公共接口 ==========

        /// <summary>
        /// 创建随机物品（按品质和标签）
        /// </summary>
        public Item CreateRandomItem(int targetQuality, Tag[] requiredTags = null)
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return null;
            }

            try
            {
                // 应用品质偏好
                int adjustedQuality = ApplyQualityBias(targetQuality);
                adjustedQuality = Mathf.Clamp(adjustedQuality, 1, 7);

                // 获取品质对应的物品池
                if (!_qualityItemCache.TryGetValue(adjustedQuality, out var itemPool) || itemPool.Count == 0)
                {
                    DebugLog($"品阶 {adjustedQuality} 无可用物品");
                    return null;
                }

                // 按标签过滤
                List<int> filteredItems = FilterItemsByTags(itemPool, requiredTags);

                if (filteredItems.Count == 0)
                {
                    DebugLog($"品阶 {adjustedQuality} 无符合标签的物品");
                    return null;
                }

                // 随机选择
                int randomItemId = filteredItems[UnityEngine.Random.Range(0, filteredItems.Count)];
                Item item = ItemAssetsCollection.InstantiateSync(randomItemId);

                if (item != null)
                {
                    item.Initialize();
                    DebugLog($"创建物品: {item.DisplayName} (品阶: {adjustedQuality})");
                }

                return item;
            }
            catch (Exception ex)
            {
                LogError($"创建随机物品失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 批量创建随机物品
        /// </summary>
        public List<Item> CreateRandomItems(int count, int targetQuality, Tag[] requiredTags = null)
        {
            List<Item> items = new List<Item>();

            for (int i = 0; i < count; i++)
            {
                Item item = CreateRandomItem(targetQuality, requiredTags);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        /// 按品质权重创建物品（支持品质偏好和标签过滤）
        /// 低品质物品权重更高（除非使用正向品质偏好）
        /// </summary>
        /// <param name="minQuality">最低品质（1-7，-1表示不限）</param>
        /// <param name="maxQuality">最高品质（1-7，-1表示不限）</param>
        /// <param name="requiredTags">必需的标签（可选）</param>
        public Item CreateItemWithTagsWeighted(int minQuality = 1, int maxQuality = 7, Tag[] requiredTags = null)
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return null;
            }

            // 处理 -1 特殊情况（不限品质，从所有品质中查找）
            if (minQuality == -1 || maxQuality == -1)
            {
                if (requiredTags == null || requiredTags.Length == 0)
                {
                    LogError("品质为 -1 时必须指定标签");
                    return null;
                }

                return CreateItemWithTagsFromAllQualities(requiredTags);
            }

            // 验证品质范围
            minQuality = Mathf.Clamp(minQuality, 1, 7);
            maxQuality = Mathf.Clamp(maxQuality, 1, 7);
            if (minQuality > maxQuality)
            {
                LogError($"无效的品质区间: {minQuality} - {maxQuality}");
                return null;
            }

            // 如果指定了标签，先统计每个品质中符合标签的物品数量
            if (requiredTags != null && requiredTags.Length > 0)
            {
                Dictionary<int, int> validItemCountsByQuality = new Dictionary<int, int>();

                for (int q = minQuality; q <= maxQuality; q++)
                {
                    if (!_qualityItemCache.TryGetValue(q, out var pool) || pool.Count == 0)
                        continue;

                    int count = 0;
                    foreach (int itemId in pool)
                    {
                        if (ItemHasAllTags(itemId, requiredTags))
                        {
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        validItemCountsByQuality[q] = count;
                    }
                }

                // 没有任何品质有符合的物品
                if (validItemCountsByQuality.Count == 0)
                {
                    string tagNames = string.Join(", ", Array.ConvertAll(requiredTags, t => t.name));
                    LogError($"品质区间 {minQuality}-{maxQuality} 中找不到包含标签 [{tagNames}] 的物品");
                    return null;
                }

                // 按权重从有效品质中选择
                int pickedQuality = PickQualityByWeightFromValidQualities(
                    validItemCountsByQuality.Keys.ToList(), minQuality, maxQuality);

                if (pickedQuality <= 0)
                {
                    LogError("按权重选择品质失败");
                    return null;
                }

                // 从选中的品质创建物品
                return CreateItemWithTagsFromQuality(pickedQuality, requiredTags);
            }
            else
            {
                // 无标签限制：按权重选择品质
                int pickedQuality = PickQualityByWeight(minQuality, maxQuality);
                if (pickedQuality <= 0)
                {
                    LogError($"按权重选择品质失败（区间: {minQuality}-{maxQuality}）");
                    return null;
                }

                return CreateItemFromQuality(pickedQuality);
            }
        }

        // ========== 品质偏好 ==========

        private int ApplyQualityBias(int baseQuality)
        {
            if (Mathf.Approximately(qualityBiasPower, 0f))
            {
                return baseQuality;
            }

            float randomValue = UnityEngine.Random.value;
            float biasedValue;

            if (qualityBiasPower > 0)
            {
                // 正偏好：偏向高品质
                biasedValue = Mathf.Pow(randomValue, 1f / (1f + qualityBiasPower));
            }
            else
            {
                // 负偏好：偏向低品质
                biasedValue = Mathf.Pow(randomValue, 1f + Mathf.Abs(qualityBiasPower));
            }

            int qualityShift = Mathf.RoundToInt((biasedValue - 0.5f) * 4f);
            return baseQuality + qualityShift;
        }

        // ========== 标签过滤 ==========

        private List<int> FilterItemsByTags(List<int> itemPool, Tag[] requiredTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
            {
                return new List<int>(itemPool);
            }

            List<int> filtered = new List<int>();

            foreach (int itemId in itemPool)
            {
                if (ItemHasTags(itemId, requiredTags))
                {
                    filtered.Add(itemId);
                }
            }

            return filtered;
        }

        private bool ItemHasTags(int itemId, Tag[] requiredTags)
        {
            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(itemId);
                if (item == null) return false;

                item.Initialize();

                bool hasAllTags = true;
                if (item.Tags?.list != null)
                {
                    foreach (Tag requiredTag in requiredTags)
                    {
                        if (!item.Tags.list.Contains(requiredTag))
                        {
                            hasAllTags = false;
                            break;
                        }
                    }
                }
                else
                {
                    hasAllTags = false;
                }

                UnityEngine.Object.Destroy(item.gameObject);
                return hasAllTags;
            }
            catch (Exception ex)
            {
                DebugLog($"检查物品标签异常 (ID: {itemId}): {ex.Message}");
                return false;
            }
        }

        // ========== 测试功能 ==========

        private void TestSpawnRandomItem()
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return;
            }

            if (_player == null)
            {
                _player = CharacterMainControl.Main;
            }

            if (_player == null)
            {
                LogError("找不到玩家");
                return;
            }

            int randomQuality = UnityEngine.Random.Range(1, 8);
            Item item = CreateRandomItem(randomQuality);

            if (item != null)
            {
                item.transform.position = _player.transform.position + _player.transform.forward * 2f;
                DebugLog($"生成物品: {item.DisplayName} (品阶: {item.Quality})");
            }
        }

        private void TestOutputStatistics()
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===== 物品缓存统计 =====");

            int totalItems = 0;
            for (int quality = 1; quality <= 7; quality++)
            {
                if (_qualityItemCache.TryGetValue(quality, out var items))
                {
                    sb.AppendLine($"品阶 {quality} ({GetQualityName(quality)}): {items.Count} 个");
                    totalItems += items.Count;
                }
            }

            sb.AppendLine($"总计: {totalItems} 个物品");
            sb.AppendLine("======================");

            DebugLog($"\n{sb}");
            OutputAllTags();
        }

        private void OutputAllTags()
        {
            HashSet<int> visitedItemIds = new HashSet<int>();
            Dictionary<string, string> uniqueTags = new Dictionary<string, string>();

            foreach (var kvp in _qualityItemCache)
            {
                foreach (int itemId in kvp.Value)
                {
                    if (visitedItemIds.Contains(itemId)) continue;
                    visitedItemIds.Add(itemId);

                    try
                    {
                        Item item = ItemAssetsCollection.InstantiateSync(itemId);
                        if (item == null) continue;

                        item.Initialize();

                        if (item.Tags?.list != null)
                        {
                            foreach (var tag in item.Tags.list)
                            {
                                if (tag == null) continue;

                                string tagName = tag.name ?? "";
                                string tagDisplay = tag.DisplayName ?? "";

                                if (!string.IsNullOrEmpty(tagName) && !uniqueTags.ContainsKey(tagName))
                                {
                                    uniqueTags[tagName] = tagDisplay;
                                }
                            }
                        }

                        UnityEngine.Object.Destroy(item.gameObject);
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"读取物品标签异常 (ID: {itemId}): {ex.Message}");
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===== 所有可用标签 =====");
            sb.AppendLine($"共 {uniqueTags.Count} 个不同标签：");

            foreach (var kvp in uniqueTags)
            {
                sb.AppendLine($"  - {kvp.Key} ({kvp.Value})");
            }

            sb.AppendLine("======================");
            DebugLog($"\n{sb}");
        }

        private void ExportAllItemsToFile()
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"ItemDatabase_{timestamp}.txt";
                string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filepath, false, Encoding.UTF8))
                {
                    writer.WriteLine("═══════════════════════════════════════════════════════════");
                    writer.WriteLine($"物品数据库导出 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("═══════════════════════════════════════════════════════════");
                    writer.WriteLine();

                    HashSet<int> exportedItems = new HashSet<int>();
                    int totalCount = 0;

                    for (int quality = 1; quality <= 7; quality++)
                    {
                        if (!_qualityItemCache.TryGetValue(quality, out var itemIds) || itemIds.Count == 0)
                            continue;

                        writer.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        writer.WriteLine($"品阶 {quality} ({GetQualityName(quality)}) - 共 {itemIds.Count} 个物品");
                        writer.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        writer.WriteLine();

                        foreach (int itemId in itemIds)
                        {
                            if (exportedItems.Contains(itemId)) continue;
                            exportedItems.Add(itemId);

                            try
                            {
                                Item item = ItemAssetsCollection.InstantiateSync(itemId);
                                if (item == null) continue;

                                item.Initialize();

                                writer.WriteLine($"┌─ 物品 #{totalCount + 1} ─────────────────────────────────────");
                                writer.WriteLine($"│ ID: {item.TypeID}");
                                writer.WriteLine($"│ 品阶: {item.Quality} ({GetQualityName(item.Quality)})");
                                writer.WriteLine($"│ 显示名称: {item.DisplayName}");
                                writer.WriteLine($"│ 内部名称: {item.DisplayNameRaw}");
                                writer.WriteLine($"│ 描述: {item.Description}");
                                writer.WriteLine($"│ 价值: {item.Value}");
                                writer.WriteLine($"│ 重量: {item.UnitSelfWeight}");
                                writer.WriteLine($"│ 可堆叠: {(item.Stackable ? "是" : "否")}");

                                if (item.Tags != null && item.Tags.list != null && item.Tags.list.Count > 0)
                                {
                                    writer.WriteLine($"│ 标签数量: {item.Tags.list.Count}");
                                    writer.WriteLine($"│ 标签列表:");
                                    foreach (var tag in item.Tags.list)
                                    {
                                        if (tag == null) continue;
                                        writer.WriteLine($"│   - {tag.name} ({tag.DisplayName})");
                                    }
                                }
                                else
                                {
                                    writer.WriteLine($"│ 标签: (无)");
                                }

                                writer.WriteLine($"└───────────────────────────────────────────────────────");
                                writer.WriteLine();

                                UnityEngine.Object.Destroy(item.gameObject);
                                totalCount++;
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"│ [错误] 物品ID {itemId} 导出失败: {ex.Message}");
                                writer.WriteLine($"└───────────────────────────────────────────────────────");
                                writer.WriteLine();
                            }
                        }
                    }

                    writer.WriteLine("═══════════════════════════════════════════════════════════");
                    writer.WriteLine($"导出完成 - 总计 {totalCount} 个物品");
                    writer.WriteLine($"文件位置: {filepath}");
                    writer.WriteLine("═══════════════════════════════════════════════════════════");
                }

                DebugLog($"物品数据库已导出到: {filepath}");
            }
            catch (Exception ex)
            {
                LogError($"导出物品数据库失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试掉落分布 - 模拟100次双词条小怪和100次Boss掉落
        /// </summary>
        private void TestLootDistribution()
        {
            if (!_isInitialized)
            {
                LogError("物品缓存未初始化");
                return;
            }

            Debug.Log("========================================");
            Debug.Log($"{LogTag} 开始测试掉落分布");
            Debug.Log($"{LogTag} 当前品质偏好: {qualityBiasPower}");
            Debug.Log("========================================");

            // 测试配置
            const int normalEnemyTests = 100;
            const int bossTests = 100;

            // 双词条小怪配置 (品质范围 1-4)
            const int normalMinQuality = 2;
            const int normalMaxQuality = 5;
            const int normalAffixCount = 2;

            // Boss配置 (品质范围 3-6)
            const int bossMinQuality = 3;
            const int bossMaxQuality = 7;
            const int bossAffixCount = 2;

            // 测试双词条小怪掉落
            Debug.Log($"\n【双词条小怪测试】({normalEnemyTests}次)");
            Debug.Log($"配置: 词条数={normalAffixCount}, 品质区间={normalMinQuality}-{normalMaxQuality}");

            Dictionary<int, int> normalQualityCount = new Dictionary<int, int>();
            int normalTotalDrops = 0;
            int normalNoDrop = 0;

            for (int i = 0; i < normalEnemyTests; i++)
            {
                // 模拟每个词条 100% 概率掉落1个物品
                int dropsThisEnemy = 0;

                for (int affix = 0; affix < normalAffixCount; affix++)
                {
                    if (UnityEngine.Random.value <= 1f) // 假设理想情况 100% 掉落率
                    {
                        Item item = CreateItemWithTagsWeighted(normalMinQuality, normalMaxQuality, null);
                        if (item != null)
                        {
                            int quality = item.Quality;

                            if (!normalQualityCount.ContainsKey(quality))
                                normalQualityCount[quality] = 0;
                            normalQualityCount[quality]++;

                            normalTotalDrops++;
                            dropsThisEnemy++;

                            UnityEngine.Object.Destroy(item.gameObject);
                        }
                    }
                }

                if (dropsThisEnemy == 0)
                    normalNoDrop++;
            }

            // 输出小怪统计
            Debug.Log($"\n小怪统计:");
            Debug.Log($"  总掉落: {normalTotalDrops} 个物品");
            Debug.Log($"  平均每只: {(float)normalTotalDrops / normalEnemyTests:F2} 个");
            Debug.Log($"  无掉落: {normalNoDrop} 只 ({(float)normalNoDrop / normalEnemyTests * 100:F1}%)");
            Debug.Log($"\n  品质分布:");

            for (int q = normalMinQuality; q <= normalMaxQuality; q++)
            {
                int count = normalQualityCount.ContainsKey(q) ? normalQualityCount[q] : 0;
                float percent = normalTotalDrops > 0 ? (float)count / normalTotalDrops * 100 : 0;
                Debug.Log($"    品质{q} ({GetQualityName(q)}): {count} ({percent:F1}%)");
            }

            // 测试Boss掉落
            Debug.Log($"\n【Boss测试】({bossTests}次)");
            Debug.Log($"配置: 词条数={bossAffixCount}, 品质区间={bossMinQuality}-{bossMaxQuality}");

            Dictionary<int, int> bossQualityCount = new Dictionary<int, int>();
            int bossTotalDrops = 0;
            int bossNoDrop = 0;

            for (int i = 0; i < bossTests; i++)
            {
                // 模拟每个词条 100% 概率掉落1个物品
                int dropsThisBoss = 0;

                for (int affix = 0; affix < bossAffixCount; affix++)
                {
                    if (UnityEngine.Random.value <= 1.0f) // 100% 掉落率
                    {
                        Item item = CreateItemWithTagsWeighted(bossMinQuality, bossMaxQuality, null);
                        if (item != null)
                        {
                            int quality = item.Quality;

                            if (!bossQualityCount.ContainsKey(quality))
                                bossQualityCount[quality] = 0;
                            bossQualityCount[quality]++;

                            bossTotalDrops++;
                            dropsThisBoss++;

                            UnityEngine.Object.Destroy(item.gameObject);
                        }
                    }
                }

                if (dropsThisBoss == 0)
                    bossNoDrop++;
            }

            // Boss统计
            Debug.Log($"\nBoss统计:");
            Debug.Log($"  总掉落: {bossTotalDrops} 个物品");
            Debug.Log($"  平均每只: {(float)bossTotalDrops / bossTests:F2} 个");
            Debug.Log($"  无掉落: {bossNoDrop} 只 ({(float)bossNoDrop / bossTests * 100:F1}%)");
            Debug.Log($"\n  品质分布:");

            for (int q = bossMinQuality; q <= bossMaxQuality; q++)
            {
                int count = bossQualityCount.ContainsKey(q) ? bossQualityCount[q] : 0;
                float percent = bossTotalDrops > 0 ? (float)count / bossTotalDrops * 100 : 0;
                Debug.Log($"    品质{q} ({GetQualityName(q)}): {count} ({percent:F1}%)");
            }
            
            
            Debug.Log($"\n【算法预期】");
            Debug.Log($"  品质偏好={qualityBiasPower}:");

            Debug.Log("========================================");
            Debug.Log($"{LogTag} 掉落分布测试完成");
            Debug.Log("========================================");
        }
        // ========== 辅助方法 ==========

        private Item CreateItemFromQuality(int quality)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0)
            {
                LogError($"品质 {quality} 无可用物品");
                return null;
            }

            int itemId = pool[UnityEngine.Random.Range(0, pool.Count)];

            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(itemId);
                if (item != null)
                {
                    item.Initialize();
                    DebugLog($"创建物品: {item.DisplayName} (品质: {quality})");
                }

                return item;
            }
            catch (Exception ex)
            {
                LogError($"创建物品失败 (ID: {itemId}): {ex.Message}");
                return null;
            }
        }

        private Item CreateItemWithTagsFromQuality(int quality, Tag[] requiredTags)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0)
            {
                LogError($"品质 {quality} 无可用物品");
                return null;
            }

            List<int> matchingItems = new List<int>();

            foreach (int itemId in pool)
            {
                if (ItemHasAllTags(itemId, requiredTags))
                {
                    matchingItems.Add(itemId);
                }
            }

            if (matchingItems.Count == 0)
            {
                string tagNames = string.Join(", ", Array.ConvertAll(requiredTags, t => t.name));
                LogError($"品质 {quality} 中找不到包含标签 [{tagNames}] 的物品");
                return null;
            }

            int selectedId = matchingItems[UnityEngine.Random.Range(0, matchingItems.Count)];

            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(selectedId);
                if (item != null)
                {
                    item.Initialize();
                    string tagNames = string.Join(", ", Array.ConvertAll(requiredTags, t => t.name));
                    DebugLog($"创建物品: {item.DisplayName} [标签: {tagNames}] (品质: {quality})");
                }

                return item;
            }
            catch (Exception ex)
            {
                LogError($"创建物品失败 (ID: {selectedId}): {ex.Message}");
                return null;
            }
        }

        private Item CreateItemWithTagsFromAllQualities(Tag[] requiredTags)
        {
            List<int> matchingItems = new List<int>();

            for (int q = 1; q <= 7; q++)
            {
                if (!_qualityItemCache.TryGetValue(q, out var pool) || pool.Count == 0)
                    continue;

                foreach (int itemId in pool)
                {
                    if (ItemHasAllTags(itemId, requiredTags))
                    {
                        matchingItems.Add(itemId);
                    }
                }
            }

            if (matchingItems.Count == 0)
            {
                string tagNames = string.Join(", ", Array.ConvertAll(requiredTags, t => t.name));
                LogError($"所有品质中都找不到包含标签 [{tagNames}] 的物品");
                return null;
            }

            int selectedId = matchingItems[UnityEngine.Random.Range(0, matchingItems.Count)];

            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(selectedId);
                if (item != null)
                {
                    item.Initialize();
                    string tagNames = string.Join(", ", Array.ConvertAll(requiredTags, t => t.name));
                    DebugLog($"创建物品: {item.DisplayName} [标签: {tagNames}] (品质: {item.Quality})");
                }

                return item;
            }
            catch (Exception ex)
            {
                LogError($"创建物品失败 (ID: {selectedId}): {ex.Message}");
                return null;
            }
        }

        private int PickQualityByWeight(int minQuality, int maxQuality)
        {
            if (Mathf.Approximately(qualityBiasPower, 0f))
            {
                return UnityEngine.Random.Range(minQuality, maxQuality + 1);
            }

            List<float> weights = new List<float>();

            for (int q = minQuality; q <= maxQuality; q++)
            {
                int baseValue = (qualityBiasPower < 0) ? (8 - q) : q;
                if (baseValue <= 0) baseValue = 1;
                float weight = Mathf.Pow(baseValue, Mathf.Abs(qualityBiasPower));
                weights.Add(weight);
            }

            float totalWeight = 0f;
            foreach (float w in weights)
                totalWeight += w;

            float random = UnityEngine.Random.value * totalWeight;
            float accumulated = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                accumulated += weights[i];
                if (random <= accumulated)
                    return minQuality + i;
            }

            return maxQuality;
        }

        private int PickQualityByWeightFromValidQualities(List<int> validQualities, int minQuality, int maxQuality)
        {
            if (validQualities == null || validQualities.Count == 0)
                return -1;

            if (Mathf.Approximately(qualityBiasPower, 0f))
            {
                return validQualities[UnityEngine.Random.Range(0, validQualities.Count)];
            }

            List<float> weights = new List<float>();

            foreach (int q in validQualities)
            {
                int baseValue = (qualityBiasPower < 0) ? (8 - q) : q;
                if (baseValue <= 0) baseValue = 1;
                float weight = Mathf.Pow(baseValue, Mathf.Abs(qualityBiasPower));
                weights.Add(weight);
            }

            float totalWeight = 0f;
            foreach (float w in weights)
                totalWeight += w;

            float random = UnityEngine.Random.value * totalWeight;
            float accumulated = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                accumulated += weights[i];
                if (random <= accumulated)
                    return validQualities[i];
            }

            return validQualities[validQualities.Count - 1];
        }

        private bool ItemHasAllTags(int itemId, Tag[] requiredTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
                return true;

            try
            {
                Item item = ItemAssetsCollection.InstantiateSync(itemId);
                if (item == null) return false;

                item.Initialize();

                bool hasAllTags = true;
                if (item.Tags?.list != null)
                {
                    foreach (Tag requiredTag in requiredTags)
                    {
                        if (!item.Tags.list.Exists(t => t != null && t.name == requiredTag.name))
                        {
                            hasAllTags = false;
                            break;
                        }
                    }
                }
                else
                {
                    hasAllTags = false;
                }

                UnityEngine.Object.Destroy(item.gameObject);
                return hasAllTags;
            }
            catch (Exception ex)
            {
                DebugLog($"检查物品标签异常 (ID: {itemId}): {ex.Message}");
                return false;
            }
        }

        private string GetQualityName(int quality)
        {
            switch (quality)
            {
                case 1: return "普通";
                case 2: return "优秀";
                case 3: return "精良";
                case 4: return "史诗";
                case 5: return "传说";
                case 6: return "神话";
                case 7: return "至高";
                default: return $"品质{quality}";
            }
        }

        private void DebugLog(string message)
        {
            if (debugMode)
            {
                Debug.Log($"{LogTag} {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LogTag} {message}");
        }
    }
}