using System;
using System.Collections.Generic;
using EliteEnemies.Stats;

namespace StatsReportPreview;

/// <summary>
/// 在**游戏外**把本局统计报告渲染出来：核对版式，并断言各种边界不炸。
///
/// <para><b>为什么需要它</b>：<see cref="StatsReport"/> / <see cref="StatsSnapshot"/>
/// 刻意做成**零依赖**（不引用 UnityEngine），目的就是"能在游戏外喂假数据跑"。
/// 但在此之前，那份能力只写在注释里、**没有任何机制**——本工程没法自动化测试
/// 游戏内行为，于是每次改动报告仍然只能靠肉眼读代码。本工具把那句声明变成可执行的东西。</para>
///
/// <para><b>它测的是真源码</b>：见 csproj 注释——直接编译仓库里的三个文件，
/// 不是复制一份到这里。所以报告改了、这里立刻跟着变；改坏了断言会红。</para>
///
/// <para>用法（在仓库根目录；参数名与用法见 csproj 注释——XML 注释里写不出那两个减号）：</para>
/// <code>
/// dotnet run 加上项目参数指向 tools\StatsReportPreview          # 版式预览 + 边界断言
/// dotnet run（同上）再加一个 check 参数                          # 只跑断言（安静）
/// </code>
///
/// <para>退出码：0 = 全部断言通过；1 = 有断言未通过（可挂进构建当门）。</para>
/// </summary>
internal static class Program
{
    /// <summary>只跑断言、不打版式预览的参数。刻意用不带减号的短名——见 csproj 注释。</summary>
    private const string CheckArg = "check";

    private static int Main(string[] args)
    {
        bool checkOnly = Array.IndexOf(args, CheckArg) >= 0;

        if (!checkOnly)
        {
            PrintPreview();
            Console.WriteLine();
        }

        int failed = RunAssertions();

        if (failed == 0)
        {
            Console.WriteLine();
            Console.WriteLine("✅ 边界断言全部通过");
            return 0;
        }

        Console.WriteLine();
        Console.Error.WriteLine($"❌ {failed} 项边界断言未通过");
        return 1;
    }

    // ══════════════════════════ 版式预览 ══════════════════════════

    /// <summary>
    /// 打印一份"像真的一样"的报告。
    ///
    /// <para>数据刻意取**一局真实的调查现场**（作者报来的那份日志）：配置被改过、
    /// 词条只启用了一部分、多数敌人是带掉率惩罚的拾荒者。版式问题（列没对齐、
    /// 折叠阈值不对、长名字撑破一行）在这份数据上比在"理想数据"上更容易看出来。</para>
    /// </summary>
    private static void PrintPreview()
    {
        Console.WriteLine("── 版式预览（假数据，非实机）──");
        Console.WriteLine(StatsReport.Build(BuildRealisticSnapshot()));
    }

    /// <summary>一份形状完整、数值仿真的快照——用来盯版式。</summary>
    private static StatsSnapshot BuildRealisticSnapshot()
    {
        var s = new StatsSnapshot
        {
            SceneName = "Level_GroundZero_1",
            SceneCount = 1,
            DurationSeconds = 7 * 60 + 28,

            NormalEliteChance = 1f,
            BossEliteChance = 1f,
            MerchantEliteChance = 1f,
            MaxAffixCount = 5,
            AffixCountWeights = new[] { 0, 50, 30, 15, 4, 50 },
            DropRateMultiplier = 1.0f,
            ItemQualityTier = 1,
            AffixPoolTotal = 53,
            EnabledAffixes = new List<string>
            {
                "AmmoEater", "Distortion", "EMP", "Explosive", "Fester", "Frozen",
                "Locksmith", "MandarinDuck", "MultiShot", "Nimble", "Overload",
                "Regeneration", "Revenge", "Slippery", "Split", "Talkative",
            },

            Tried = 79,
            Elite = 69,
            AffixCountHist = new Dictionary<int, int>
            {
                [1] = 27, [2] = 11, [3] = 9, [4] = 2, [5] = 19, [9] = 1,
            },
            AffixNameHist = new Dictionary<string, int>
            {
                ["Overload"] = 18, ["Regeneration"] = 16, ["Slippery"] = 14,
                ["Nimble"] = 12, ["Talkative"] = 12, ["Fester"] = 11,
                ["Distortion"] = 9, ["Tear"] = 9, ["Frozen"] = 8, ["EMP"] = 7,
            },
            RarityHist = new[] { 0, 67, 70, 41, 10 },

            LootAttempts = 54,
            TotalDrops = 119,
        };

        s.Skipped[SkipReason.Ignored] = 8;
        s.Skipped[SkipReason.IneligibleType] = 2;

        s.Presets.Add(new StatsSnapshot.PresetStat
        { Key = "EnemyPreset_Scav_low", Label = "拾荒者", Tried = 24, Elite = 24 });
        s.Presets.Add(new StatsSnapshot.PresetStat
        { Key = "EnemyPreset_Scav", Label = "拾荒者", Tried = 7, Elite = 7 });
        s.Presets.Add(new StatsSnapshot.PresetStat
        { Key = "EnemyPreset_Animal_Wolf", Label = "狼", Tried = 6, Elite = 6 });
        s.Presets.Add(new StatsSnapshot.PresetStat
        { Key = "EnemyPreset_Scav_Melee", Label = "暴走拾荒者", Tried = 6, Elite = 6 });
        s.Presets.Add(new StatsSnapshot.PresetStat
        { Key = "EnemyPreset_Mushroom", Label = "行走菇", Tried = 3, Elite = 3 });

        s.QualityHist[1] = 13; s.QualityHist[2] = 29; s.QualityHist[3] = 34;
        s.QualityHist[4] = 25; s.QualityHist[5] = 18;

        s.Sources.Add(MakeSource("稀有度奖励", 31, Q5: 13));
        s.Sources.Add(MakeSource("词缀随机(Overload)", 12, Q2: 6, Q3: 4, Q4: 2));
        s.Sources.Add(MakeSource("词缀随机(Regeneration)", 12, Q1: 3, Q2: 3, Q3: 2, Q4: 4));
        s.Sources.Add(MakeSource("词缀固定", 10, Q2: 3, Q4: 5, Q5: 2));
        s.Sources.Add(MakeSource("词缀随机(MultiShot)", 8, Q1: 2, Q2: 2, Q3: 3, Q4: 1));
        s.Sources.Add(MakeSource("词缀随机(Revenge)", 8, Q2: 2, Q3: 5, Q4: 1));
        s.Sources.Add(MakeSource("词缀随机(Split)", 8, Q1: 1, Q3: 5, Q4: 1, Q5: 1));
        s.Sources.Add(MakeSource("词缀随机(Talkative)", 7, Q1: 5, Q2: 2));
        // 第 9 项起会被折叠成「…另 N 个来源」——留两项专门盯那条路径。
        //
        // ⚠ 其中一项刻意用**真实存在的最长来源名**：`词缀随机(MagazineCurse)`，
        //   显示宽度 23，而 NameColumn = 24 —— **只余 1 格**。Pad 只补齐、不截断，
        //   所以再加一个更长的词条 key，这一列就会整体右移。放在预览里是为了让那
        //   条边界一直看得见，而不是等它哪天真的错位。（超长时的"不炸"由断言覆盖。）
        s.Sources.Add(MakeSource("词缀随机(MagazineCurse)", 15, Q3: 15));
        s.Sources.Add(MakeSource("词缀随机(Fester)", 8, Q2: 4, Q3: 4));

        return s;
    }

    /// <summary>
    /// 造一个掉落来源。品阶分布用命名参数给，只写关心的那几档。
    /// <para>「/只」那个分母不在这里——报告是从快照的 <c>LootAttempts</c> 算的，
    /// 与单个来源无关。</para>
    /// </summary>
    private static StatsSnapshot.SourceStat MakeSource(
        string name, int total, int Q1 = 0, int Q2 = 0, int Q3 = 0,
        int Q4 = 0, int Q5 = 0, int Q6 = 0, int Q7 = 0)
    {
        var quality = new int[8];
        quality[1] = Q1; quality[2] = Q2; quality[3] = Q3; quality[4] = Q4;
        quality[5] = Q5; quality[6] = Q6; quality[7] = Q7;
        return new StatsSnapshot.SourceStat { Name = name, Total = total, Quality = quality };
    }

    // ══════════════════════════ 边界断言 ══════════════════════════

    private static int RunAssertions()
    {
        int failed = 0;

        Console.WriteLine("── 词条数量权重的渲染 ──");
        failed += AssertWeight("默认", new[] { 0, 50, 30, 15, 4, 1 }, "1-5:50/30/15/4/1");
        failed += AssertWeight("末尾全 0", new[] { 0, 50, 30, 0, 0, 0 }, "1-2:50/30");
        failed += AssertWeight("只放开 1 条", new[] { 0, 50, 0, 0, 0, 0 }, "1-1:50");
        failed += AssertWeight("全 0", new[] { 0, 0, 0, 0, 0, 0 }, "(全为 0，按均匀分布)");
        failed += AssertWeight("空数组", new int[0], "(未配置)");
        failed += AssertWeight("null", null, "(未配置)");
        failed += AssertWeight("长度 1", new[] { 0 }, "(未配置)");
        failed += AssertWeight("中间夹负数", new[] { 0, 50, -3, 15 }, "1-3:50/0/15");

        Console.WriteLine();
        Console.WriteLine("── 报告整体不炸（喂各种极端组合）──");
        failed += AssertNoThrow("全新快照（全默认值）", new StatsSnapshot());
        failed += AssertNoThrow("零精英零掉落", new StatsSnapshot
        { Tried = 0, Elite = 0, LootAttempts = 0, TotalDrops = 0 });
        failed += AssertNoThrow("时长为 0", new StatsSnapshot { DurationSeconds = 0f });
        failed += AssertNoThrow("来源表里有 Total=0 的项", new StatsSnapshot
        { LootAttempts = 0, TotalDrops = 0, Sources = { MakeSource("零来源", 0, 0) } });
        failed += AssertNoThrow("来源质量为 null", new StatsSnapshot
        { LootAttempts = 1, TotalDrops = 0, Sources = { new StatsSnapshot.SourceStat { Name = "无质量数组", Total = 0 } } });
        failed += AssertNoThrow("超长预设名与超长显示名", new StatsSnapshot
        {
            Presets =
            {
                new StatsSnapshot.PresetStat
                {
                    Key = "EnemyPreset_" + new string('X', 90),
                    Label = new string('长', 40),
                    Tried = 1, Elite = 1,
                },
            },
        });
        failed += AssertNoThrow("词条条数键不连续（1 与 9）", new StatsSnapshot
        { AffixCountHist = { [1] = 1, [9] = 1 } });
        failed += AssertNoThrow("启用词条为空（池子非空）", new StatsSnapshot { AffixPoolTotal = 53 });

        return failed;
    }

    /// <summary>断言报告里「词条权重」那一段的渲染结果。</summary>
    private static int AssertWeight(string label, int[] weights, string expected)
    {
        string actual;
        try
        {
            actual = WeightSegment(weights);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ {label,-12} → 抛异常 {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Console.WriteLine($"  ✓ {label,-12} → {actual}");
            return 0;
        }

        Console.WriteLine($"  ✗ {label,-12} → 期望 {expected}，实际 {actual}");
        return 1;
    }

    /// <summary>
    /// 从报告里切出「词条权重」那一段的值。
    /// <para>⚠ 刻意**按标记切**而不是整行比对：整行比对会在无关的配置项一改就红，
    /// 那会让人学会"看到红就改期望值"，断言也就失去意义。这里只钉住本工具负责的那一项。</para>
    /// </summary>
    private static string WeightSegment(int[] weights)
    {
        const string marker = "| 词条权重 ";
        string report = StatsReport.Build(new StatsSnapshot { AffixCountWeights = weights });

        foreach (string line in report.Split('\n'))
        {
            int at = line.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) continue;

            int from = at + marker.Length;
            int to = line.IndexOf(" | ", from, StringComparison.Ordinal);
            return (to < 0 ? line.Substring(from) : line.Substring(from, to - from)).Trim();
        }

        return "(报告里找不到「词条权重」段)";
    }

    /// <summary>断言渲染不抛异常。</summary>
    private static int AssertNoThrow(string label, StatsSnapshot snapshot)
    {
        try
        {
            StatsReport.Build(snapshot);
            Console.WriteLine($"  ✓ {label}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ {label} → 抛异常 {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
