using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Utilities;
using EliteEnemies.DebugTool;
using EliteEnemies.EliteEnemy.AffixBehaviors;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.Localization;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EliteEnemies.EliteEnemy.LootSystem
{
    /// <summary>
    /// 精英敌人掉落系统
    /// </summary>
    [HarmonyPatch(typeof(InteractableLootbox), "CreateFromItem")]
    public static class EliteLootSystem
    {
        private const string LogTag = "[EliteEnemies.EliteLootSystem]";
        private static bool Verbose = false;
        public static float GlobalDropRate = 1.0f;

        private static readonly HashSet<int> ProcessedLootBoxes = new HashSet<int>();
        private static LootItemHelper _lootHelper = null;
        private static MethodInfo _cachedGetCharacterItemMethod;
        private static bool _hasCachedMethod = false;

        // 弱怪惩罚配置
        private static readonly Dictionary<string, (float dropRatePenalty, int qualityDowngrade)> WeakEnemyPenalties =
            new Dictionary<string, (float, int)>
            {
                { "Cname_Scav", (0.7f, 0) },
                { "Cname_ScavRage", (0.75f, 0) }
            };
        
        private static readonly Dictionary<string, int> MapQualityCaps = new Dictionary<string, int>
        {
            { "Level_GroundZero_Main", 5 },
            { "Level_GroundZero_1", 5 },
            { "Level_HiddenWarehouse", 6 },
            { "Level_Farm_Main", 7 },
            { "Level_Farm_01", 7 }
        };
                
        private static readonly string FromInfoKeyFixed = "EliteLoot_Fixed";
        private static readonly string FromInfoKeyRandom = "EliteLoot_Random";
        private static readonly string FromInfoKeyBonus = "EliteLoot_Bonus";
        
        #region Harmony Patch & Entry Point

        [HarmonyPostfix]
        private static void Postfix(InteractableLootbox __result, Item item, Vector3 position)
        {
            try
            {
                if (__result == null || item == null) return;

                // 1. 获取角色并校验精英身份
                CharacterMainControl character = GetCharacterFromItem(item);
                if (character == null) return;

                var marker = character.GetComponent<EliteEnemyCore.EliteMarker>();
                if (marker == null || marker.Affixes == null || marker.Affixes.Count == 0) return;

                // 2. 防止重复处理
                int hash = position.GetHashCode() ^ item.GetHashCode();
                if (ProcessedLootBoxes.Contains(hash)) return;
                ProcessedLootBoxes.Add(hash);

                // 3. 执行掉落逻辑
                ProcessEliteLoot(__result, character, marker.Affixes);
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
            LootAlgorithmVerifier.RecordAttempt();
            
            string charName = character.characterPreset?.nameKey ?? character.name;
            
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

                float finalChance = CalculateFinalChance(pick.DropChance, enemyPenalty);

                if (UnityEngine.Random.value <= finalChance)
                {
                    int count = UnityEngine.Random.Range(pick.MinCount, pick.MaxCount + 1);
                    AddItemToInventory(lootbox, pick.ItemID, count, "词缀固定", finalChance, FromInfoKeyFixed);
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
                    float finalChance = CalculateFinalChance(config.DropChance, enemyPenalty);
                    if (UnityEngine.Random.value > finalChance) continue;

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
                    for (int i = 0; i < config.ItemCount; i++)
                    {
                        Item item = helper.CreateItemWithTagsWeighted(targetMin, targetMax, tags);

                        if (item != null)
                        {
                            int stackCount = UnityEngine.Random.Range(config.MinStack, config.MaxStack + 1);
                            AddItemInstanceToInventory(lootbox, item, stackCount, $"词缀随机({affixName})", finalChance, FromInfoKeyRandom);
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
            bool isBoss = EliteEnemyCore.BossPresets.Contains(charName);
            
            // 计算积分
            float powerScore = isBoss ? 5f : 0f;
            foreach (var name in affixes)
            {
                if (EliteAffixes.TryGetAffix(name, out var data))
                    powerScore += GetRarityScore(data.Rarity);
                else
                    powerScore += 1f;
            }

            // 计算概率
            float baseChance = 0.30f + (powerScore * 0.05f);
            float finalChance = Mathf.Clamp01(baseChance * GlobalDropRate * enemyPenalty);

            if (UnityEngine.Random.value > finalChance)
            {
                if (Verbose) Debug.Log($"{LogTag} [稀有度奖励] 未触发 (分:{powerScore:F1}, 率:{finalChance:P0})");
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
            
            
            // 只生成1个稀有度奖励
            Item item = helper.CreateItemWithTagsWeighted(minQ, maxQ, null);
            if (item != null)
            {
                string sourceLabel = isBoss ? "BOSS奖励" : "稀有度奖励";
                AddItemInstanceToInventory(lootbox, item, 1, sourceLabel, finalChance, FromInfoKeyBonus);
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
        
        public static void RegisterLootSourceLocalizations()
        {
            string textFixed = LocalizationManager.GetText(FromInfoKeyFixed, "--固定掉落--");
            string textRandom = LocalizationManager.GetText(FromInfoKeyRandom, "--随机掉落--");
            string textBonus = LocalizationManager.GetText(FromInfoKeyBonus, "--稀有度奖励--");
            
            SodaCraft.Localizations.LocalizationManager.overrideTexts[FromInfoKeyFixed] = textFixed;
            SodaCraft.Localizations.LocalizationManager.overrideTexts[FromInfoKeyRandom] = textRandom;
            SodaCraft.Localizations.LocalizationManager.overrideTexts[FromInfoKeyBonus] = textBonus;
            
            if (Verbose) Debug.Log($"{LogTag} 已注册掉落来源文本: {textFixed}, {textRandom}, {textBonus}");
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
            
            LootAlgorithmVerifier.RecordDrop(sourcePool, item.Quality, count);
            
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

        private static float CalculateFinalChance(float baseChance, float penalty)
        {
            return Mathf.Clamp01(baseChance * GlobalDropRate * penalty);
        }

        private static void GetEnemyPenalty(string charName, out float dropPenalty, out int qualityDowngrade)
        {
            if (WeakEnemyPenalties.TryGetValue(charName, out var p))
            {
                dropPenalty = p.dropRatePenalty;
                qualityDowngrade = p.qualityDowngrade;
            }
            else
            {
                dropPenalty = 1.0f;
                qualityDowngrade = 0;
            }
        }

        private static void PreExpandInventory(Inventory inventory, List<string> affixes)
        {
            // 估算需要的格子数，避免扩容多次
            int estimate = 2; // 基础余量 + 奖励
            foreach(var aff in affixes) estimate += 2; // 假设每个词缀最多贡献2组
            
            int newCap = inventory.Capacity + estimate;
            inventory.SetCapacity(newCap);
        }

        private static CharacterMainControl GetCharacterFromItem(Item item)
        {
            if (item == null) return null;

            if (!_hasCachedMethod)
            {
                _cachedGetCharacterItemMethod = item.GetType().GetMethod("GetCharacterItem");
                _hasCachedMethod = true;
            }

            if (_cachedGetCharacterItemMethod != null)
            {
                try 
                {
                    Item characterItem = _cachedGetCharacterItemMethod.Invoke(item, null) as Item;
                    if (characterItem != null)
                    {
                        CharacterMainControl character = characterItem.GetComponent<CharacterMainControl>();
                        if (character != null) return character;
                    }
                }
                catch (Exception) 
                { 
                }
            }

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
            if (_lootHelper == null) _lootHelper = UnityEngine.Object.FindObjectOfType<LootItemHelper>();
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