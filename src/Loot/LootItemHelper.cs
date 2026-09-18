using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Duckov.ItemBuilders;
using Duckov.Utilities;
using EliteEnemies.Settings;
using ItemStatsSystem;
using Saves;
using UnityEngine;

namespace EliteEnemies.Loot
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
        // 存储物品ID对应的标签集合
        private Dictionary<int, HashSet<string>> _itemTagCache = new Dictionary<int, HashSet<string>>();

        private bool _isInitialized = false;

        /// <summary>
        /// 缓存构建完成后能不能用（调试工具导出前要先看这个）。
        /// </summary>
        internal bool IsCacheReady => _isInitialized;

        /// <summary>只读访问器，供同程序集的调试工具导出缓存快照。</summary>
        internal IReadOnlyDictionary<int, List<int>> QualityItemCache => _qualityItemCache;

        /// <summary>只读访问器，供同程序集的调试工具导出标签快照。</summary>
        internal IReadOnlyDictionary<int, HashSet<string>> ItemTagCache => _itemTagCache;

        // 物品缓存构建期的两个统计，见 ProcessItemAndCacheTags：
        //   · _rejectedItemCount   —— 读元数据**失败**（真异常），原先什么都不记，导致池子悄悄缩水查不出原因
        //   · _worldObjectExcluded —— 被**刻意**排除的「存档持久化的世界物件」（商店/任务/奖励等），不是失败
        // 两者分开计数：混在一起会让人以为过滤规则出错了。
        private const int RejectedSampleLimit = 5;
        private int _rejectedItemCount;
        private int _worldObjectExcluded;
        private readonly List<string> _rejectedSamples = new List<string>();

        // ========== 黑名单配置 ==========
        // 其他mod物品
        private static readonly HashSet<int> _modItemIdBlacklist = new HashSet<int>();
        // 原版
        private readonly HashSet<int> _idBlacklist = new HashSet<int>
        {
            1158, // 水族箱
            397, // 比特币矿机
            769, // 蛋清能源碎片
            913, // 口口头盔
            910, // 口口防弹衣
            1151, // 防空系统密钥
        };
        
        private readonly string[] _nameDescriptionBlacklist =
        {
            "Item_", "Quest_", "BP_"
        };

        private readonly string[] _tagBlacklist =
        {
            "DestroyOnLootBox", "DestroyInBase", "Formula", "Formula_Blueprint", "Quest"
        };
        
        private void Awake()
        {
            var others = FindObjectsByType<LootItemHelper>(FindObjectsSortMode.None);
            if (others.Length > 1)
            {
                Destroy(this.gameObject);
                return;
            }
            DontDestroyOnLoad(this.gameObject);
            qualityBiasPower = GameConfig.ItemQualityBias;
            InitializeModIdBlacklist();
        }

        private void Start()
        {
            StartCoroutine(InitializeItemCacheAsync());
        }

        private void OnDestroy()
        {
            if (Verbose) Debug.Log($"{LogTag} LootItemHelper 被销毁");
        }
        
        private void InitializeModIdBlacklist()
        {
            _modItemIdBlacklist.Clear();

            // 1. “三角鸭武器和配件扩展2.6.2”
            _modItemIdBlacklist.Add(12013);
            _modItemIdBlacklist.Add(12014);

            // 2. “ArcaneEra(beta)” ID 范围：421455000-421455033
            for (int id = 421455000; id <= 421455033; id++)
            {
                _modItemIdBlacklist.Add(id);
            }

            Debug.Log($"{LogTag} MOD ID黑名单初始化完成，共屏蔽 {_modItemIdBlacklist.Count} 个MOD物品。");
        }
        
        private IEnumerator InitializeItemCacheAsync()
        {
            if (Verbose) Debug.Log($"{LogTag} 开始初始化物品缓存（异步模式）");
            
            _qualityItemCache.Clear();
            _itemTagCache.Clear();
            _rejectedItemCount = 0;
            _worldObjectExcluded = 0;
            _rejectedSamples.Clear();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            // 独立的总耗时计时器。**上面那个 stopwatch 是「每帧预算」用的**，每 10 件就
            // 在超过 5ms 时重置，所以它量不出总时间。两者用途不同，不要合并。
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            int scanned = 0;

            // ⚠ **CPU 与墙钟必须分开报**。只报墙钟会误导：本协程会 `yield return null`
            // 让帧，而墙钟把等待帧的时间也算进去了——实测第一次跑出 2660 ms，
            // 而那段时间里别的模组正在做贴图 I/O 与更新检查，帧时间被拉长了很久。
            // 「先量再优化」的前提是**量对东西**：优化能改善的只有 CPU 那一半。
            double cpuMs = 0;
            int frameYields = 0;

            float maxMillisecondsPerFrame = 5f;

            for (int quality = 1; quality <= 7; quality++)
            {
                int[] itemIds = null;

                try
                {
                    ItemFilter filter = new ItemFilter
                    {
                        requireTags = new Tag[0],
                        excludeTags = new Tag[0],
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
                        scanned++;
                        if (ProcessItemAndCacheTags(itemId))
                        {
                            validItems.Add(itemId);
                        }
                        
                        processedCount++;
                        
                        if (processedCount % 10 == 0 && stopwatch.Elapsed.TotalMilliseconds > maxMillisecondsPerFrame)
                        {
                            // ⚠ **顺序要紧：先 Reset 停表 → 让帧 → 恢复后才 Start。**
                            //
                            // 原先是 `Reset(); Stopwatch.Start(); yield return null;`——停表之后
                            // 立刻又启动，**让帧期间它一直在走**。这一处错同时毁了两件事：
                            //   · 累计出来的「CPU」其实还是墙钟。实测两者都报 2730 ms，露了馅
                            //     （真让了 85 次帧，墙钟不可能等于纯 CPU）。
                            //   · **每帧 5ms 的预算从来没生效过**：恢复后 elapsed 里已经含了整帧
                            //     等待（那次约 32ms），下次检查必然 >5ms，于是**每 10 件就让一次帧**
                            //     （887 件让了 85 次，正是 887/10 ≈ 89 的量级）。
                            cpuMs += stopwatch.Elapsed.TotalMilliseconds;
                            stopwatch.Reset();          // Reset 会停表，让帧期间不计时
                            frameYields++;
                            yield return null;
                            stopwatch.Start();          // 让帧结束后才重新计时
                        }
                    }
                }

                _qualityItemCache[quality] = validItems;
                if (Verbose) Debug.Log($"{LogTag} 品阶 {quality}: {validItems.Count} 个可用物品");

                // ⚠ **这里也必须停表**。它是「每个品阶末尾」的**无条件**让帧，
                //    原先只修了循环体内那处预算让帧，这一处漏掉了——于是这 7 帧的等待
                //    全漏进了 CPU 统计。实测两轮才对上：7 次预算让帧 ÷ 887 件 ≈ 每块 127 件，
                //    而阈值只有 5ms；真有 840ms 的 CPU，第 10 件就该让帧了。
                //    唯一自洽的解释是每件约 0.04ms（真 CPU ≈ 35ms），
                //    报出来的 840ms 里约 700ms 是这 7 帧漏进去的。
                cpuMs += stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Reset();
                frameYields++;
                yield return null;
                stopwatch.Start();
            }

            _isInitialized = true;

            // 收尾那一段（未达到让帧阈值的最后一批）也要计进 CPU
            cpuMs += stopwatch.Elapsed.TotalMilliseconds;

            // 耗时。**刻意常开**（不受 Verbose 控制）：这是本模组最大的一处启动开销，
            // 而在此之前它从未被量过——「优化」不能建立在印象上。
            int cached = 0;
            foreach (var pool in _qualityItemCache.Values) cached += pool.Count;
            long wallMs = total.ElapsedMilliseconds;
            Debug.Log($"{LogTag} 物品缓存构建完成：扫描 {scanned} 件、收录 {cached} 件、" +
                      $"排除世界物件 {_worldObjectExcluded} 件、读取失败 {_rejectedItemCount} 件；" +
                      $"**CPU {cpuMs:F0} ms / 墙钟 {wallMs} ms（让帧 {frameYields} 次）**");

            // ── 交叉校验：把「指标可信」这件事写进代码，而不是靠人眼 ──
            //
            // 这个统计**连着写错过两轮**，而两轮它都照样输出一个像模像样的数字：
            //   第一轮：把 Reset/Start 写反，让帧期间表在走 → CPU 与墙钟**完全相等**
            //   第二轮：只修了预算让帧，漏了品阶末尾那处无条件让帧 → CPU 仍偏高约 20 倍
            // 两次都是靠「和让帧次数对不上」才发现的。所以判据留在这里自动跑：
            //   · CPU 不可能超过墙钟（真超了说明表没停）
            //   · 平均每次让帧的「CPU」若接近整帧时长，说明让帧时间漏进来了
            if (cpuMs > wallMs || (frameYields > 0 && cpuMs / frameYields > 40))
            {
                Debug.LogWarning($"{LogTag} 耗时统计自相矛盾（CPU {cpuMs:F0} ms / 墙钟 {wallMs} ms / " +
                                 $"让帧 {frameYields} 次）——让帧期间多半没停表，这个数**不可信**。");
            }

            // 汇总「读取失败的物品」。**这条日志是刻意常开的（不受 Verbose 控制）**：
            // 它对应的失败模式是「掉落池悄悄缩水」，不报出来就没人会发现。
            if (_rejectedItemCount > 0)
            {
                Debug.LogWarning($"{LogTag} 有 {_rejectedItemCount} 个物品读取元数据失败、已排除出掉落池" +
                                 $"（样本：{string.Join("；", _rejectedSamples)}）。");
            }

            if (Verbose) Debug.Log($"{LogTag} 物品缓存初始化完成");
        }

        /// <summary>
        /// 处理单个物品：验证黑名单并提取标签缓存。
        ///
        /// <para><b>本方法不实例化任何物品。</b> 名字/描述/标签一律走
        /// <see cref="ItemAssetsCollection.GetMetaData"/>。原先这里对**每一个**物品
        /// <c>InstantiateSync</c> 再 <c>Destroy</c>，只为读三个字段——那是本模组最大的一处
        /// 启动开销，而且实例化会同步跑预制体的 <c>Awake</c>，于是「商店」这类需要存档状态的
        /// 物件会抛 NRE（见下面第 3 步）。</para>
        ///
        /// <para>元数据的两条来源都不实例化：原版条目读资产里预烘焙的 <c>Entry.metaData</c>；
        /// 动态条目（mod 物品）由 <c>new ItemMetaData(prefab)</c> 从预制体读字段并缓存。</para>
        /// </summary>
        private bool ProcessItemAndCacheTags(int itemId)
        {
            // 1. ID 黑名单
            if (_idBlacklist.Contains(itemId)) return false;
            if (_modItemIdBlacklist.Contains(itemId)) return false;

            // 2. 预制体存在性。GetMetaData 对未知 id 返回 default(ItemMetaData)，
            //    用预制体判有效性比用 meta 的某个字段可靠；下面第 3 步也要用它。
            Item prefab = ItemAssetsCollection.GetPrefab(itemId);
            if (prefab == null) return false;

            // 3. **排除「存档持久化的世界物件」**。
            //
            //    这一条不是可有可无的：改动前，这道闸门是由 InstantiateSync 抛异常**顺带**充当的
            //    ——实测那一次会话恰好挡掉了 1 件（预制体上挂着 StockShop，它的
            //    Awake → Load → SetupSaveData 在无存档场景下抛 NRE）。
            //    一旦不再实例化，那道「碰巧有效」的闸门就消失了，商店会重新进掉落池，
            //    甚至可能掉出一座**商店**。
            //
            //    判据用 ISaveDataProvider 而**不是**硬编码 StockShop：
            //      · 它正是那个 NRE 的成因本身（SetupSaveData 就是这个接口的方法）；
            //      · 覆盖面恰是我们要的——实现它的全是 StockShop / Quest / Task / Reward /
            //        各种 Manager，都是靠存档持久化的世界物件，不是掉落物。
            //    硬编码具体类型属于「过拟合一条观测」，游戏下次加一种可放置物件就会漏。
            if (prefab.GetComponentInChildren<ISaveDataProvider>(true) != null)
            {
                _worldObjectExcluded++;
                return false;
            }

            ItemMetaData meta;
            try
            {
                meta = ItemAssetsCollection.GetMetaData(itemId);
            }
            catch (Exception ex)
            {
                // **曾经这里什么都不记**——后果是「某个物品被静默踢出掉落池」完全无迹可循。
                // 不逐条打日志是因为本方法要遍历**全部**物品，批量失败会刷屏；
                // 改为计数 + 采样，结束时汇总一条（见 InitializeItemCacheAsync 末尾）。
                _rejectedItemCount++;
                if (_rejectedSamples.Count < RejectedSampleLimit)
                {
                    _rejectedSamples.Add($"id={itemId} {ex.GetType().Name}: {ex.Message}");
                }
                return false;
            }

            // 4. 名称/描述黑名单
            string name = meta.DisplayName ?? "";
            string desc = meta.Description ?? "";
            foreach (string prefix in _nameDescriptionBlacklist)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    desc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 5. 标签黑名单 + 建标签缓存
            HashSet<string> currentTags = new HashSet<string>();

            if (meta.tags != null)
            {
                foreach (var tag in meta.tags)
                {
                    if (tag == null || string.IsNullOrEmpty(tag.name)) continue;

                    foreach (string blacklistedTag in _tagBlacklist)
                    {
                        if (tag.name.Equals(blacklistedTag, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    currentTags.Add(tag.name);
                }
            }

            _itemTagCache[itemId] = currentTags;
            return true;

        }

        /// <summary>
        /// 检查物品ID是否在白名单缓存中
        /// </summary>
        public bool IsItemWhitelisted(int itemId)
        {
            if (!_isInitialized) return false; 
            return _itemTagCache.ContainsKey(itemId);
        }

        private bool ItemHasAllTags(int itemId, Tag[] requiredTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
                return true;

            if (!_itemTagCache.TryGetValue(itemId, out var itemTags))
            {
                return false;
            }

            foreach (Tag requiredTag in requiredTags)
            {
                if (!itemTags.Contains(requiredTag.name))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 按品质权重创建物品
        /// </summary>
        public Item CreateItemWithTagsWeighted(int minQuality = 1, int maxQuality = 7, Tag[] requiredTags = null)
        {
            if (!_isInitialized) return null;

            if (minQuality == -1 || maxQuality == -1)
            {
                if (requiredTags == null || requiredTags.Length == 0) return null;
                return CreateItemWithTagsFromAllQualities(requiredTags);
            }

            minQuality = Mathf.Clamp(minQuality, 1, 7);
            maxQuality = Mathf.Clamp(maxQuality, 1, 7);
            
            if (requiredTags != null && requiredTags.Length > 0)
            {
                List<int> validQualities = new List<int>();

                for (int q = minQuality; q <= maxQuality; q++)
                {
                    if (!_qualityItemCache.TryGetValue(q, out var pool) || pool.Count == 0)
                        continue;

                    foreach (int itemId in pool)
                    {
                        if (ItemHasAllTags(itemId, requiredTags))
                        {
                            validQualities.Add(q);
                            break;
                        }
                    }
                }

                if (validQualities.Count == 0) return null;
                
                int pickedQuality = PickQualityByWeightFromValidQualities(validQualities, minQuality, maxQuality);
                return CreateItemWithTagsFromQuality(pickedQuality, requiredTags);
            }
            else
            {
                int pickedQuality = PickQualityByWeight(minQuality, maxQuality);
                return CreateItemFromQuality(pickedQuality);
            }
        }


        private Item CreateItemFromQuality(int quality)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0) return null;
            int itemId = pool[UnityEngine.Random.Range(0, pool.Count)];
            return InstantiateItem(itemId);
        }

        private Item CreateItemWithTagsFromQuality(int quality, Tag[] requiredTags)
        {
            if (!_qualityItemCache.TryGetValue(quality, out var pool) || pool.Count == 0) return null;

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

        /// <summary>
        /// 实例化普通物品
        /// </summary>
        private Item InstantiateItem(int id)
        {
            try
            {
                var item = ItemAssetsCollection.InstantiateSync(id);
                if (item != null)
                {
                    item.Initialize();
                    // FromInfoKey 是**本地化键**，由游戏在显示时解析
                    // （`ToPlainText()` → override 优先）。这个键已进 CSV（4 语言），
                    // 并由 LocalizationManager 全量推给游戏本地化器——
                    // 早先这里硬编码了中文（英文玩家也看到中文），已删。
                    item.FromInfoKey = "EliteEnemies_LootSource";
                }
                return item;
            }
            catch { return null; }
        }

        /// <summary>
        /// 使用 ItemBuilder 创建自定义程序化物品
        /// </summary>
        public Item CreateCustomItem(int typeId, int stackCount = 1, Sprite icon = null)
        {
            try
            {
                var builder = ItemBuilder.New()
                    .TypeID(typeId);

                if (stackCount > 1)
                    builder.EnableStacking(Mathf.Max(stackCount, 99), stackCount);
                else
                    builder.DisableStacking();

                if (icon != null)
                    builder.Icon(icon);
                
                var item = builder.Instantiate();
                item.Initialize();
                return item;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 创建自定义物品失败: {ex.Message}");
                return null;
            }
        }
        
        // ========== 权重算法 ==========

        private int PickQualityByWeight(int minQuality, int maxQuality)
        {
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