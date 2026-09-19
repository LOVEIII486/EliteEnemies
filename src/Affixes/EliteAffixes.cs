using System.Collections.Generic;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条总表：所有可用词条的数据与查询入口。
    ///
    /// <para>本文件**只有数据与查询**。词条的数据结构见 <c>AffixData.cs</c>，
    /// 稀有度见 <c>AffixRarity.cs</c>，互斥规则见 <c>AffixExclusivity.cs</c>。</para>
    ///
    /// <para>⚠ 本表是 <c>static readonly</c>，但里面**不存译文，只存键**
    /// （<see cref="LocalizedText"/>）——所以它可以随时被读，不存在「首次访问的时机」
    /// 这类约束。曾经有过：那时初始化器里直接调 <c>GetText</c>，取一次就把当时的语言焊死，
    /// 于是读取时机变成必须推迟到本地化就绪之后。改存键之后该约束消失，
    /// 详情见 <c>src/Localization/LocalizedText.cs</c>。</para>
    /// </summary>
    public static class EliteAffixes
    {
    /// <summary>词条池</summary>
    public static readonly Dictionary<string, AffixData> Pool = new Dictionary<string, AffixData>
    {
        ["Tanky"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Tanky_Name", "肉盾"),
            Description = new LocalizedText("EliteEnemies_Affix_Tanky_Description", "更耐打但行动迟缓。掉落：重型防弹衣、弹药"),
            HealthMultiplier = 1.8f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 0.75f,
            Rarity = AffixRarity.Common
        }.WithLootGroup(
            new LootEntry(1139, 1, 1, 0.4f), // 3级重型防弹衣
            new LootEntry(1138, 1, 1, 0.2f), // 4级重型防弹衣
            new LootEntry(1137, 1, 1, 0.1f) // 5级重型防弹衣
        ).WithRandomLootRange(minQuality: 1, maxQuality: 3, itemCount: 1, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Swift"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Swift_Name", "迅捷"),
            Description = new LocalizedText("EliteEnemies_Affix_Swift_Description", "移动迅速但较为脆弱。掉落：轻盈图腾、提速针剂"),
            HealthMultiplier = 0.8f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.5f,
            Rarity = AffixRarity.Common
        }.WithLootGroup(
            new LootEntry(993, 1, 1, 0.3f), // 轻盈图腾 1
            new LootEntry(324, 1, 1, 0.1f), // 轻盈图腾 2
            new LootEntry(992, 1, 1, 0.05f) // 轻盈图腾 3
        ).WithLootGroup(
            new LootEntry(137, 1, 1, 0.5f) // 黄针 1-3
        ),
        ["Berserk"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Berserk_Name", "狂暴"),
            Description = new LocalizedText("EliteEnemies_Affix_Berserk_Description"),
            HealthMultiplier = 0.8f,
            DamageMultiplier = 1.2f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Common
        }.WithLootGroup(
            new LootEntry(321, 1, 1, 0.3f), // 进击图腾 1
            new LootEntry(320, 1, 1, 0.1f), // 进击图腾 2
            new LootEntry(957, 1, 1, 0.05f) // 进击图腾 3
        ).WithLootGroup(
            new LootEntry(438, 1, 1, 0.5f) // 热血针剂
        ),
        ["GlassCannon"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_GlassCannon_Name", "玻璃大炮"),
            Description = new LocalizedText("EliteEnemies_Affix_GlassCannon_Description", "伤害极高但极其脆弱。掉落：特种穿甲弹"),
            HealthMultiplier = 0.5f,
            DamageMultiplier = 1.6f,
            MoveSpeedMultiplier = 1.2f,
            Rarity = AffixRarity.Common
        }.WithRandomLootRange(minQuality: 3, maxQuality: 5, itemCount: 1, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Fisherman"] = new AffixData
            {
                Name = new LocalizedText("EliteEnemies_Affix_Fisherman_Name", "钓鱼佬"),
                Description = new LocalizedText("EliteEnemies_Affix_Fisherman_Description", "掉落各种鱼类、鱼饵与鱼竿"),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Uncommon
            }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1, tagNames: new[] { "Fish" }) // 鱼类
            .WithLootGroup(new LootEntry(1154, 1, 3, 1f), // 鱼饵
                new LootEntry(1095, 1, 1, 0.1f), // 好钓竿
                new LootEntry(1096, 1, 1, 0.05f) // 厉害钓竿
            ),
        ["Chef"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Chef_Name", "厨子"),
            Description = new LocalizedText("EliteEnemies_Affix_Chef_Description", "喜欢美食与饮品，掉落罐头、水、甜点等补给物资"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1, tagNames: new[] { "Food" }),
        ["Musician"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Musician_Name", "音乐家"),
            Description = new LocalizedText("EliteEnemies_Affix_Musician_Description", "带着乐器上战场的奇葩，玩家靠近时随机吹奏曲子，掉落乐器"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithLootGroup(new LootEntry(112, 1, 1, 1f), // 麦克风
            new LootEntry(124, 1, 1, 1f), // 手鼓
            new LootEntry(125, 1, 1, 0.6f), // 小号
            new LootEntry(126, 1, 1, 0.8f), // 木琴
            new LootEntry(1259, 1, 1, 0.6f) // 卡祖笛
        ),
        ["NineDragons"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_NineDragons_Name", "九龙拉棺"),
            Description = new LocalizedText("EliteEnemies_Affix_NineDragons_Description", "全属性提升 1.3 倍，掉落各种针剂"),
            HealthMultiplier = 1.3f,
            DamageMultiplier = 1.3f,
            MoveSpeedMultiplier = 1.3f,
            Rarity = AffixRarity.Rare
       }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1, tagNames: new[] { "Injector" }),
        ["Talkative"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Talkative_Name", "话痨"),
            Description = new LocalizedText("EliteEnemies_Affix_Talkative_Description", "战斗中会不断发表随机台词或嘲讽"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithRandomLootRange(minQuality: 1, maxQuality: 4, itemCount: 1, dropChance: 1, tagNames: new[] { "Daily" }),
        ["Regeneration"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Regeneration_Name"),
            Description =
                new LocalizedText("EliteEnemies_Affix_Regeneration_Description"),
            HealthMultiplier = 1.2f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon,
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "Medic" }),
        ["Invisible"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Invisible_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Invisible_Description"),
            HealthMultiplier = 0.7f,
            DamageMultiplier = 1.1f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
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
            Name = new LocalizedText("EliteEnemies_Affix_Giant_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Giant_Description"),
            // 血量和速度倍率由行为类动态计算，这里设置为默认值
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithLootGroup(
            new LootEntry(995, 1, 1, 0.4f), // 健壮图腾1
            new LootEntry(994, 1, 1, 0.2f), // 健壮图腾2
            new LootEntry(325, 1, 1, 0.1f) // 健壮图腾3
        ).WithRandomLootRange(minQuality: 1, maxQuality: 5, itemCount: 1, dropChance: 0.5f, tagNames: new[] { "Backpack" }),
        ["Mini"] = new AffixData
            {
                Name = new LocalizedText("EliteEnemies_Affix_Mini_Name", "迷你"),
                Description = new LocalizedText("EliteEnemies_Affix_Mini_Description", "敌人变得更小，生命略低但移速略微提升"),
                HealthMultiplier = 1.0f,
                DamageMultiplier = 1.0f,
                MoveSpeedMultiplier = 1.0f,
                Rarity = AffixRarity.Rare
            }
            .WithLootGroup(
                new LootEntry(444, 1, 2, 1f), // 红包
                new LootEntry(446, 1, 1, 0.15f), // 铜钱串
                new LootEntry(447, 1, 1, 1f), // 中国结
                new LootEntry(448, 1, 1, 1f) // 红灯笼
            ),
        ["Undead"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Undead_Name", "不死"),
            Description = new LocalizedText("EliteEnemies_Affix_Undead_Description", "残血时短暂无敌 2.5 秒，并恢复至 50% 生命"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithLootGroup(
            new LootEntry(963, 1, 1, 0.4f), // 生命图腾1
            new LootEntry(961, 1, 1, 0.2f), // 生命图腾2
            new LootEntry(962, 1, 1, 0.1f) // 生命图腾3
        ),
        ["MimicTear"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_MimicTear_Name", "仿身泪滴"),
            Description = new LocalizedText("EliteEnemies_Affix_MimicTear_Description", "复制玩家主手武器与装备（随机掉落其中一件）"),
            HealthMultiplier = 1.2f,
            DamageMultiplier = 0.8f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic,
        }.WithRandomLootRange(minQuality: 3, maxQuality: 6, itemCount: 1, dropChance: 1f, tagNames: new[] { "Luxury" }),
        ["Split"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Split_Name", "分裂"),
            Description = new LocalizedText("EliteEnemies_Affix_Split_Description", "敌人残血时召唤数个更弱的分身"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "Totem" })
        .WithRandomLootRange(minQuality: 3, maxQuality: 5, itemCount: 2, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Explosive"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Explosive_Name", "自爆"),
            Description = new LocalizedText("EliteEnemies_Affix_Explosive_Description", "死亡后引发小范围爆炸，造成约 10-30 点伤害（依据玩家最大生命值）"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.2f,
            Rarity = AffixRarity.Rare
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1, tagNames: new[] { "Explosive" }),
        ["Sticky"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Sticky_Name", "粘性"),
            Description = new LocalizedText("EliteEnemies_Affix_Sticky_Description", "首次受击会使玩家掉落当前装备的武器，击杀掉落胶带与万能胶"),
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
            Name = new LocalizedText("EliteEnemies_Affix_TimeStop_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_TimeStop_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithLootGroup(
            new LootEntry(51, 1, 1, 1f), // 钟
            new LootEntry(83, 1, 1, 1f) // 紫色怀表
            //new LootEntry(385, 1, 1, 1f) // 怀表 任务物品
        ),
        ["MagazineCurse"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_MagazineCurse_Name", "弹匣诅咒"),
            Description = new LocalizedText("EliteEnemies_Affix_MagazineCurse_Description", "受伤时强制玩家换弹，掉落各种快速弹匣组件"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithLootGroup(
            new LootEntry(531, 1, 1, 0.6f), // 手枪快速弹匣1
            new LootEntry(532, 1, 1, 0.5f), // 手枪快速弹匣2
            new LootEntry(543, 1, 1, 0.5f), // 步枪快速弹匣1
            new LootEntry(543, 1, 1, 0.5f), // 步枪快速弹匣2
            new LootEntry(553, 1, 1, 0.6f), // 狙击枪快速弹匣1
            new LootEntry(554, 1, 1, 0.5f), // 狙击枪快速弹匣2
            new LootEntry(837, 1, 1, 0.6f), // BR快速弹匣1
            new LootEntry(838, 1, 1, 0.5f) // BR快速弹匣2
        ),
        ["Knockback"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Knockback_Name", "击飞"),
            Description = new LocalizedText("EliteEnemies_Affix_Knockback_Description", "攻击产生强力击退"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        },
        ["Chaos"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Chaos_Name", "混沌"),
            Description = new LocalizedText("EliteEnemies_Affix_Chaos_Description", "攻击随机产生异常效果"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare,
        }.WithLootGroup(
            new LootEntry(408, 1, 2, 0.7f), // 电抗性针
            new LootEntry(1070, 1, 2, 0.7f), // 火抗针
            new LootEntry(1071, 1, 2, 0.7f), // 毒抗针
            new LootEntry(1072, 1, 2, 0.7f) // 空间抗性针
        ),
        ["Vampirism"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Vampirism_Name", "吸血"),
            Description = new LocalizedText("EliteEnemies_Affix_Vampirism_Description"),
            HealthMultiplier = 1.3f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare,
        }.WithRandomLootRange(minQuality: 2, maxQuality: 4, itemCount: 1, dropChance: 1f, tagNames: new[] { "Healing" }),
        ["Collector"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Collector_Name", "收藏家"),
            Description = new LocalizedText("EliteEnemies_Affix_Collector_Description", "掉落各种收藏品"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLootRange(minQuality: 3, maxQuality: 6, itemCount: 2, dropChance: 1f, tagNames: new[] { "Luxury" }),
        ["Gunsmith"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Gunsmith_Name", "枪匠"),
            Description = new LocalizedText("EliteEnemies_Affix_Gunsmith_Description", "掉落枪械配件"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.1f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLootRange(minQuality: 3, maxQuality: 6, itemCount: 2, dropChance: 1f, tagNames: new[] { "Accessory" }),
        ["Slime"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Slime_Name", "史莱姆"),
            Description = new LocalizedText("EliteEnemies_Affix_Slime_Description", "初始巨大但虚弱，随血量降低逐渐缩小并增强伤害，会周期性跳跃"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLootRange(minQuality: 3, maxQuality: 4, itemCount: 2, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Blindness"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Blindness_Name", "致盲"),
            Description = new LocalizedText("EliteEnemies_Affix_Blindness_Description", "攻击使玩家视野受限7秒"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        },
        ["Slow"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Slow_Name", "迟缓"),
            Description = new LocalizedText("EliteEnemies_Affix_Slow_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        },
        ["Stun"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Stun_Name", "震慑"),
            Description = new LocalizedText("EliteEnemies_Affix_Stun_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        },
        ["DungEater"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_DungEater_Name", "食粪者"),
            Description = new LocalizedText("EliteEnemies_Affix_DungEater_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithLootGroup(
            new LootEntry(938, 1, 2, 1f), // 粑粑
            new LootEntry(1257, 1, 5, 1f), // 粪球
            new LootEntry(68, 1, 1, 0.7f) // 巧克力
        )
        .WithRandomLootRange(minQuality: 3, maxQuality: 4, itemCount: 1, dropChance: 0.7f, tagNames: new[] { "Bullet" }),
        ["Hardening"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Hardening_Name", "硬化"),
            Description = new LocalizedText("EliteEnemies_Affix_Hardening_Description"),
            HealthMultiplier = 1.2f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithRandomLootRange(minQuality: 1, maxQuality: 4, itemCount: 1, dropChance: 0.4f, tagNames: new[] { "Armor" }),
        ["ChickenBro"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_ChickenBro_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_ChickenBro_Description"),
            HealthMultiplier = 1.0f,
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
            Name = new LocalizedText("EliteEnemies_Affix_Phase_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Phase_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "JLab" }),
        ["MandarinDuck"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_MandarinDuck_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_MandarinDuck_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "Accessory" }),
        ["Revenge"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Revenge_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Revenge_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithLootGroup(
            new LootEntry(326, 1, 1, 0.5f)// 火箭弹
        ).WithRandomLootRange(minQuality: 3, maxQuality: 5, itemCount: 2, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Obscurer"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Obscurer_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Obscurer_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "MiniGame" })
        .WithRandomLootRange(minQuality: 4, maxQuality: 7, itemCount: 1, dropChance: 1f, tagNames: new[] { "Weapon" }),
        ["Distortion"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Distortion_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Distortion_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 0.6f, tagNames: new[] { "JLab" }),
        ["MultiShot"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_MultiShot_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_MultiShot_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithRandomLootRange(minQuality: 2, maxQuality: 4, itemCount: 2, dropChance: 1f, tagNames: new[] { "Bullet" }),
        ["Guardian"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Guardian_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Guardian_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        },
        ["Grenadier"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Grenadier_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Grenadier_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        },
        ["EMP"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_EMP_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_EMP_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 0.8f, tagNames: new[] { "Electric" }),
        ["Tear"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Tear_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Tear_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithRandomLootRange(minQuality: 3, maxQuality: 5, itemCount: 1, dropChance: 0.7f, tagNames: new[] { "Bullet" }),
        ["Slippery"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Slippery_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Slippery_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        },
        ["Locksmith"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Locksmith_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Locksmith_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "Key" }),
        ["Overload"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Overload_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Overload_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Uncommon
        }.WithRandomLootRange(minQuality: 3, maxQuality: 4, itemCount: 2, dropChance: 0.7f, tagNames: new[] { "Bullet" }),
        ["Mimic"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Mimic_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Mimic_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.1f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        },
        // ItemMimic：Mimic 的对照实验——同一个思路，伪装体换成"躺在地上的一件物品"。
        // 倍率与稀有度**刻意与 Mimic 取同一档**，这样两者的手感差异只来自伪装体本身，
        // 不被数值干扰（词条与行为的完整说明见 Behaviors\ItemMimicBehavior.cs）。
        //
        // 掉落取 Totem 标签，与伪装池同主题：它伪装成的就是它掉的东西。
        ["ItemMimic"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_ItemMimic_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_ItemMimic_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.1f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithRandomLoot(quality: -1, itemCount: 1, dropChance: 1f, tagNames: new[] { "Totem" }),
        ["Reflect"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Reflect_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Reflect_Description"),
            HealthMultiplier = 1.2f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Epic
        },
        ["Frozen"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Frozen_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Frozen_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 0.8f,
            MoveSpeedMultiplier = 0.8f,
            Rarity = AffixRarity.Epic
        },
        ["Nimble"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Nimble_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Nimble_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.2f,
            Rarity = AffixRarity.Uncommon
        },
        ["Phantom"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Phantom_Name"),
            Description = new LocalizedText("EliteEnemies_Affix_Phantom_Description"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        }.WithLootGroup(
            new LootEntry(679, 1, 1, 0.5f), // 耳机
            new LootEntry(1252, 1, 1, 0.2f), // 橘子耳机
            new LootEntry(112, 1, 1, 0.5f), // 麦克风
            new LootEntry(113, 1, 1, 0.5f), // 收音机
            new LootEntry(64, 1, 1, 0.6f), // 对讲机
            new LootEntry(65, 1, 1, 0.2f) // 军用对讲机
        ),
        ["Fester"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Fester_Name", "溃伤"),
            Description = new LocalizedText("EliteEnemies_Affix_Fester_Description", "命中玩家后 10 秒内，治疗效果减半"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        },
        ["AmmoEater"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_AmmoEater_Name", "噬弹"),
            Description = new LocalizedText("EliteEnemies_Affix_AmmoEater_Description", "命中玩家时啃掉其弹匣中的子弹"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Rare
        },
        // 小偷的强度全在"偷"这件事上，所以三围不额外加成——
        // 免得"能抢你东西的敌人"同时还打不死
        ["Thief"] = new AffixData
        {
            Name = new LocalizedText("EliteEnemies_Affix_Thief_Name", "小偷"),
            Description = new LocalizedText("EliteEnemies_Affix_Thief_Description", "命中时偷走玩家背包里的一件物品，击杀它会掉落两件"),
            HealthMultiplier = 1.0f,
            DamageMultiplier = 1.0f,
            MoveSpeedMultiplier = 1.0f,
            Rarity = AffixRarity.Legendary
        }
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
    }
}
