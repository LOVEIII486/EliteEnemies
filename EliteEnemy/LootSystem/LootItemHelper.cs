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
    /// </summary>
    public class LootItemHelper : MonoBehaviour
    {
        private const string LogTag = "[LootHelper]";
        public bool debugMode = false;
        public float qualityBiasPower = 0f; // >0 偏向高品质, <0 偏向低品质

        private Dictionary<int, List<int>> _qualityItemCache = new Dictionary<int, List<int>>();
        private bool _isInitialized = false;

        #region Configuration

        private readonly string[] _nameDescriptionBlacklist =
        {
            "Item_", "Quest_", "BP_", "水族箱", "比特币矿机", "蛋清能源碎片",
            "口口头盔", "口口防弹衣", "防空系统密钥"
        };

        private readonly string[] _tagBlacklist =
        {
            "DestroyOnLootBox", "DestroyInBase", "Formula", "Formula_Blueprint", "Quest"
        };

        #endregion

        private void Start()
        {
            InitializeItemCache();
        }

        #region Initialization

        private void InitializeItemCache()
        {
            _qualityItemCache.Clear();
            try
            {
                var excludedTags = GetExcludedTags();
                for (int quality = 1; quality <= 7; quality++)
                {
                    ItemFilter filter = new ItemFilter
                    {
                        requireTags = new Tag[0],
                        excludeTags = excludedTags,
                        minQuality = quality,
                        maxQuality = quality
                    };

                    int[] itemIds = ItemAssetsCollection.Search(filter);
                    List<int> validItems = new List<int>();

                    if (itemIds != null)
                    {
                        foreach (int itemId in itemIds)
                        {
                            if (IsItemValid(itemId)) validItems.Add(itemId);
                        }
                    }

                    _qualityItemCache[quality] = validItems;
                }
                _isInitialized = true;
                if (debugMode) Debug.Log($"{LogTag} 初始化完成，共缓存 {_qualityItemCache.Sum(x=>x.Value.Count)} 个物品");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}");
            }
        }

        private Tag[] GetExcludedTags()
        {
            return _tagBlacklist
                .Select(TagUtilities.TagFromString)
                .Where(t => t != null)
                .ToArray();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 按权重创建物品
        /// </summary>
        public Item CreateItemWithTagsWeighted(int minQuality = 1, int maxQuality = 7, Tag[] requiredTags = null)
        {
            if (!_isInitialized) return null;

            // 特殊情况：不限品质
            if (minQuality == -1 || maxQuality == -1)
            {
                if (requiredTags == null || requiredTags.Length == 0)
                {
                    Debug.LogError($"{LogTag} 品质为 -1 时必须指定标签");
                    return null;
                }
                return CreateItemFromAllQualities(requiredTags);
            }

            // 1. 确定有效品质池
            minQuality = Mathf.Clamp(minQuality, 1, 7);
            maxQuality = Mathf.Clamp(maxQuality, 1, 7);
            
            // 如果有标签需求，先筛选哪些品质里有符合标签的物品
            List<int> validQualities = new List<int>();
            if (requiredTags != null && requiredTags.Length > 0)
            {
                for (int q = minQuality; q <= maxQuality; q++)
                {
                    if (HasAnyItemWithTags(q, requiredTags)) validQualities.Add(q);
                }
                
                if (validQualities.Count == 0) return null;
            }
            else
            {
                // 无标签，区间内所有品质都视为有效
                for (int q = minQuality; q <= maxQuality; q++) validQualities.Add(q);
            }

            // 2. 按权重选择一个品质
            int pickedQuality = PickQualityByWeight(validQualities);

            // 3. 从该品质池创建物品
            return CreateItemFromSpecificQuality(pickedQuality, requiredTags);
        }

        #endregion

        #region Internal Logic

        private Item CreateItemFromSpecificQuality(int quality, Tag[] requiredTags)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0) return null;

            // 筛选符合标签的 ID
            List<int> candidates = pool;
            if (requiredTags != null && requiredTags.Length > 0)
            {
                candidates = pool.Where(id => CheckItemTags(id, requiredTags)).ToList();
            }

            if (candidates.Count == 0) return null;

            int selectedId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return InstantiateItem(selectedId);
        }

        private Item CreateItemFromAllQualities(Tag[] requiredTags)
        {
            // 这是一个较慢的全搜索，仅用于特殊配置
            List<int> allCandidates = new List<int>();
            foreach (var kvp in _qualityItemCache)
            {
                allCandidates.AddRange(kvp.Value.Where(id => CheckItemTags(id, requiredTags)));
            }

            if (allCandidates.Count == 0) return null;
            
            int selectedId = allCandidates[UnityEngine.Random.Range(0, allCandidates.Count)];
            return InstantiateItem(selectedId);
        }

        private int PickQualityByWeight(List<int> validQualities)
        {
            if (validQualities.Count == 1) return validQualities[0];
            if (Mathf.Approximately(qualityBiasPower, 0f)) return validQualities[UnityEngine.Random.Range(0, validQualities.Count)];

            // 计算权重
            List<float> weights = new List<float>();
            float totalWeight = 0f;
            foreach (int q in validQualities)
            {
                // 偏好算法：bias>0 偏高品质，bias<0 偏低品质
                int baseVal = (qualityBiasPower < 0) ? (8 - q) : q;
                float w = Mathf.Pow(Mathf.Max(1, baseVal), Mathf.Abs(qualityBiasPower));
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

        private bool HasAnyItemWithTags(int quality, Tag[] tags)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool)) return false;
            // 简单的存在性检查
            return pool.Any(id => CheckItemTags(id, tags));
        }

        /// <summary>
        /// 实例化并校验物品标签
        /// </summary>
        private bool CheckItemTags(int itemId, Tag[] requiredTags)
        {
            var item = InstantiateItem(itemId);
            if (item == null) return false;

            bool pass = true;
            if (item.Tags?.list != null)
            {
                foreach (var req in requiredTags)
                {
                    if (!item.Tags.list.Exists(t => t.name == req.name))
                    {
                        pass = false;
                        break;
                    }
                }
            }
            else
            {
                pass = false;
            }

            Destroy(item.gameObject);
            return pass;
        }

        private bool IsItemValid(int itemId)
        {
            var item = InstantiateItem(itemId);
            if (item == null) return false;

            string name = item.DisplayName ?? "";
            string desc = item.Description ?? "";
            bool valid = true;

            // 检查名字
            foreach (var black in _nameDescriptionBlacklist)
            {
                if (name.StartsWith(black) || desc.StartsWith(black))
                {
                    valid = false; 
                    break;
                }
            }

            // 检查标签
            if (valid && item.Tags?.list != null)
            {
                foreach (var t in item.Tags.list)
                {
                    if (_tagBlacklist.Contains(t.name))
                    {
                        valid = false;
                        break;
                    }
                }
            }

            Destroy(item.gameObject);
            return valid;
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

        #endregion

        #region Debug Tools (F5-F8)
        // 保留原有的Update测试逻辑，折叠起来以免干扰阅读
        private void Update()
        {
            if (!debugMode) return;
            if (Input.GetKeyDown(KeyCode.F5)) TestSpawn();
            // ... 其他测试键位可在此处保留
        }
        private void TestSpawn() { /* ... */ }
        #endregion
    }
}