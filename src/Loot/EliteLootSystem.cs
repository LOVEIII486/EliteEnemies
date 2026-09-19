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

                // 0. **本机必须是精英逻辑的权威**（联机客户端不是）。
                //
                // 第 1 期起客户端会给复制体挂 EliteMarker（那是**显示**用的），
                // 于是这道校验会通过——若不加这层闸门，客户端就会把自己那口箱子里
                // 也注入一份精英掉落，与主机同步下来的箱子**重复**。
                //
                // 掉落由主机权威生成、经联机模组同步，客户端只该展示。
                // 判据是一个静态 bool，单机与主机下恒为 true ⇒ 此处零行为变化。
                if (!EliteEnemyCore.IsEliteAuthority) return;

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

            // （这里原先有一次「预先扩容」：按 `2 + 2×词条数` 估一个值把箱子撑大。
            //   已删除——理由见 AddToLootbox 的注释：那个估法与词条表里的实际掉落配置无关，
            //   而箱子的基数**并不固定**。现在容量跟着实际件数长。）

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

            // 收尾：留一个空格。"满格"因此成为**异常信号**——正常状态下不该出现。
            EnsureOneSpareSlot(lootbox.Inventory);
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
            AddToLootbox(lootbox, item, sourceKey);

            // 如果数量 > 1，复制剩余的
            for (int i = 1; i < count; i++)
            {
                var clone = ItemAssetsCollection.InstantiateSync(item.TypeID);
                if (clone != null)
                {
                    clone.Initialize();
                    clone.FromInfoKey = sourceKey;
                    clone.Detach();
                    AddToLootbox(lootbox, clone, sourceKey);
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

        /// <summary>
        /// 往战利品箱里放一件东西，**放不下才按需扩容再试一次**。
        ///
        /// <para><b>它取代的旧做法是"开掉前先估一个数把箱子撑大"</b>
        /// （<c>SetCapacity(Capacity + estimate)</c>，estimate = <c>2 + 2×词条数</c> × 倍率）。
        /// 那个做法有两个问题：</para>
        /// <list type="number">
        /// <item><b>估法与词条表里的实际掉落配置毫无关系</b>，是猜的。猜小了照样吞掉落，
        /// 猜大了就是把箱子撑大——而它本来是为了"确保放得下"才写的。</item>
        /// <item><b>箱子的基数并不固定。</b>游戏的建箱路径是
        /// 「先临时 <c>SetCapacity(512)</c> 把尸体身上的东西全塞进去，再加完**收紧**到
        /// <c>Mathf.Max(8, 最后一件物品的位置 + 1)</c>」（<c>InteractableLootbox.cs:357,411-413</c>）
        /// ⇒ 常见的敌人箱子只有 8~十几格。而本模组的补丁跑在那个收紧**之后**，
        /// 于是旧写法是在"实际需要"之上再加一个猜的数：10 格的箱子会被撑到 16 或 46。</item>
        /// </list>
        ///
        /// <para>改成按需扩之后，容量**只跟着实际件数长**：放得下就一个字节都不动，
        /// 放不下才 +1，而且只加真正需要的那一格。掉落件数随掉率倍率放大时自动跟随，
        /// 不需要任何估算常量跟着改。</para>
        ///
        /// <para>⚠ 这同时补上了一个**静默吞掉落**的洞：<c>AddAndMerge</c> 在没空格时
        /// 直接 <c>return false</c> 什么也不做（<c>ItemUtilities.cs:127-155</c>），
        /// 而旧代码两处调用都没接返回值 ⇒ 那件物品变成谁也不管的孤儿。
        /// 现在扩容后仍失败会**打日志**。</para>
        /// </summary>
        private static void AddToLootbox(InteractableLootbox lootbox, Item item, string sourceKey)
        {
            if (lootbox == null || item == null) return;

            Inventory inventory = lootbox.Inventory;
            if (inventory == null) return;

            if (inventory.AddAndMerge(item, 0)) return;

            inventory.SetCapacity(inventory.Capacity + 1);
            if (inventory.AddAndMerge(item, 0)) return;

            Debug.LogWarning($"{LogTag} 扩容后仍放不下，丢弃一件掉落：{item.DisplayName}（来源 {sourceKey}）");
        }

        /// <summary>
        /// 收尾：确保箱子**比实际物品数多一格**，即永远留一个空格。
        ///
        /// <para><b>为什么留这一格</b>：留了它，"箱子显示满是满的"就**不可能是正常状态**——
        /// 战利品界面显示的是 <c>(件数/容量)</c>（<c>LootView.cs:43,304-312</c>），
        /// 于是玩家（和排查问题的我们）看到 <c>(N/N)</c> 就知道**有东西被吞了**。
        /// 否则箱子刚好装满与"掉了一件没放进去"看起来一模一样，那种失败是静默的。</para>
        ///
        /// <para>⚠ <b>只增不减</b>：基数可能本来就比 <c>用到的格数 + 1</c> 大——
        /// 游戏自己有一个 <c>Mathf.Max(8, …)</c> 的下限（<c>InteractableLootbox.cs:411</c>），
        /// 一个只装了 3 件的箱子基数就是 8。那是游戏自己的版面选择，
        /// 本方法**不去把它收下来**，只在它不够留一格时才往上补。</para>
        /// </summary>
        private static void EnsureOneSpareSlot(Inventory inventory)
        {
            if (inventory == null) return;

            // GetLastItemPosition() 是最后一个非空位的下标；+1 = 实际用到的格数，再 +1 = 留一格。
            // 空箱子返回 -1 ⇒ wanted = 1，恒小于任何已有容量，不会误改。
            int wanted = inventory.GetLastItemPosition() + 2;
            if (inventory.Capacity < wanted) inventory.SetCapacity(wanted);
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