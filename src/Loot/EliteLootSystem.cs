using System;
using System.Collections.Generic;
using Duckov.Utilities;
using EliteEnemies.DebugTools;
using EliteEnemies.Affixes;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using EliteEnemies.Stats;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EliteEnemies.Loot
{
    /// <summary>
    /// 精英敌人掉落系统。
    ///
    /// <para><b>它不再自己挂 Harmony 补丁。</b>入口在 <c>EliteEnemies.Patches.LootboxPatch</c>——
    /// 那个类把「填充精英掉落 → 史莱姆箱走物理 → 削减分裂体掉落」三步按**固定顺序**串起来，
    /// 顺序在代码里可见，不再依赖 Harmony 对同一方法多个补丁的隐式排序。
    /// 原因见 <c>LootboxPatch</c> 的注释：第三步必须在第一步之后，否则分裂体的箱子
    /// 会被重新填满（词条说明是「分身仅掉落 2 件物品」，那正是要防的）。</para>
    /// </summary>
    public static class EliteLootSystem
    {
        private const string LogTag = "[EliteEnemies.EliteLootSystem]";
        private static bool Verbose = false;
        public static float GlobalDropRate = 1.0f;

        private static readonly HashSet<int> ProcessedLootBoxes = new HashSet<int>();
        private static LootItemHelper _lootHelper = null;

        // 弱怪惩罚配置
        private static readonly Dictionary<string, (float dropRatePenalty, int qualityDowngrade)> WeakEnemyPenalties =
            new Dictionary<string, (float, int)>(StringComparer.OrdinalIgnoreCase)
            { 
                { "EnemyPreset_Prison_Melee", (0.4f, 1) },
                { "EnemyPreset_Prison_Pistol", (0.4f, 1) },
                { "EnemyPreset_Scav", (0.6f, 1) },
                { "EnemyPreset_Scav_Elete", (0.6f, 1) },
                { "EnemyPreset_Scav_Farm", (0.6f, 1) },
                { "EnemyPreset_Scav_low", (0.6f, 1) },
                { "EnemyPreset_Scav_low_ak74", (0.6f, 1) },
                { "EnemyPreset_Scav_Melee", (0.7f, 0) }
            };
        
        private static readonly Dictionary<string, int> MapQualityCaps = new Dictionary<string, int>
        {
            { "Level_Guide_1", 3 },
            { "Level_Guide_Main", 3 },
            { "Level_GroundZero_Main", 5 },
            { "Level_GroundZero_1", 5 },
            { "Level_HiddenWarehouse", 6 },
            { "Level_Farm_Main", 7 },
            { "Level_Farm_01", 7 }
        };
                
        private static readonly string FromInfoKeyFixed = "EliteEnemies_EliteLoot_Fixed";
        private static readonly string FromInfoKeyRandom = "EliteEnemies_EliteLoot_Random";
        private static readonly string FromInfoKeyBonus = "EliteEnemies_EliteLoot_Bonus";
        
        #region 入口与三步处理

        /// <summary>
        /// 第一步：把精英词条对应的掉落填进箱子。
        /// 由 <c>LootboxPatch</c> 调用——**不要**自己挂 <c>[HarmonyPatch]</c>，否则顺序又不可控了。
        /// </summary>
        /// <param name="character">
        /// 掉落箱归属的角色。由编排入口 <c>InteractableLootboxPatch</c> **统一反查一次**后传入——
        /// 三步都要它，而 <see cref="GetCharacterFromItem"/> 带层级遍历兜底，各自查一遍等于付三遍。
        /// </param>
        internal static void Apply(InteractableLootbox lootbox, Item item,
                                   CharacterMainControl character, Vector3 position)
        {
            try
            {
                if (lootbox == null || item == null) return;

                // 1. 校验精英身份（角色的反查已由调用方统一完成）
                if (character == null) return;

                var marker = character.GetComponent<EliteMarker>();
                if (marker == null || marker.Affixes == null || marker.Affixes.Count == 0) return;

                // 2. 防止重复处理
                int hash = position.GetHashCode() ^ item.GetHashCode();
                if (ProcessedLootBoxes.Contains(hash)) return;
                ProcessedLootBoxes.Add(hash);

                // 3. 执行掉落逻辑
                ProcessEliteLoot(lootbox, character, marker.Affixes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 处理掉落时发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region Core Logic

        private static void ProcessEliteLoot(InteractableLootbox lootbox, CharacterMainControl character, List<string> affixes)
        {
            GlobalDropRate = EliteEnemyCore.Config.DropRateMultiplier;

            // 将配置的品质偏好应用到 Helper 实例
            var helper = GetLootItemHelper();
            if (helper != null)
            {
                helper.qualityBiasPower = EliteEnemyCore.Config.ItemQualityBias;
            }
            SessionStats.RecordLootAttempt();
            
            string charName = character.characterPreset != null ? character.characterPreset.name : character.name;
            
            // 获取惩罚参数
            GetEnemyPenalty(charName, out float dropPenalty, out int qualityDowngrade);

            // 预先扩容（防止格子不够）
            PreExpandInventory(lootbox.Inventory, affixes);

            if (Verbose) Debug.Log($"{LogTag} >>> 开始处理 [{charName}] 的掉落 (词缀数:{affixes.Count}) | 掉率修正:{dropPenalty:P0} | 全局倍率:{GlobalDropRate:F1} | 品质偏好:{helper.qualityBiasPower:F1}");

            // 阶段 1: 词缀固定掉落
            ProcessFixedLoot(lootbox, affixes, dropPenalty);

            // 阶段 2: 词缀随机配置
            ProcessRandomConfigLoot(lootbox, affixes, dropPenalty, qualityDowngrade);

            // 阶段 3: 稀有度保底奖励
            if (EliteEnemyCore.Config.EnableBonusLoot)
            {
                ProcessRarityBonusLoot(lootbox, affixes, charName, dropPenalty, qualityDowngrade);
            }
        }

        /// <summary>
        /// 阶段1: 处理词缀的固定掉落组
        /// </summary>
        private static void ProcessFixedLoot(InteractableLootbox lootbox, List<string> affixes, float enemyPenalty)
        {
            var helper = GetLootItemHelper();
            var lootGroups = EliteAffixes.GetLootGroupsForAffixes(affixes);
            
            foreach (var group in lootGroups)
            {
                if (group == null || group.Count == 0) continue;

                var pick = group[UnityEngine.Random.Range(0, group.Count)];
                if (pick == null) continue;

                if (helper != null)
                {
                    if (!helper.IsItemWhitelisted(pick.ItemID))
                    {
                        Debug.LogWarning($"{LogTag} 固定掉落 {pick.ItemID} 因在黑名单中被拦截");
                        continue;
                    }
                }

                float chance = CalculateChance(pick.DropChance, enemyPenalty);

                // 每次成功判定各自掷一次数量——这样期望件数严格等于
                // 「倍率 × 概率 × 期望件数」，与倍率的线性关系成立。
                int hits = RollCount(chance);
                for (int i = 0; i < hits; i++)
                {
                    int count = UnityEngine.Random.Range(pick.MinCount, pick.MaxCount + 1);
                    AddItemToInventory(lootbox, pick.ItemID, count, "词缀固定", chance, FromInfoKeyFixed);
                }
            }
        }

        /// <summary>
        /// 阶段2: 处理词缀定义的随机池配置
        /// </summary>
        private static void ProcessRandomConfigLoot(InteractableLootbox lootbox, List<string> affixes, float enemyPenalty, int qualityDowngrade)
        {
            var helper = GetLootItemHelper();
            if (helper == null) return;
            int mapCap = GetCurrentMapQualityCap();

            foreach (var affixName in affixes)
            {
                if (!EliteAffixes.TryGetAffix(affixName, out var affixData)) continue;
                if (affixData.RandomLootConfigs == null) continue;

                foreach (var config in affixData.RandomLootConfigs)
                {
                    float chance = CalculateChance(config.DropChance, enemyPenalty);

                    // 倍率 = 判定次数：每成功一次就整组发一遍
                    int hits = RollCount(chance);
                    if (hits == 0) continue;

                    Tag[] tags = ParseTags(config.TagNames);

                    int effectiveMax = Mathf.Max(1, mapCap - qualityDowngrade);
                    int targetMin, targetMax;

                    // 检查是否为全随机
                    if (config.MinQuality == -1)
                    {
                        targetMin = 1;
                        targetMax = effectiveMax;
                    }
                    else
                    {
                        int rawMin = Mathf.Max(1, config.MinQuality - qualityDowngrade);
                        int rawMax = Mathf.Max(1, config.MaxQuality - qualityDowngrade);
                        targetMax = Mathf.Min(rawMax, effectiveMax);
                        targetMin = Mathf.Min(rawMin, targetMax);
                    }
                    
                    // 两层逻辑不一样，第一次是多个不同物品，第二层是多个相同物品
                    for (int h = 0; h < hits; h++)
                    {
                        for (int i = 0; i < config.ItemCount; i++)
                        {
                            Item item = helper.CreateItemWithTagsWeighted(targetMin, targetMax, tags);

                            if (item != null)
                            {
                                int stackCount = UnityEngine.Random.Range(config.MinStack, config.MaxStack + 1);
                                AddItemInstanceToInventory(lootbox, item, stackCount, $"词缀随机({affixName})", chance, FromInfoKeyRandom);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 阶段3: 稀有度积分奖励
        /// </summary>
        private static void ProcessRarityBonusLoot(InteractableLootbox lootbox, List<string> affixes, string charName, float enemyPenalty, int qualityDowngrade)
        {
            var helper = GetLootItemHelper();
            if (helper == null) return;
            
            int mapCap = GetCurrentMapQualityCap();
            bool isBoss = PresetDirectory.BossPresets.Contains(charName);
            
            // 计算积分
            float powerScore = isBoss ? 5f : 0f;
            foreach (var name in affixes)
            {
                if (EliteAffixes.TryGetAffix(name, out var data))
                    powerScore += GetRarityScore(data.Rarity);
                else
                    powerScore += 1f;
            }

            // 计算概率（不含全局倍率——倍率走判定次数，见 RollCount）
            float baseChance = 0.30f + (powerScore * 0.05f);
            float chance = Mathf.Clamp01(baseChance * enemyPenalty);

            int hits = RollCount(chance);
            if (hits == 0)
            {
                if (Verbose) Debug.Log($"{LogTag} [稀有度奖励] 未触发 (分:{powerScore:F1}, 率:{chance:P0})");
                return;
            }

            int baseQuality = Mathf.FloorToInt(1.5f + (powerScore / 3.0f));
            // 先应用弱怪降级
            int rawMinQ = Mathf.Clamp(baseQuality - qualityDowngrade, 1, 6);
            int rawMaxQ = Mathf.Clamp(rawMinQ + UnityEngine.Random.Range(1, 3), rawMinQ, 7);

            // BOSS保底修正
            if (isBoss && qualityDowngrade == 0 && rawMinQ < 3) 
            { 
                rawMinQ = 3; 
                if (rawMaxQ < 3) rawMaxQ = 3; 
            }

            // 应用地图 Cap
            int maxQ = Mathf.Min(rawMaxQ, mapCap);
            int minQ = Mathf.Min(rawMinQ, maxQ);
            
            
            // 每次成功判定生成 1 个稀有度奖励
            string sourceLabel = isBoss ? "BOSS奖励" : "稀有度奖励";
            for (int h = 0; h < hits; h++)
            {
                Item item = helper.CreateItemWithTagsWeighted(minQ, maxQ, null);
                if (item != null)
                {
                    AddItemInstanceToInventory(lootbox, item, 1, sourceLabel, chance, FromInfoKeyBonus);
                }
            }
        }

        #endregion

        #region Helper Methods
        
        private static int GetCurrentMapQualityCap()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (MapQualityCaps.TryGetValue(sceneName, out int cap))
            {
                int mapCap = Mathf.Clamp(cap, 1, 7);
                return mapCap;
            }
            return 7;
        }
        
        private static void AddItemToInventory(InteractableLootbox lootbox, int itemId, int count, string sourcePool, float chance, string sourceKey)
        {
            if (count <= 0) return;
            var item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item != null)
            {
                item.Initialize();
                AddItemInstanceToInventory(lootbox, item, count, sourcePool, chance, sourceKey);
            }
        }

        private static void AddItemInstanceToInventory(InteractableLootbox lootbox, Item item, int count, string sourcePool, float chance, string sourceKey)
        {
            if (item == null || count <= 0) return;

            string itemName = item.DisplayName;
            string qualityStr = item.Quality.ToString();
            
            SessionStats.RecordDrop(sourcePool, item.Quality, count);
            
            // 核心逻辑：添加第一个
            item.Detach();
            item.FromInfoKey = sourceKey;
            lootbox.Inventory.AddAndMerge(item, 0);
            
            // 如果数量 > 1，复制剩余的
            for (int i = 1; i < count; i++)
            {
                var clone = ItemAssetsCollection.InstantiateSync(item.TypeID);
                if (clone != null)
                {
                    clone.Initialize();
                    clone.FromInfoKey = sourceKey;
                    clone.Detach();
                    lootbox.Inventory.AddAndMerge(clone, 0);
                }
            }
            
            if (Verbose) Debug.Log($"{LogTag} + [来源:{sourcePool} / {sourceKey}] 获得: {itemName} (Q{qualityStr}) x{count} [概率:{chance:P0}]");
        }

        /// <summary>
        /// 基础概率 × 弱怪惩罚。**不含全局倍率**——倍率现在走判定次数，见 <see cref="RollCount"/>。
        /// </summary>
        private static float CalculateChance(float baseChance, float penalty)
        {
            return Mathf.Clamp01(baseChance * penalty);
        }

        /// <summary>
        /// 按全局倍率重复判定，返回**成功次数**。
        ///
        /// <para><b>为什么不是「把概率乘上倍率」</b>：那样会被 <c>clamp01</c> 吃掉效果。
        /// 实测词条表里 67 条掉率配置，倍率调到 2.0 时已有 **43 条（64%）钉死在 100%**，
        /// 滑块从 2 拖到 3 对它们毫无变化——那是数值在撒谎，不是玩家看不懂。
        /// 改成重复判定后，<c>期望掉落件数 = 倍率 × 基础概率 × 件数</c>，
        /// 倍率真正线性、可预期，设置项描述里那句话才**可核对为真**。</para>
        ///
        /// <para><b>与旧行为的关系</b>（逐条验过）：</para>
        /// <list type="bullet">
        /// <item>倍率 1.0 —— 等价于原来的一次判定（1 次 roll、概率不变），
        /// <b>默认配置下掉落与改动前完全一致</b>。</item>
        /// <item>倍率 &lt; 1 —— 期望一致。例如 0.5：50% 概率来 1 次判定，期望 0.5 次。</item>
        /// <item>倍率 &gt; 1 —— 掉落**变多**，那正是此前被 clamp 吃掉的部分。</item>
        /// </list>
        ///
        /// <para>整数部分必定判定，小数部分按概率多判定一次，以此逼近任意小数倍率。</para>
        /// </summary>
        private static int RollCount(float chance)
        {
            float multiplier = GlobalDropRate;
            if (multiplier <= 0f || chance <= 0f) return 0;

            int rolls = (int)multiplier;
            if (UnityEngine.Random.value < (multiplier - rolls)) rolls++;

            int hits = 0;
            for (int i = 0; i < rolls; i++)
            {
                if (UnityEngine.Random.value <= chance) hits++;
            }
            return hits;
        }

        private static void GetEnemyPenalty(string resourceName, out float dropRate, out int qualityDowngrade)
        {
            dropRate = 1f;
            qualityDowngrade = 0;

            if (string.IsNullOrEmpty(resourceName)) return;

            // 在进行字典查找前，先剥离掉 EggSpawnHelper 可能添加的后缀
            // 这样 EnemyPreset_Scav_low_EE_Split 就能匹配到 EnemyPreset_Scav_low
            string cleanName = resourceName;
            int suffixIndex = cleanName.IndexOf("_EE_");
            if (suffixIndex > 0)
            {
                cleanName = cleanName.Substring(0, suffixIndex);
            }

            if (WeakEnemyPenalties.TryGetValue(cleanName, out var penalty))
            {
                dropRate = penalty.dropRatePenalty;
                qualityDowngrade = penalty.qualityDowngrade;
            }
        }

        private static void PreExpandInventory(Inventory inventory, List<string> affixes)
        {
            // 估算需要的格子数，避免扩容多次
            int estimate = 2; // 基础余量 + 奖励
            foreach(var aff in affixes) estimate += 2; // 假设每个词缀最多贡献2组

            // 倍率现在是**判定次数**（见 RollCount），件数会按倍率放大到最多 3 倍。
            // 估算必须跟着放大，否则倍率 > 1 时箱子可能装不下、掉落被吞掉。
            estimate = Mathf.CeilToInt(estimate * Mathf.Max(1f, GlobalDropRate));

            int newCap = inventory.Capacity + estimate;
            inventory.SetCapacity(newCap);
        }

        /// <summary>
        /// 从掉落箱对应的 Item 反查角色。
        ///
        /// <para>公开给同程序集的其它掉落箱处理步骤复用——此前这段逻辑在
        /// <c>EliteLootSystem</c> / <c>SlimeLootboxPhysicsPatch</c> / <c>SplitLootPatch</c>
        /// 里各抄了一份（当时三处的反射写法还不一致，第 3 步清理反射时统一成了直接调用，
        /// 但重复仍在）。现在只此一份。</para>
        /// </summary>
        internal static CharacterMainControl GetCharacterFromItem(Item item)
        {
            if (item == null) return null;

            // Item.GetCharacterItem() 是 public 方法（TeamSoda.ItemStatsSystem/ItemStatsSystem/Item.cs:752）
            Item characterItem = item.GetCharacterItem();
            if (characterItem != null)
            {
                CharacterMainControl character = characterItem.GetComponent<CharacterMainControl>();
                if (character != null) return character;
            }

            // 物品不在角色物品树下时，退回沿父节点向上找
            Transform current = item.transform;
            int depth = 0;
            while (current != null && depth < 10)
            {
                CharacterMainControl character = current.GetComponent<CharacterMainControl>();
                if (character != null) return character;
                current = current.parent;
                depth++;
            }

            // Debug.LogWarning($"{LogTag} 无法找到对应的 CharacterMainControl");
            return null;
        }

        private static LootItemHelper GetLootItemHelper()
        {
            if (_lootHelper == null) _lootHelper = UnityEngine.Object.FindFirstObjectByType<LootItemHelper>();
            return _lootHelper;
        }

        private static float GetRarityScore(AffixRarity rarity) => rarity switch
        {
            AffixRarity.Common => 1f,
            AffixRarity.Uncommon => 2f,
            AffixRarity.Rare => 3f,
            AffixRarity.Epic => 4f,
            AffixRarity.Legendary => 5f,
            _ => 1f
        };
        
        private static Tag[] ParseTags(string[] tagNames)
        {
            if (tagNames == null || tagNames.Length == 0) return null;
            var list = new List<Tag>();
            foreach (var name in tagNames)
            {
                var t = TagUtilities.TagFromString(name);
                if (t != null) list.Add(t);
            }
            return list.ToArray();
        }

        public static void ClearCache() => ProcessedLootBoxes.Clear();
        
        #endregion
    }
}