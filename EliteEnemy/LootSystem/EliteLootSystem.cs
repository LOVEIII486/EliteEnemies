using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Utilities;
using HarmonyLib;
using UnityEngine;
using ItemStatsSystem;
using EliteEnemies.DebugTool;

namespace EliteEnemies
{
    /// <summary>
    /// 精英敌人掉落系统
    /// 1. Patch InteractableLootbox.CreateFromItem
    /// 2. 在战利品箱刚创建时拦截
    /// 3. 检查是否来自精英敌人
    /// 4. 如果是，修改战利品箱内容
    /// </summary>
    [HarmonyPatch(typeof(InteractableLootbox), "CreateFromItem")]
    public static class EliteLootSystem
    {
        private const string LogTag = "[EliteEnemies.EliteLootSystem]";
        private static bool Verbose = false; // 详细日志

        private static readonly HashSet<int> ProcessedLootBoxes = new HashSet<int>();
        public static float GlobalDropRate = 1.0f; // 全局掉率倍率 0-3


        private static LootItemHelper _lootHelper = null;

        private static readonly Dictionary<string, (float dropRatePenalty, int qualityDowngrade)> WeakEnemyPenalties =
            new Dictionary<string, (float, int)>
            {
                { "Cname_Scav", (0.5f, 1) },
                { "Cname_ScavRage", (0.6f, 1) },
                { "Cname_Wolf", (0.6f, 1) },
            };

        /// <summary>
        /// 拦截战利品箱创建
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(
            InteractableLootbox __result, // 刚创建的战利品箱
            Item item,
            Vector3 position,
            Quaternion rotation,
            bool moveToMainScene,
            InteractableLootbox prefab,
            bool filterDontDropOnDead)
        {
            try
            {
                if (__result == null || item == null)
                {
                    if (Verbose) Debug.Log($"{LogTag} 基础检查失败：__result 或 item 为 null");
                    return;
                }

                CharacterMainControl character = GetCharacterFromItem(item);
                if (character == null)
                {
                    if (Verbose) Debug.Log($"{LogTag} 无法从 item 获取 character");
                    return;
                }

                var marker = character.GetComponent<EliteEnemyCore.EliteMarker>();
                if (marker == null || marker.Affixes == null || marker.Affixes.Count == 0)
                {
                    if (Verbose) Debug.Log($"{LogTag} {character.name} 不是精英，跳过");
                    return;
                }

                int hash = position.GetHashCode() ^ item.GetHashCode();
                if (ProcessedLootBoxes.Contains(hash))
                {
                    if (Verbose) Debug.Log($"{LogTag} 战利品箱已处理过，跳过");
                    return;
                }

                ProcessedLootBoxes.Add(hash);

                if (Verbose) Debug.Log($"{LogTag} 开始处理精英 {character.name} 的掉落（{marker.Affixes.Count} 个词缀）");
                AddEliteLootToLootbox(__result, marker.Affixes, character.characterPreset);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Postfix error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 从 Item 获取对应的 CharacterMainControl
        /// </summary>
        private static CharacterMainControl GetCharacterFromItem(Item item)
        {
            if (item == null) return null;

            MethodInfo method = item.GetType().GetMethod("GetCharacterItem");
            if (method != null)
            {
                Item characterItem = method.Invoke(item, null) as Item;
                if (characterItem != null)
                {
                    // 从 CharacterItem 获取 CharacterMainControl 组件
                    CharacterMainControl character = characterItem.GetComponent<CharacterMainControl>();
                    if (character != null)
                    {
                        if (Verbose) Debug.Log($"{LogTag} 通过 GetCharacterItem 找到角色：{character.name}");
                        return character;
                    }
                }
            }

            Transform current = item.transform;
            int depth = 0;
            while (current != null && depth < 10)
            {
                CharacterMainControl character = current.GetComponent<CharacterMainControl>();
                if (character != null)
                {
                    if (Verbose) Debug.Log($"{LogTag} 通过父对象查找找到角色：{character.name}");
                    return character;
                }

                current = current.parent;
                depth++;
            }

            Debug.LogWarning($"{LogTag} 无法找到对应的 CharacterMainControl");

            return null;
        }

        /// <summary>
        /// 为战利品箱添加精英掉落
        /// </summary>
        private static void AddEliteLootToLootbox(
            InteractableLootbox lootbox,
            List<string> affixes,
            CharacterRandomPreset characterPreset)
        {
            var inventory = lootbox.Inventory;
            string characterName = characterPreset.nameKey;
            if (inventory == null)
            {
                Debug.LogError($"{LogTag} Inventory is null for {characterName}");
                return;
            }

            // 获取敌人惩罚系数
            float enemyDropPenalty = 1.0f;
            int enemyQualityDowngrade = 0;
            if (WeakEnemyPenalties.TryGetValue(characterName, out var penalty))
            {
                enemyDropPenalty = penalty.dropRatePenalty;
                enemyQualityDowngrade = penalty.qualityDowngrade;
                Debug.Log($"{LogTag} {characterName} 应用掉落惩罚: 掉率x{enemyDropPenalty}, 品质-{enemyQualityDowngrade}");
            }

            // 获取掉落组
            var lootGroups = EliteAffixes.GetLootGroupsForAffixes(affixes);

            int randomLootCount = 0;
            foreach (var affixName in affixes)
            {
                if (EliteAffixes.TryGetAffix(affixName, out var affixData))
                {
                    if (affixData.RandomLootConfigs != null)
                    {
                        foreach (var config in affixData.RandomLootConfigs)
                        {
                            randomLootCount += config.ItemCount;
                        }
                    }
                }
            }

            // 扩展容量（手动+随机+稀有度奖励）
            int totalCapacityNeeded = lootGroups.Count + randomLootCount + 1; // +1为稀有度奖励物品
            ExpandInventoryCapacity(inventory, totalCapacityNeeded);

            int addedCount = 0;
            int attemptCount = 0;

            foreach (var group in lootGroups)
            {
                if (group == null || group.Count == 0) continue;

                var pick = group[UnityEngine.Random.Range(0, group.Count)];
                if (pick == null) continue;

                attemptCount++;

                // 全局掉率倍率
                float finalChance = pick.DropChance * GlobalDropRate * enemyDropPenalty;
                if (finalChance > 1f) finalChance = 1f;
                if (finalChance < 0f) finalChance = 0f;
                // 概率检查
                if (UnityEngine.Random.value > finalChance)
                {
                    if (Verbose)
                    {
                        Debug.Log(
                            $"{LogTag} 物品 {pick.ItemID} 未通过概率检查 ({pick.DropChance} x {GlobalDropRate} x {enemyDropPenalty} = {finalChance})");
                    }

                    continue;
                }

                // 随机数量
                int count = UnityEngine.Random.Range(pick.MinCount, pick.MaxCount + 1);
                AddItemToInventory(lootbox, pick.ItemID, count);
                addedCount++;
            }

            // 基于品阶和标签的掉落
            AddRandomLootToLootbox(lootbox, affixes, characterName, enemyDropPenalty, enemyQualityDowngrade);
            // 额外稀有度奖励掉落
            if (EliteEnemyCore.Config.EnableBonusLoot)
            {
                AddRarityBonusLoot(lootbox, affixes, characterPreset);
            }


            if (addedCount > 0)
            {
                Debug.Log($"{LogTag} ✓ {characterName} 成功添加 {addedCount}/{attemptCount} 个总体精英掉落");
            }
            else if (Verbose || attemptCount > 0)
            {
                Debug.LogWarning($"{LogTag} ⚠ {characterName} 尝试添加 {attemptCount} 个掉落，但都未通过概率检查或创建失败");
            }
        }

        /// <summary>
        /// 根据敌人词缀稀有度添加奖励物品
        /// </summary>
        private static void AddRarityBonusLoot(
            InteractableLootbox lootbox,
            List<string> affixes,
            CharacterRandomPreset characterPreset)
        {
            if (affixes == null || affixes.Count == 0) return;

            var helper = GetLootItemHelper();
            if (helper == null) return;

            string characterName = characterPreset.DisplayName;
            // 判断是否为BOSS
            bool isBoss = EliteEnemyCore.BossPresets.Contains(characterPreset.nameKey);

            int affixCount = affixes.Count;
            int minQuality, maxQuality;
            float dropChance;

            if (isBoss)
            {
                // BOSS必定掉落品阶4-7
                minQuality = 3;
                maxQuality = 7;
                dropChance = 0.6f;
            }
            else
            {
                // 根据词缀数量决定品阶范围和概率
                switch (affixCount)
                {
                    case 0:
                        return; // 无词缀不掉落
                    case 1:
                        minQuality = 2;
                        maxQuality = 4;
                        dropChance = 0.3f;
                        break;
                    case 2:
                        minQuality = 2;
                        maxQuality = 5;
                        dropChance = 0.3f;
                        break;
                    default: // 3+
                        minQuality = 2;
                        maxQuality = 6;
                        dropChance = 0.4f;
                        break;
                }
            }

            // 全局掉率
            float finalChance = dropChance * GlobalDropRate;
            if (finalChance > 1f) finalChance = 1f;
            if (finalChance < 0f) finalChance = 0f;

            if (UnityEngine.Random.value > finalChance)
            {
                return;
            }

            Item item = helper.CreateItemWithTagsWeighted(minQuality, maxQuality, null);
            if (item != null)
            {
                item.Detach();
                lootbox.Inventory.AddAndMerge(item, 0);

                string bossTag = isBoss ? "[BOSS奖励]" : "[词缀奖励]";
                Debug.Log(
                    $"{LogTag} ✓ {characterName} {bossTag} 添加稀有度奖励: [品阶{item.Quality}] {item.DisplayName} (词缀数:{affixCount})");
            }
            else
            {
                if (Verbose)
                {
                    Debug.LogWarning($"{LogTag} 稀有度奖励物品创建失败");
                }
            }
        }

        /// <summary>
        /// 清空 Inventory 的所有内容
        /// </summary>
        private static void ClearInventory(Inventory inventory)
        {
            try
            {
                inventory.DestroyAllContent();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} ClearInventory failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 扩展 Inventory 容量
        /// </summary>
        private static void ExpandInventoryCapacity(Inventory inventory, int additional)
        {
            var currentCapacity = inventory.Capacity;
            var newCapacity = inventory.Capacity + additional;
            inventory.SetCapacity(newCapacity);
            if (Verbose) Debug.Log($"{LogTag} 扩展容量：{currentCapacity} → {newCapacity}");
        }

        /// <summary>
        /// 添加物品到 Inventory
        /// </summary>
        private static void AddItemToInventory(InteractableLootbox lootbox, int itemId, int count)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var item = ItemAssetsCollection.InstantiateSync(itemId);
                    if (item == null)
                    {
                        Debug.LogError($"{LogTag} 物品创建失败：ID={item.TypeID} {item.DisplayName}。请检查资源或物品表。");
                        continue;
                    }

                    item.Initialize();
                    item.Detach();
                    lootbox.Inventory.AddAndMerge(item, 0);
                    if (Verbose) Debug.Log($"{LogTag} 添加了1个 {item.DisplayName} 总计需要 {count}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 向战利品箱添加物品时发生异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 设置详细日志
        /// </summary>
        public static void SetVerbose(bool enabled)
        {
            Verbose = enabled;
            Debug.Log($"{LogTag} 详细日志已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            int count = ProcessedLootBoxes.Count;
            ProcessedLootBoxes.Clear();
            if (Verbose) Debug.Log($"{LogTag} 已清理 {count} 个缓存记录");
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static int GetProcessedCount()
        {
            return ProcessedLootBoxes.Count;
        }

        /// <summary>
        /// 获取 LootItemHelper 实例
        /// </summary>
        private static LootItemHelper GetLootItemHelper()
        {
            if (_lootHelper == null)
            {
                _lootHelper = UnityEngine.Object.FindObjectOfType<LootItemHelper>();
                if (_lootHelper == null)
                {
                    Debug.LogWarning($"{LogTag} LootItemHelper 未找到！请确保场景中存在 LootItemHelper 组件。");
                }
            }

            return _lootHelper;
        }

        /// <summary>
        /// 为战利品箱添加随机掉落物品（基于品阶和标签）
        /// </summary>
        private static void AddRandomLootToLootbox(
            InteractableLootbox lootbox,
            List<string> affixes,
            string characterName,
            float enemyDropPenalty = 1.0f,
            int enemyQualityDowngrade = 0)
        {
            var helper = GetLootItemHelper();
            if (helper == null) return;

            var allConfigs = new List<EliteAffixes.RandomLootConfig>();
            foreach (var affixName in affixes)
            {
                if (EliteAffixes.TryGetAffix(affixName, out var affixData))
                {
                    if (affixData.RandomLootConfigs != null && affixData.RandomLootConfigs.Count > 0)
                    {
                        allConfigs.AddRange(affixData.RandomLootConfigs);
                    }
                }
            }

            if (allConfigs.Count == 0) return;

            int addedCount = 0;
            int attemptCount = 0;

            foreach (var config in allConfigs)
            {
                if (config == null) continue;
                attemptCount++;

                float finalChance = config.DropChance * GlobalDropRate * enemyDropPenalty;
                if (finalChance > 1f) finalChance = 1f;
                if (finalChance < 0f) finalChance = 0f;

                if (UnityEngine.Random.value > finalChance)
                {
                    if (Verbose) Debug.Log($"{LogTag} 随机掉落配置未通过概率检查");
                    continue;
                }

                // 转换标签
                Tag[] tags = null;
                if (config.TagNames != null && config.TagNames.Length > 0)
                {
                    List<Tag> tagList = new List<Tag>();
                    foreach (string tagName in config.TagNames)
                    {
                        if (!string.IsNullOrEmpty(tagName))
                        {
                            Tag tag = TagUtilities.TagFromString(tagName);
                            if (tag != null) tagList.Add(tag);
                        }
                    }

                    if (tagList.Count > 0) tags = tagList.ToArray();
                }

                // 判断是品阶范围还是固定品阶
                bool isQualityRange = (config.MinCount >= 1 && config.MaxCount >= 1 &&
                                       config.MinCount <= 7 && config.MaxCount <= 7 &&
                                       config.MinCount <= config.MaxCount);

                for (int i = 0; i < config.ItemCount; i++)
                {
                    Item item = null;

                    // 修改：应用品质降级
                    int adjustedQuality = config.Quality - enemyQualityDowngrade;
                    int adjustedMinQuality = config.MinCount - enemyQualityDowngrade;
                    int adjustedMaxQuality = config.MaxCount - enemyQualityDowngrade;

                    // 确保品质不低于1
                    if (adjustedQuality < 1) adjustedQuality = 1;
                    if (adjustedMinQuality < 1) adjustedMinQuality = 1;
                    if (adjustedMaxQuality < 1) adjustedMaxQuality = 1;

                    if (config.Quality == -1)
                    {
                        // 品阶为 -1：从所有品阶中按标签筛选
                        if (tags != null && tags.Length > 0)
                        {
                            item = helper.CreateItemWithTagsWeighted(-1, -1, tags);
                        }
                        else
                        {
                            Debug.LogWarning($"{LogTag} 品阶为-1但未指定标签，跳过该配置");
                            continue;
                        }
                    }
                    else if (config.MinCount > 1 && config.MaxCount > 1 &&
                             config.MinCount <= 7 && config.MaxCount <= 7)
                    {
                        item = helper.CreateItemWithTagsWeighted(adjustedMinQuality, adjustedMaxQuality, tags);
                    }
                    else
                    {
                        // 普通情况：单一品阶
                        item = helper.CreateItemWithTagsWeighted(adjustedQuality, adjustedQuality, tags);
                    }

                    if (item != null)
                    {
                        // 数量：品阶范围时固定为1，固定品阶时使用配置的数量
                        int count = isQualityRange ? 1 : UnityEngine.Random.Range(config.MinCount, config.MaxCount + 1);

                        for (int j = 0; j < count; j++)
                        {
                            Item itemInstance;
                            if (j == 0)
                            {
                                itemInstance = item;
                            }
                            else
                            {
                                itemInstance = ItemAssetsCollection.InstantiateSync(item.TypeID);
                                if (itemInstance == null) continue;
                                itemInstance.Initialize();
                            }

                            itemInstance.Detach();
                            lootbox.Inventory.AddAndMerge(itemInstance, 0);
                        }

                        addedCount++;

                        if (Verbose)
                        {
                            Debug.Log($"{LogTag} ✓ 添加随机物品: [品阶{item.Quality}] {item.DisplayName} x{count}");
                        }

                        if (count == 0) UnityEngine.Object.Destroy(item.gameObject);
                    }
                }
            }

            if (addedCount > 0)
            {
                Debug.Log($"{LogTag} ✓ {characterName} 成功添加 {addedCount}/{attemptCount} 个基于词条的随机掉落");
            }
        }
    }
}