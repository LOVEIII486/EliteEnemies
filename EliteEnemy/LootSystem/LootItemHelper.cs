using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Duckov.Utilities;
using EliteEnemies.Settings;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.LootSystem
{
    /// <summary>
    /// 掉落物品辅助工具
    /// </summary>
    public class LootItemHelper : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.LootHelper]";
        
        private static bool Verbose = false;
        
        // 品质偏好：>0 偏向高品质, <0 偏向低品质
        // 由EliteLootSystem主导更新，不需要手动更新
        public float qualityBiasPower = 0f;

        // 存储每个品质对应的可用物品ID列表
        private Dictionary<int, List<int>> _qualityItemCache = new Dictionary<int, List<int>>();
        // 存储物品ID对应的标签集合，避免运行时实例化查询
        private Dictionary<int, HashSet<string>> _itemTagCache = new Dictionary<int, HashSet<string>>();
        
        // 标记是否初始化完成
        private bool _isInitialized = false;

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

        // 确保切换场景不销毁，防止协程中断和数据丢失
        private void Awake()
        {
            var others = FindObjectsOfType<LootItemHelper>();
            if (others.Length > 1)
            {
                Destroy(this.gameObject);
                return;
            }
            DontDestroyOnLoad(this.gameObject);
            qualityBiasPower = GameConfig.ItemQualityBias; 
        }

        private void Start()
        {
            StartCoroutine(InitializeItemCacheAsync());
        }

        // 监控销毁情况
        private void OnDestroy()
        {
            if (Verbose) Debug.Log($"{LogTag} LootItemHelper 被销毁");
        }

        // ========== 初始化核心逻辑 ==========
        
        private IEnumerator InitializeItemCacheAsync()
        {
            if (Verbose) Debug.Log($"{LogTag} 开始初始化物品缓存（异步模式）");
            
            _qualityItemCache.Clear();
            _itemTagCache.Clear();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            
            float maxMillisecondsPerFrame = 5f; 

            for (int quality = 1; quality <= 7; quality++)
            {
                int[] itemIds = null;

                try
                {
                    ItemFilter filter = new ItemFilter
                    {
                        requireTags = new Tag[0],
                        excludeTags = new Tag[0], // 不在此处过滤，在处理函数中统一处理
                        minQuality = quality,
                        maxQuality = quality
                    };
                    itemIds = ItemAssetsCollection.Search(filter);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogTag} 搜索品质 {quality} 物品时出错: {ex.Message}");
                    continue;
                }

                List<int> validItems = new List<int>();

                if (itemIds != null)
                {
                    int processedCount = 0;
                    foreach (int itemId in itemIds)
                    {
                        // 处理单个物品：检查有效性并缓存标签
                        if (ProcessItemAndCacheTags(itemId))
                        {
                            validItems.Add(itemId);
                        }
                        
                        processedCount++;
                        
                        if (processedCount % 10 == 0 && stopwatch.Elapsed.TotalMilliseconds > maxMillisecondsPerFrame)
                        {
                            stopwatch.Reset();
                            stopwatch.Start();
                            yield return null; 
                        }
                    }
                }

                _qualityItemCache[quality] = validItems;
                if (Verbose) Debug.Log($"{LogTag} 品阶 {quality}: {validItems.Count} 个可用物品");
                yield return null;
            }

            _isInitialized = true;
            if (Verbose) Debug.Log($"{LogTag} 物品缓存初始化完成");
        }

        /// <summary>
        /// 处理单个物品：验证黑名单并提取标签缓存
        /// </summary>
        private bool ProcessItemAndCacheTags(int itemId)
        {
            Item item = null;
            try
            {
                item = ItemAssetsCollection.InstantiateSync(itemId);
                if (item == null) return false;
                
                item.Initialize();

                // 1. 检查名称和描述黑名单
                string name = item.DisplayName ?? "";
                string desc = item.Description ?? "";
                
                foreach (string prefix in _nameDescriptionBlacklist)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        desc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEngine.Object.Destroy(item.gameObject);
                        return false;
                    }
                }

                // 2. 提取标签 & 检查标签黑名单
                HashSet<string> currentTags = new HashSet<string>();
                bool isTagBlacklisted = false;

                if (item.Tags?.list != null)
                {
                    foreach (var tag in item.Tags.list)
                    {
                        if (tag == null || string.IsNullOrEmpty(tag.name)) continue;

                        foreach (string blacklistedTag in _tagBlacklist)
                        {
                            if (tag.name.Equals(blacklistedTag, StringComparison.OrdinalIgnoreCase))
                            {
                                isTagBlacklisted = true;
                                break;
                            }
                        }
                        
                        if (isTagBlacklisted) break;

                        // 记录标签到临时集合
                        currentTags.Add(tag.name);
                    }
                }

                // 如果命中黑名单，销毁并返回无效
                if (isTagBlacklisted)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                    return false;
                }

                // 3. 验证通过，存入缓存
                _itemTagCache[itemId] = currentTags;
                
                UnityEngine.Object.Destroy(item.gameObject);
                return true;
            }
            catch (Exception)
            {
                if (item != null) UnityEngine.Object.Destroy(item.gameObject);
                return false;
            }
        }

        // ========== 查询接口  ==========

        /// <summary>
        /// 检查物品是否包含所有指定标签 
        /// </summary>
        private bool ItemHasAllTags(int itemId, Tag[] requiredTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
                return true;

            // 直接查缓存
            if (!_itemTagCache.TryGetValue(itemId, out var itemTags))
            {
                return false; // 缓存中没有说明初始化时被过滤或无效
            }

            foreach (Tag requiredTag in requiredTags)
            {
                // 只要有一个标签不在缓存中，就返回 false
                if (!itemTags.Contains(requiredTag.name))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 按品质权重创建物品（支持品质偏好和标签过滤）
        /// </summary>
        public Item CreateItemWithTagsWeighted(int minQuality = 1, int maxQuality = 7, Tag[] requiredTags = null)
        {
            if (!_isInitialized)
            {
                // Debug.LogWarning($"{LogTag} 正在初始化缓存，暂时无法生成掉落...");
                return null;
            }

            // 处理 -1 特殊情况
            if (minQuality == -1 || maxQuality == -1)
            {
                if (requiredTags == null || requiredTags.Length == 0) return null;
                return CreateItemWithTagsFromAllQualities(requiredTags);
            }

            minQuality = Mathf.Clamp(minQuality, 1, 7);
            maxQuality = Mathf.Clamp(maxQuality, 1, 7);
            
            // 如果指定了标签，先统计有效品质
            if (requiredTags != null && requiredTags.Length > 0)
            {
                List<int> validQualities = new List<int>();

                for (int q = minQuality; q <= maxQuality; q++)
                {
                    if (!_qualityItemCache.TryGetValue(q, out var pool) || pool.Count == 0)
                        continue;

                    // 使用优化后的方法检查是否有符合条件的物品
                    foreach (int itemId in pool)
                    {
                        if (ItemHasAllTags(itemId, requiredTags))
                        {
                            validQualities.Add(q);
                            break; // 该品质下只要有一个符合条件的即可
                        }
                    }
                }

                if (validQualities.Count == 0) return null;
                
                // 按权重选择一个品质
                int pickedQuality = PickQualityByWeightFromValidQualities(validQualities, minQuality, maxQuality);
                //Debug.Log($"elite quality {qualityBiasPower} -》 {pickedQuality}");
                return CreateItemWithTagsFromQuality(pickedQuality, requiredTags);
            }
            else
            {
                // 无标签限制：直接按权重选择品质
                int pickedQuality = PickQualityByWeight(minQuality, maxQuality);
                //Debug.Log($"elite quality non limit {qualityBiasPower} -》 {pickedQuality}");
                return CreateItemFromQuality(pickedQuality);
            }
        }

        // ========== 创建物品辅助方法 ==========

        private Item CreateItemFromQuality(int quality)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0) return null;
            int itemId = pool[UnityEngine.Random.Range(0, pool.Count)];
            return InstantiateItem(itemId);
        }

        private Item CreateItemWithTagsFromQuality(int quality, Tag[] requiredTags)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0) return null;

            // 筛选符合标签的ID (内存查询，速度极快)
            List<int> matchingItems = new List<int>();
            foreach (int itemId in pool)
            {
                if (ItemHasAllTags(itemId, requiredTags))
                {
                    matchingItems.Add(itemId);
                }
            }

            if (matchingItems.Count == 0) return null;

            int selectedId = matchingItems[UnityEngine.Random.Range(0, matchingItems.Count)];
            return InstantiateItem(selectedId);
        }

        private Item CreateItemWithTagsFromAllQualities(Tag[] requiredTags)
        {
            List<int> matchingItems = new List<int>();

            for (int q = 1; q <= 7; q++)
            {
                if (!_qualityItemCache.TryGetValue(q, out var pool) || pool.Count == 0) continue;

                foreach (int itemId in pool)
                {
                    if (ItemHasAllTags(itemId, requiredTags))
                    {
                        matchingItems.Add(itemId);
                    }
                }
            }

            if (matchingItems.Count == 0) return null;
            int selectedId = matchingItems[UnityEngine.Random.Range(0, matchingItems.Count)];
            return InstantiateItem(selectedId);
        }

        private Item InstantiateItem(int id)
        {
            try
            {
                var item = ItemAssetsCollection.InstantiateSync(id);
                if (item != null) item.Initialize();
                return item;
            }
            catch { return null; }
        }

        // ========== 权重算法 ==========

        private int PickQualityByWeight(int minQuality, int maxQuality)
        {
            //qualityBiasPower = GameConfig.ItemQualityBias;
            if (Mathf.Approximately(qualityBiasPower, 0f))
                return UnityEngine.Random.Range(minQuality, maxQuality + 1);

            float totalWeight = 0f;
            float[] weights = new float[maxQuality - minQuality + 1];

            for (int i = 0; i < weights.Length; i++)
            {
                int q = minQuality + i;
                int baseValue = (qualityBiasPower < 0) ? (8 - q) : q;
                if (baseValue <= 0) baseValue = 1;
                weights[i] = Mathf.Pow(baseValue, Mathf.Abs(qualityBiasPower));
                totalWeight += weights[i];
            }

            float random = UnityEngine.Random.value * totalWeight;
            float accumulated = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                accumulated += weights[i];
                if (random <= accumulated) return minQuality + i;
            }
            return maxQuality;
        }

        private int PickQualityByWeightFromValidQualities(List<int> validQualities, int minQuality, int maxQuality)
        {
            //qualityBiasPower = GameConfig.ItemQualityBias;
            if (validQualities == null || validQualities.Count == 0) return minQuality;
            if (Mathf.Approximately(qualityBiasPower, 0f))
                return validQualities[UnityEngine.Random.Range(0, validQualities.Count)];

            float totalWeight = 0f;
            float[] weights = new float[validQualities.Count];

            for (int i = 0; i < weights.Length; i++)
            {
                int q = validQualities[i];
                int baseValue = (qualityBiasPower < 0) ? (8 - q) : q;
                if (baseValue <= 0) baseValue = 1;
                weights[i] = Mathf.Pow(baseValue, Mathf.Abs(qualityBiasPower));
                totalWeight += weights[i];
            }

            float random = UnityEngine.Random.value * totalWeight;
            float accumulated = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                accumulated += weights[i];
                if (random <= accumulated) return validQualities[i];
            }
            return validQualities.Last();
        }
    }
}