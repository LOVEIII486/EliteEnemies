using System.Collections.Generic;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>单条掉落定义。</summary>
    public class LootEntry
    {
        public int ItemID;
        public int MinCount = 1;
        public int MaxCount = 1;
        public float DropChance = 1.0f;

        public LootEntry(int itemID, int minCount = 1, int maxCount = 1, float dropChance = 1.0f)
        {
            ItemID = itemID;
            MinCount = minCount;
            MaxCount = maxCount;
            DropChance = dropChance;
        }
    }

    /// <summary>随机掉落配置（基于品阶和标签）</summary>
    public class RandomLootConfig
    {
        public int MinQuality = 1;
        public int MaxQuality = 7;
        
        public string[] TagNames = null;
        public int ItemCount = 1;
        
        public int MinStack = 1;
        public int MaxStack = 1;
        
        public float DropChance = 1.0f;

        public RandomLootConfig(
            int minQuality,
            int maxQuality,
            string[] tagNames,
            int itemCount,
            int minStack,
            int maxStack,
            float dropChance)
        {
            MinQuality = minQuality;
            MaxQuality = maxQuality;
            TagNames = tagNames;
            ItemCount = itemCount;
            MinStack = minStack;
            MaxStack = maxStack;
            DropChance = dropChance;
        }
    }

    /// <summary>词条元数据</summary>
    public class AffixData
    {
        /// <summary>
        /// 词条名。**存键，不存译文**——译文随语言切换由 <see cref="LocalizedText"/> 负责，
        /// 读的时候用 <c>Name.Value</c>。见 <c>src\Localization\LocalizedText.cs</c> 的类注释。
        /// </summary>
        public LocalizedText Name;

        /// <summary>词条描述。同 <see cref="Name"/>：存键不存译文，读用 <c>Description.Value</c>。</summary>
        public LocalizedText Description;

        public Color Color => AffixRarityColor.Of(Rarity);
        public string ColorHex => ColorUtility.ToHtmlStringRGB(Color);
        public string ColoredTag => $"<color=#{ColorHex}>[{Name.Value}]</color>";

        public float HealthMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float MoveSpeedMultiplier = 1f;

        public AffixRarity Rarity = AffixRarity.Common;
        public int Weight => (int)Rarity;

        public readonly List<List<LootEntry>> LootGroups = new List<List<LootEntry>>();
        public readonly List<RandomLootConfig> RandomLootConfigs = new List<RandomLootConfig>();
    }

    /// <summary>
    /// <see cref="AffixData"/> 的构建辅助，供词条池的初始化器链式调用。
    /// 放在本文件是因为它们只服务于 AffixData 的构造，不是通用工具。
    /// </summary>
    internal static class AffixDataExtensions
    {
    // —— 添加一个"掉落组"（组内随机 1 个） ——
    internal static AffixData WithLootGroup(this AffixData a, params LootEntry[] entries)
    {
        if (a == null) return null;
        var group = new List<LootEntry>();
        if (entries != null && entries.Length > 0) group.AddRange(entries);
        a.LootGroups.Add(group);
        return a;
    }

    /// <summary>
    /// 添加随机掉落配置，不限制品阶
    /// </summary>
    /// <param name="quality">品阶（1-7）</param>
    /// <param name="itemCount">随机选择多少个不同物品</param>
    /// <param name="dropChance">掉落概率（0-1）</param>
    /// <param name="tagNames">可选：标签过滤，null表示不限制</param>
    /// <param name="minStack">每个物品最小堆叠数量</param>
    /// <param name="maxStack">每个物品最大堆叠数量</param>
    internal static AffixData WithRandomLoot(
        this AffixData a,
        int quality,
        int itemCount,
        float dropChance,
        string[] tagNames = null,
        int minStack = 1,
        int maxStack = 1)
    {
        if (a == null) return null;
        a.RandomLootConfigs.Add(new RandomLootConfig(
            minQuality: quality,
            maxQuality: quality,
            tagNames: tagNames,
            itemCount: itemCount,
            minStack: minStack, 
            maxStack: maxStack,
            dropChance: dropChance));
        return a;
    }
    
    /// <summary>
    /// 添加随机掉落配置，支持品阶范围
    /// </summary>
    internal static AffixData WithRandomLootRange(
        this AffixData a,
        int minQuality, 
        int maxQuality, 
        int itemCount, 
        float dropChance, 
        string[] tagNames = null,
        int minStack = 1,
        int maxStack = 1)
    {
        if (a == null) return null;

        a.RandomLootConfigs.Add(new RandomLootConfig(
            minQuality: minQuality, 
            maxQuality: maxQuality, 
            tagNames: tagNames,
            itemCount: itemCount,
            minStack: minStack,
            maxStack: maxStack,
            dropChance: dropChance
        ));
        return a;
    }
    }
}
