using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies
{
    public enum AffixRarity
    {
        Common = 100, // 普通 - 权重 100 - 基础词缀
        Uncommon = 60, // 高级 - 权重 50 - 略强词缀
        Rare = 40, // 稀有 - 权重 20 - 强力词缀
        Epic = 20, // 史诗 - 权重 10 - 极强词缀
        Legendary = 10 // 传说 - 权重 5 - 顶级词缀
    }

    /// <summary>集中管理所有可用词条。</summary>
    public static class EliteAffixes
    {
        /// <summary>
        /// 互斥词缀字典：Key = 词缀名，Value = 与该词缀互斥的词缀集合
        /// 注意：互斥关系是双向的，但只需要在其中一个方向定义即可
        /// </summary>
        public static readonly Dictionary<string, HashSet<string>> MutuallyExclusiveAffixes =
            new Dictionary<string, HashSet<string>>
            {
                ["Giant"] = new HashSet<string> { "Mini" },
                ["Undead"] = new HashSet<string> { "Explosive" },
                // ["Regeneration"] = new HashSet<string> { "Tanky", "Giant" },
                ["Split"] = new HashSet<string> { "MimicTear", "ChickenBro" , "MandarinDuck"},
                ["Sticky"] = new HashSet<string> { "MagazineCurse" },
                ["Knockback"] = new HashSet<string> { "Phase" },
            };

        /// <summary>
        /// 检查两个词缀是否互斥
        /// </summary>
        public static bool AreAffixesMutuallyExclusive(string affix1, string affix2)
        {
            if (string.IsNullOrEmpty(affix1) || string.IsNullOrEmpty(affix2))
                return false;

            // 检查双向互斥关系
            if (MutuallyExclusiveAffixes.TryGetValue(affix1, out var exclusions1))
            {
                if (exclusions1.Contains(affix2))
                    return true;
            }

            if (MutuallyExclusiveAffixes.TryGetValue(affix2, out var exclusions2))
            {
                if (exclusions2.Contains(affix1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查某个词缀是否与已选词缀列表中的任何一个互斥
        /// </summary>
        public static bool IsAffixConflictingWithList(string affix, IEnumerable<string> selectedAffixes)
        {
            if (string.IsNullOrEmpty(affix) || selectedAffixes == null)
                return false;

            foreach (var selected in selectedAffixes)
            {
                if (AreAffixesMutuallyExclusive(affix, selected))
                    return true;
            }

            return false;
        }

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
            public int Quality = 7;
            public string[] TagNames = null;
            public int ItemCount = 1;
            public int MinCount = 1;
            public int MaxCount = 1;
            public float DropChance = 1.0f;

            public RandomLootConfig(
                int quality = 7,
                string[] tagNames = null,
                int itemCount = 1,
                int minCount = 1,
                int maxCount = 1,
                float dropChance = 1.0f)
            {
                Quality = quality;
                TagNames = tagNames;
                ItemCount = itemCount;
                MinCount = minCount;
                MaxCount = maxCount;
                DropChance = dropChance;
            }
        }

        /// <summary>词条元数据</summary>
        public class AffixData
        {
            public string Name;
            public string Description;
            public Color Color = Color.white;
            public string ColorHex => ColorUtility.ToHtmlStringRGB(Color);
            public string ColoredTag => $"<color=#{ColorHex}>[{Name}]</color>";

            public float HealthMultiplier = 1f;
            public float DamageMultiplier = 1f;
            public float MoveSpeedMultiplier = 1f;

            public AffixRarity Rarity = AffixRarity.Common;
            public int Weight => (int)Rarity;

            public readonly List<List<LootEntry>> LootGroups = new List<List<LootEntry>>();
            public readonly List<RandomLootConfig> RandomLootConfigs = new List<RandomLootConfig>();
        }

        /// <summary>词条池</summary>
        public static readonly Dictionary<string, AffixData> Pool = new Dictionary<string, AffixData>
        {
            ["Tanky"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Tanky_Name", "肉盾"),
                Description = LocalizationManager.GetText("Affix_Tanky_Description", "更耐打但行动迟缓。掉落：重型防弹衣、医疗箱"),
                Color = new Color(0.902f, 0.494f, 0.133f),
                HealthMultiplier = 1.8f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 0.7f,
                Rarity = AffixRarity.Common
            }.WithLootGroup(
                new LootEntry(1139, 1, 1, 0.4f), // 3级重型防弹衣
                new LootEntry(1138, 1, 1, 0.2f), // 4级重型防弹衣
                new LootEntry(1137, 1, 1, 0.07f) // 5级重型防弹衣
            ),
            ["Swift"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Swift_Name", "迅捷"),
                Description = LocalizationManager.GetText("Affix_Swift_Description", "移动迅速但较为脆弱。掉落：轻盈图腾、提速针剂"),
                Color = new Color(0.0f, 0.784f, 1.0f),
                HealthMultiplier = 0.8f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.5f,
                Rarity = AffixRarity.Common
            }.WithLootGroup(
                new LootEntry(993, 1, 1, 0.3f), // 轻盈图腾 1
                new LootEntry(324, 1, 1, 0.1f), // 轻盈图腾 2
                new LootEntry(992, 1, 1, 0.05f) // 轻盈图腾 3
            ).WithLootGroup(
                new LootEntry(137, 1, 2, 0.7f), // 黄针 1-3
                new LootEntry(398, 1, 2, 0.5f) // 负重针剂 1-2
            ),
            ["Berserk"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Berserk_Name", "狂暴"),
                Description = LocalizationManager.GetText("Affix_Berserk_Description"),
                Color = new Color(1.0f, 0.298f, 0.298f),
                HealthMultiplier = 0.9f,
                DamageMultiplier = 1.2f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Common
            }.WithLootGroup(
                new LootEntry(321, 1, 1, 0.3f), // 进击图腾 1
                new LootEntry(320, 1, 1, 0.1f), // 进击图腾 2
                new LootEntry(957, 1, 1, 0.05f) // 进击图腾 3
            ).WithLootGroup(
                new LootEntry(438, 1, 2, 0.6f) // 热血针剂
            ),
            ["GlassCannon"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_GlassCannon_Name", "玻璃大炮"),
                Description = LocalizationManager.GetText("Affix_GlassCannon_Description", "伤害极高但极其脆弱。掉落：特种穿甲弹"),
                Color = new Color(1.0f, 0.4f, 0.8f),
                HealthMultiplier = 0.6f,
                DamageMultiplier = 1.7f,
                MoveSpeedMultiplier = 1.2f,
                Rarity = AffixRarity.Common
            }.WithRandomLootRange(4, 5, 1, 0.7f, new[] { "Bullet" }),
            ["Fisherman"] = new AffixData
                {
                    Name = LocalizationManager.GetText("Affix_Fisherman_Name", "钓鱼佬"),
                    Description = LocalizationManager.GetText("Affix_Fisherman_Description", "掉落各种鱼类、鱼饵与鱼竿"),
                    Color = new Color(0.20f, 0.80f, 0.80f),
                    HealthMultiplier = 1.0f,
                    DamageMultiplier = 1.0f,
                    MoveSpeedMultiplier = 1.0f,
                    Rarity = AffixRarity.Uncommon
                }
                .WithLootGroup(
                    new LootEntry(1106, 1, 1, 1f), // 棕沙丁鱼
                    new LootEntry(1123, 1, 1, 1f), // 大头金鱼
                    new LootEntry(1119, 1, 1, 1f), // 红金鱼
                    new LootEntry(1114, 1, 1, 1f), // 粉金鱼
                    new LootEntry(1115, 1, 1, 1f), // 紫雀鲷鱼
                    new LootEntry(1124, 1, 1, 1f), // 白扁鱼
                    new LootEntry(1118, 1, 1, 1f), // 红九间鱼
                    new LootEntry(1109, 1, 1, 1f), // 绿背鳙鱼
                    new LootEntry(1104, 1, 1, 1f), // 棕白石鲈
                    new LootEntry(1099, 1, 1, 1f) // 青南乳鱼
                )
                .WithLootGroup(
                    new LootEntry(1154, 1, 3, 1f), // 鱼饵
                    new LootEntry(1095, 1, 1, 0.1f), // 好钓竿
                    new LootEntry(1096, 1, 1, 0.05f) // 厉害钓竿
                ),
            ["Chef"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Chef_Name", "厨子"),
                Description = LocalizationManager.GetText("Affix_Chef_Description", "喜欢美食与饮品，掉落罐头、水、甜点等补给物资"),
                Color = new Color(0.95f, 0.66f, 0.20f),
                HealthMultiplier = 1.2f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            }.WithRandomLoot(-1, 1, 1, new[] { "Food" }),
            ["Musician"] = new AffixData
                {
                    Name = LocalizationManager.GetText("Affix_Musician_Name", "音乐家"),
                    Description = LocalizationManager.GetText("Affix_Musician_Description", "带着乐器上战场的奇葩，掉落乐器"),
                    Color = new Color(0.65f, 0.55f, 1.0f),
                    HealthMultiplier = 1.0f,
                    DamageMultiplier = 1.0f,
                    MoveSpeedMultiplier = 1.0f,
                    Rarity = AffixRarity.Rare
                }
                .WithLootGroup(
                    new LootEntry(112, 1, 1, 1f), // 麦克风
                    new LootEntry(124, 1, 1, 1f), // 手鼓
                    new LootEntry(125, 1, 1, 0.6f), // 小号
                    new LootEntry(126, 1, 1, 0.8f), // 木琴
                    new LootEntry(1259, 1, 1, 0.6f) // 卡祖笛
                ),
            ["NineDragons"] = new AffixData
                {
                    Name = LocalizationManager.GetText("Affix_NineDragons_Name", "九龙拉棺"),
                    Description = LocalizationManager.GetText("Affix_NineDragons_Description", "全属性提升 1.3 倍，掉落各种针剂"),
                    Color = new Color(0.60f, 0.20f, 0.80f),
                    HealthMultiplier = 1.3f,
                    DamageMultiplier = 1.3f,
                    MoveSpeedMultiplier = 1.3f,
                    Rarity = AffixRarity.Rare
                }
                .WithRandomLoot(-1, 1, 1, new[] { "Injector" }),
            ["Talkative"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Talkative_Name", "话痨"),
                Description = LocalizationManager.GetText("Affix_Talkative_Description", "战斗中会不断发表随机台词或嘲讽"),
                Color = new Color(1.0f, 0.412f, 0.706f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            }.WithRandomLootRange(1, 4, 1, 1, new[] { "Daily" }),
            ["Regeneration"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Regeneration_Name", "再生"),
                Description =
                    LocalizationManager.GetText("Affix_Regeneration_Description", "持续回复生命，每秒回复约 10 点最大生命值"),
                Color = new Color(0.2f, 1.0f, 0.3f),
                HealthMultiplier = 1.5f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon,
            }.WithRandomLoot(-1, 1, 1f, new[] { "Medic" }),
            ["Invisible"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Invisible_Name", "隐身"),
                Description = LocalizationManager.GetText("Affix_Invisible_Description", "获得隐身状态，每隔数秒会短暂显形并闪烁几下"),
                Color = new Color(0.65f, 0.65f, 1.0f),
                HealthMultiplier = 0.7f,
                DamageMultiplier = 1.2f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            }.WithLootGroup(
                new LootEntry(660, 1, 1, 1f) // 烟雾弹
            ).WithLootGroup(
                new LootEntry(741, 1, 1, 1f), // 黑色眼镜
                new LootEntry(742, 1, 1, 1f), // 闪光眼镜
                new LootEntry(973, 1, 1, 0.1f), //蝇蝇眼镜
                new LootEntry(718, 1, 1, 0.07f), // 夜视仪
                new LootEntry(719, 1, 1, 0.07f), // 热成像
                new LootEntry(718, 1, 1, 0.1f) // 蒙眼布
            ),
            ["Giant"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Giant_Name", "巨大化"),
                Description = LocalizationManager.GetText("Affix_Giant_Description", "敌人变得更大，生命值提升但移动速度降低"),
                Color = new Color(0.8f, 0.3f, 1.0f),
                // 血量和速度倍率由行为类动态计算，这里设置为默认值
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Rare
            }.WithLootGroup(
                new LootEntry(995, 1, 1, 0.4f), // 健壮图腾1
                new LootEntry(994, 1, 1, 0.2f), // 健壮图腾2
                new LootEntry(325, 1, 1, 0.1f) // 健壮图腾3
            ).WithRandomLootRange(1,5,1,0.6f,new[] { "Backpack" }),
            ["Mini"] = new AffixData
                {
                    Name = LocalizationManager.GetText("Affix_Mini_Name", "迷你"),
                    Description = LocalizationManager.GetText("Affix_Mini_Description", "敌人变得更小，生命略低但移速略微提升"),
                    Color = new Color(0.4f, 0.8f, 1.0f),
                    HealthMultiplier = 1.0f,
                    DamageMultiplier = 1.0f,
                    MoveSpeedMultiplier = 1.0f,
                    Rarity = AffixRarity.Rare
                }
                .WithLootGroup(
                    new LootEntry(444, 1, 2, 1f), // 红包
                    new LootEntry(446, 1, 1, 0.1f), // 铜钱串
                    new LootEntry(447, 1, 1, 0.8f), // 中国结
                    new LootEntry(448, 1, 1, 0.5f) // 红灯笼
                ),
            ["Undead"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Undead_Name", "不死"),
                Description = LocalizationManager.GetText("Affix_Undead_Description", "残血时短暂无敌 2 s，并恢复至 50% 生命"),
                Color = new Color(1f, 0.85f, 0.2f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Rare
            }.WithLootGroup(
                new LootEntry(963, 1, 1, 0.6f), // 生命图腾1
                new LootEntry(961, 1, 1, 0.4f), // 生命图腾2
                new LootEntry(962, 1, 1, 0.2f) // 生命图腾3
            ),
            ["MimicTear"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_MimicTear_Name", "仿身泪滴"),
                Description = LocalizationManager.GetText("Affix_MimicTear_Description", "复制玩家主手武器与装备（不会掉落）"),
                Color = new Color(0.2f, 0.7f, 1.0f),
                HealthMultiplier = 1.5f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary,
            },
            ["Split"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Split_Name", "分裂"),
                Description = LocalizationManager.GetText("Affix_Split_Description", "敌人残血时召唤数个更弱的分身"),
                Color = new Color(0.60f, 0.90f, 1.00f),
                HealthMultiplier = 1.5f,
                DamageMultiplier = 0.8f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic
            }.WithRandomLoot(-1, 1, 1f, new[] { "Totem" }),
            ["Explosive"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Explosive_Name", "自爆"),
                Description = LocalizationManager.GetText("Affix_Explosive_Description", "死亡后引发小范围爆炸，造成约 20 点伤害"),
                Color = new Color(1.0f, 0.2f, 0.3f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.2f,
                Rarity = AffixRarity.Epic
            }.WithRandomLoot(-1, 1, 1, new[] { "Explosive" }),
            ["Sticky"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Sticky_Name", "粘性"),
                Description = LocalizationManager.GetText("Affix_Sticky_Description", "首次受击会使玩家掉落当前装备的武器，击杀掉落胶带与万能胶"),
                Color = new Color(0.196f, 0.804f, 0.196f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            }.WithLootGroup(
                new LootEntry(765, 1, 1, 1f), // 胶带
                new LootEntry(833, 1, 1, 1f), // 万能胶A 
                new LootEntry(834, 1, 1, 1f) // 万能胶B
            ),
            ["TimeStop"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_TimeStop_Name", "时停"),
                Description = LocalizationManager.GetText("Affix_TimeStop_Description", "受伤时会短暂停止时间约 3 秒"),
                Color = new Color(1.0f, 0.42f, 0.21f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic
            }.WithLootGroup(
                new LootEntry(51, 1, 1, 1f), // 钟
                new LootEntry(83, 1, 1, 1f), // 紫色怀表
                new LootEntry(385, 1, 1, 1f) // 怀表
            ),
            ["MagazineCurse"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_MagazineCurse_Name", "弹匣诅咒"),
                Description = LocalizationManager.GetText("Affix_MagazineCurse_Description", "受伤时强制玩家换弹，掉落各种快速弹匣组件"),
                Color = new Color(0.608f, 0.349f, 0.714f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic
            }.WithLootGroup(
                new LootEntry(531, 1, 1, 0.6f), // 手枪快速弹匣1
                new LootEntry(532, 1, 1, 0.3f), // 手枪快速弹匣2
                new LootEntry(543, 1, 1, 0.6f), // 步枪快速弹匣1
                new LootEntry(543, 1, 1, 0.3f), // 步枪快速弹匣2
                new LootEntry(553, 1, 1, 0.6f), // 狙击枪快速弹匣1
                new LootEntry(554, 1, 1, 0.3f), // 狙击枪快速弹匣2
                new LootEntry(837, 1, 1, 0.6f), // BR快速弹匣1
                new LootEntry(838, 1, 1, 0.3f) // BR快速弹匣2
            ),
            ["Knockback"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Knockback_Name", "击飞"),
                Description = LocalizationManager.GetText("Affix_Knockback_Description", "攻击产生强力击退"),
                Color = new Color(0.20f, 0.80f, 0.95f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic
            },
            ["Chaos"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Chaos_Name", "混沌"),
                Description = LocalizationManager.GetText("Affix_Chaos_Description", "攻击随机产生异常效果"),
                Color = new Color(0.58f, 0.0f, 0.83f),
                HealthMultiplier = 1.2f,
                DamageMultiplier = 1.1f,
                MoveSpeedMultiplier = 1.1f,
                Rarity = AffixRarity.Epic,
            }.WithLootGroup(
                new LootEntry(408, 1, 2, 0.7f), // 电抗性针
                new LootEntry(1070, 1, 2, 0.7f), // 火抗针
                new LootEntry(1071, 1, 2, 0.7f), // 毒抗针
                new LootEntry(1072, 1, 2, 0.7f) // 空间抗性针
            ),
            ["Vampirism"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Vampirism_Name", "吸血"),
                Description = LocalizationManager.GetText("Affix_Vampirism_Description",
                    "攻击时回复自身生命值"),
                Color = new Color(0.86f, 0.08f, 0.24f),
                HealthMultiplier = 1.4f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Rare,
            }.WithRandomLootRange(1, 4, 1, 1f, new[] { "Medic" }),
            ["Collector"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Collector_Name", "收藏家"),
                Description = LocalizationManager.GetText("Affix_Collector_Description", "掉落各种稀有收藏品"),
                Color = new Color(0.85f, 0.65f, 0.25f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            }.WithRandomLootRange(2, 4, 2, 1f, new[] { "Luxury" }),
            ["Gunsmith"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Gunsmith_Name", "枪匠"),
                Description = LocalizationManager.GetText("Affix_Gunsmith_Description", "掉落高品质枪械配件"),
                Color = new Color(0.4f, 0.5f, 0.6f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.2f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            }.WithRandomLootRange(2, 4, 1, 1f, new[] { "Accessory" }),
            ["Slime"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Slime_Name", "史莱姆"),
                Description = LocalizationManager.GetText("Affix_Slime_Description", "初始巨大但虚弱，随血量降低逐渐缩小并增强伤害，会周期性跳跃"),
                Color = new Color(0.4f, 0.8f, 0.4f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            },
            ["Blindness"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Blindness_Name", "致盲"),
                Description = LocalizationManager.GetText("Affix_Blindness_Description", "攻击使玩家视野受限5秒"),
                Color = new Color(0.2f, 0.2f, 0.2f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            },
            ["Slow"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Slow_Name", "缓速"),
                Description = LocalizationManager.GetText("Affix_Slow_Description"),
                Color = new Color(0.45f, 0.70f, 0.95f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            },
            ["Stun"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Stun_Name", "震慑"),
                Description = LocalizationManager.GetText("Affix_Stun_Description"),
                Color = new Color(0.95f, 0.85f, 0.30f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            },
            ["DungEater"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_DungEater_Name", "食粪者"),
                Description = LocalizationManager.GetText("Affix_DungEater_Description"),
                Color = new Color(0.70f, 0.55f, 0.30f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Rare
            },
            ["Hardening"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Hardening_Name", "硬化"),
                Description = LocalizationManager.GetText("Affix_Hardening_Description", "受伤随机增加护甲值"),
                Color = new Color(0.7f, 0.7f, 0.7f), // 灰色
                HealthMultiplier = 1.4f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            },
            ["ChickenBro"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_ChickenBro_Name"),
                Description = LocalizationManager.GetText("Affix_ChickenBro_Description"),
                Color = new Color(1.0f, 0.84f, 0.0f), // 金黄色
                HealthMultiplier = 1.2f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            }.WithLootGroup(
                new LootEntry(379, 1, 1, 1f), // 背带裤
                new LootEntry(380, 1, 1, 1f), // 篮球
                new LootEntry(395, 1, 1, 1f)  // 黑色针剂
            ),
            ["Phase"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Phase_Name"),
                Description = LocalizationManager.GetText("Affix_Phase_Description"),
                Color = new Color(0.0f, 1.0f, 1.0f), 
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic 
            },
            ["MandarinDuck"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_MandarinDuck_Name"),
                Description = LocalizationManager.GetText("Affix_MandarinDuck_Description"),
                Color = new Color(1.0f, 0.4f, 0.7f),
                HealthMultiplier = 0.8f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Legendary
            }.WithRandomLoot(-1, 1, 1f, new[] { "Accessory" }),
            ["Revenge"] = new AffixData
            {
                Name = LocalizationManager.GetText("Affix_Revenge_Name", "报复"),
                Description = LocalizationManager.GetText("Affix_Revenge_Description", "受伤时发射子弹反击"),
                Color = new Color(0.75f, 0.25f, 0.25f),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Epic
            }.WithLootGroup(
                new LootEntry(326, 1, 1, 0.5f)// 火箭弹
            ).WithRandomLootRange(3, 5, 2, 1f, new[] { "Bullet" }),
        };


        /// <summary>将多个词条的“掉落组”合并 </summary>
        public static List<List<LootEntry>> GetLootGroupsForAffixes(IReadOnlyList<string> affixes)
        {
            var groups = new List<List<LootEntry>>();
            if (affixes == null || affixes.Count == 0) return groups;

            foreach (var name in affixes)
            {
                if (Pool.TryGetValue(name, out var a) && a != null && a.LootGroups.Count > 0)
                {
                    groups.AddRange(a.LootGroups);
                }
            }

            return groups;
        }

        public static bool TryGetAffix(string name, out AffixData data)
            => Pool.TryGetValue(name, out data);

        // —— 添加一个"掉落组"（组内随机 1 个） ——
        private static AffixData WithLootGroup(this AffixData a, params LootEntry[] entries)
        {
            if (a == null) return null;
            var group = new List<LootEntry>();
            if (entries != null && entries.Length > 0) group.AddRange(entries);
            a.LootGroups.Add(group);
            return a;
        }

        /// <summary>
        /// 添加随机掉落配置
        /// </summary>
        /// <param name="quality">品阶（1-7）</param>
        /// <param name="itemCount">随机选择多少个不同物品</param>
        /// <param name="dropChance">掉落概率（0-1）</param>
        /// <param name="tagNames">可选：标签过滤，null表示不限制</param>
        private static AffixData WithRandomLoot(
            this AffixData a,
            int quality,
            int itemCount,
            float dropChance,
            string[] tagNames = null)
        {
            if (a == null) return null;
            a.RandomLootConfigs.Add(new RandomLootConfig(quality, tagNames, itemCount, 1, 1, dropChance));
            return a;
        }

        // 支持品阶范围的随机掉落
        /// <summary>
        /// 添加随机掉落配置（支持品阶范围）
        /// </summary>
        private static AffixData WithRandomLootRange(
            this AffixData a,
            int minQuality, // 最小品阶
            int maxQuality, // 最大品阶
            int itemCount, // 物品数量
            float dropChance, // 掉落概率
            string[] tagNames = null)
        {
            if (a == null) return null;

            a.RandomLootConfigs.Add(new RandomLootConfig(
                quality: maxQuality, // quality 字段存最大品阶
                tagNames: tagNames,
                itemCount: itemCount,
                minCount: minQuality, // minCount 存最小品阶
                maxCount: maxQuality, // maxCount 存最大品阶
                dropChance: dropChance
            ));
            return a;
        }
    }
}