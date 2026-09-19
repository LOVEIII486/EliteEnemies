using System.Collections.Generic;
using System.Text;

namespace EliteEnemies.Stats
{
    /// <summary>
    /// 把 <see cref="StatsSnapshot"/> 渲染成一段可直接贴进日志的报告。
    ///
    /// <para><b>纯函数、零依赖</b>（不引用 UnityEngine），因此可以在游戏外喂假数据跑——
    /// 见 <see cref="StatsSnapshot"/> 的类注释。</para>
    ///
    /// <para><b>它服务的唯一目的是「排查」</b>：玩家出问题时把 Player.log 发过来，
    /// 读这一块就能回答三个问题——① 精英到底生成了没有、没生成是卡在哪一步；
    /// ② 掉落系统有没有在跑、每次掉几件；③ 他当时的配置是什么。
    /// 因此**措辞一律写死中文**，不走本地化：它是给你看的诊断输出，不是玩家界面文本。</para>
    /// </summary>
    internal static class StatsReport
    {
        /// <summary>每个排行榜最多列几项，其余折叠成「…另 N 种」。防日志变长。</summary>
        private const int TopN = 8;

        /// <summary>来源名那一列的对齐宽度（**按显示宽度算**，中文算 2，见 <see cref="Pad"/>）。</summary>
        private const int NameColumn = 24;

        private static readonly string[] RarityLabels = { "普通", "罕见", "稀有", "史诗", "传说" };

        // 顺序即输出顺序；标签与枚举一一对应，改枚举时这里会编译报错（数组长度在 Build 里断言）
        private static readonly SkipReason[] JudgedOrder =
        {
            SkipReason.ChanceMissed, SkipReason.IneligibleType,
            SkipReason.Ignored, SkipReason.NoAffixSelected,
        };
        private static readonly string[] JudgedLabels = { "概率未命中", "类型不合格", "忽略名单", "词条池为空" };

        private static readonly SkipReason[] NotInvolvedOrder =
        {
            SkipReason.MainCharacter, SkipReason.FriendlyTeam, SkipReason.NoPreset,
        };
        private static readonly string[] NotInvolvedLabels = { "主控", "友军", "无预设" };

        internal static string Build(StatsSnapshot s)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("════════════ 精英怪统计（本局累计）════════════");
            sb.AppendLine($"本局 {s.SceneCount} 个战斗场景 | 时长 {Duration(s.DurationSeconds)} | 当前场景: {s.SceneName}");
            sb.AppendLine("[配置] 精英率 普通 " + Pct0(s.NormalEliteChance) +
                          " Boss " + Pct0(s.BossEliteChance) +
                          " 商人 " + Pct0(s.MerchantEliteChance) +
                          " | 词条上限 " + s.MaxAffixCount +
                          " | 词条权重 " + DescribeAffixWeights(s.AffixCountWeights) +
                          " | 掉率倍率 " + s.DropRateMultiplier.ToString("F1") +
                          " | 品质档位 " + s.ItemQualityTier);
            sb.AppendLine("[配置] 词条 " + DescribeAffixes(s));

            AppendSpawn(sb, s);
            AppendLoot(sb, s);

            sb.AppendLine("══════════════════════════════════════════════");
            return sb.ToString();
        }

        // ────────────────────────── 生成 ──────────────────────────

        /// <summary>
        /// 词条**数量**权重的紧凑写法：<c>1-5:50/30/15/4/1</c>。
        ///
        /// <para>它与 <c>词条上限</c> 是两件事、必须一起看：上限只说"最多几条"，
        /// 而"**通常几条**"由这张权重表决定——「词条条数」那段直方图能不能解释，
        /// 全靠这一项。实测吃过一次亏：某份日志里 5 条词条占 27%（默认权重下只该占 1%），
        /// 而报告里没有这个数，读的人只能回头去问作者是不是改过权重。</para>
        ///
        /// <para>三条渲染约定：</para>
        /// <list type="bullet">
        /// <item>下标 0 恒为 0（不允许"0 条词条"），不打印；</item>
        /// <item>**末尾为 0 的档位也省掉**，于是"只放开 1、2 条"读起来就是 <c>1-2:50/30</c>；</item>
        /// <item>负数按 0 显示——<c>AffixSelector.SelectWeightedAffixCount</c> 就是这么 clamp 的
        /// （<c>Mathf.Max(0, weights[i])</c>）。报告必须与判定口径一致，
        /// 否则"报告说有权重、实际被当 0"会让读的人往错的方向推。</item>
        /// </list>
        /// </summary>
        private static string DescribeAffixWeights(int[] weights)
        {
            if (weights == null || weights.Length < 2) return "(未配置)";

            int last = weights.Length - 1;
            while (last >= 1 && weights[last] <= 0) last--;
            if (last < 1) return "(全为 0，按均匀分布)";

            var sb = new StringBuilder();
            sb.Append('1').Append('-').Append(last).Append(':');
            for (int i = 1; i <= last; i++)
            {
                if (i > 1) sb.Append('/');
                sb.Append(weights[i] > 0 ? weights[i] : 0);
            }
            return sb.ToString();
        }

        private static void AppendSpawn(StringBuilder sb, StatsSnapshot s)
        {
            sb.AppendLine("── 生成 ──");

            int skipped = s.Tried - s.Elite;
            sb.AppendLine($"参与判定 {s.Tried} | 精英 {s.Elite} ({Ratio(s.Elite, s.Tried)}) | 跳过 {skipped}");

            // 四类合并过的旧实现就是在这里丢掉答案的——现在它们各自有数。
            sb.AppendLine("  原因: " + JoinCounts(s.Skipped, JudgedOrder, JudgedLabels));

            var notInvolved = new int[NotInvolvedOrder.Length];
            int notInvolvedTotal = 0;
            for (int i = 0; i < NotInvolvedOrder.Length; i++)
            {
                s.Skipped.TryGetValue(NotInvolvedOrder[i], out notInvolved[i]);
                notInvolvedTotal += notInvolved[i];
            }
            sb.Append("  未参与判定 ").Append(notInvolvedTotal).Append("（");
            for (int i = 0; i < notInvolved.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(NotInvolvedLabels[i]).Append(' ').Append(notInvolved[i]);
            }
            sb.AppendLine("）");

            AppendHistogram(sb, "  词条条数: ", s.AffixCountHist, "条", showZeroKeys: false);
            AppendRarity(sb, "  稀有度: ", s.RarityHist);

            sb.Append("  高频词条: ").AppendLine(TopByValue(s.AffixNameHist, TopN, "种"));

            sb.Append("  精英预设: ").AppendLine(TopPresets(s.Presets, TopN));
        }

        private static void AppendRarity(StringBuilder sb, string prefix, int[] hist)
        {
            sb.Append(prefix);
            for (int i = 0; i < RarityLabels.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(RarityLabels[i]).Append(' ').Append(i < hist.Length ? hist[i] : 0);
            }
            sb.AppendLine();
        }

        /// <summary>词条条数分布：键是条数，按升序（1 条、2 条…）。</summary>
        private static void AppendHistogram(StringBuilder sb, string prefix,
                                            Dictionary<int, int> hist, string unit, bool showZeroKeys)
        {
            sb.Append(prefix);
            if (hist.Count == 0)
            {
                sb.AppendLine("（无）");
                return;
            }

            var keys = new List<int>(hist.Keys);
            keys.Sort();

            int min = keys[0], max = keys[keys.Count - 1];
            bool first = true;
            for (int k = min; k <= max; k++)
            {
                hist.TryGetValue(k, out int c);
                if (!showZeroKeys && c == 0) continue;
                if (!first) sb.Append(" | ");
                first = false;
                sb.Append(k).Append(unit).Append(' ').Append(c);
            }
            sb.AppendLine();
        }

        // ────────────────────────── 掉落 ──────────────────────────

        private static void AppendLoot(StringBuilder sb, StatsSnapshot s)
        {
            sb.AppendLine("── 掉落 ──");
            sb.AppendLine($"精英击杀 {s.LootAttempts} | 掉落 {s.TotalDrops} 件 | 平均 " +
                          (s.LootAttempts > 0 ? ((float)s.TotalDrops / s.LootAttempts).ToString("F1") : "0.0") +
                          " 件/只");

            // 品质总分布——这是「品质档位」那个旋钮的直接效果，调参时对着它看
            sb.Append("  品质分布: ");
            int qualitySum = 0;
            for (int q = 1; q <= 7; q++) qualitySum += s.QualityHist[q];

            // ⚠ 判据是**直方图自己有没有数**，不是 TotalDrops 是否为零：
            //    曾经这两个可能不一致（记录侧漏写直方图），那时这里会打出一个**空行**，
            //    看上去像格式问题，其实是数据缺失。现在明确写出「（无）」。
            if (qualitySum == 0)
            {
                sb.AppendLine("（无）");
            }
            else
            {
                bool first = true;
                for (int q = 1; q <= 7; q++)
                {
                    if (s.QualityHist[q] <= 0) continue;
                    if (!first) sb.Append(" | ");
                    first = false;
                    sb.Append('Q').Append(q).Append(' ')
                      .Append(Ratio(s.QualityHist[q], s.TotalDrops));
                }
                sb.AppendLine();
            }

            if (s.Sources.Count == 0)
            {
                sb.AppendLine("  （无来源数据）");
                return;
            }

            var sorted = new List<StatsSnapshot.SourceStat>(s.Sources);
            sorted.Sort((a, b) =>
            {
                int byTotal = b.Total.CompareTo(a.Total);
                return byTotal != 0 ? byTotal : string.CompareOrdinal(a.Name, b.Name);
            });

            int shown = 0, hiddenTotal = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i < TopN)
                {
                    AppendSource(sb, sorted[i], s.LootAttempts);
                    shown++;
                }
                else
                {
                    hiddenTotal += sorted[i].Total;
                }
            }

            // 折叠掉的部分只报总数：它们本来就是为了让「一共掉了多少」对得上账
            if (sorted.Count > shown)
            {
                sb.Append("  …另 ").Append(sorted.Count - shown)
                  .Append(" 个来源，共 ").Append(hiddenTotal).AppendLine(" 件");
            }
        }

        private static void AppendSource(StringBuilder sb, StatsSnapshot.SourceStat src, int attempts)
        {
            sb.Append("  ").Append(Pad(src.Name, NameColumn));

            sb.Append(src.Total.ToString().PadLeft(4)).Append(" 件");

            float per = attempts > 0 ? (float)src.Total / attempts : 0f;
            sb.Append("  ").Append(per.ToString("F2")).Append("/只");

            if (src.Quality != null)
            {
                var dist = new StringBuilder();
                for (int q = 1; q <= 7 && q < src.Quality.Length; q++)
                {
                    if (src.Quality[q] <= 0) continue;
                    if (dist.Length > 0) dist.Append(' ');
                    dist.Append('Q').Append(q).Append(':').Append(src.Quality[q]);
                }
                if (dist.Length > 0) sb.Append("   ").Append(dist);
            }

            sb.AppendLine();
        }

        // ────────────────────────── 小工具 ──────────────────────────

        private static string JoinCounts(Dictionary<SkipReason, int> map, SkipReason[] order, string[] labels)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < order.Length && i < labels.Length; i++)
            {
                map.TryGetValue(order[i], out int c);
                if (i > 0) sb.Append(" | ");
                sb.Append(labels[i]).Append(' ').Append(c);
            }
            return sb.ToString();
        }

        /// <summary>按次数降序排 Top N，其余折叠成「…另 N 种」。空表返回「（无）」。</summary>
        private static string TopByValue(Dictionary<string, int> map, int topN, string unit)
        {
            if (map.Count == 0) return "（无）";

            var items = new List<KeyValuePair<string, int>>(map);
            // 次数相同时按名字排——字典的枚举顺序不保证稳定，不加这个兜底的话
            // 两份日志里同一批并列项会换位置，没法逐行对照（而对照正是看日志在做的事）。
            items.Sort((a, b) =>
            {
                int byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
            });

            var sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= topN) break;
                if (shown > 0) sb.Append(" | ");
                sb.Append(items[i].Key).Append(' ').Append(items[i].Value);
                shown++;
            }
            if (items.Count > shown)
            {
                sb.Append(" | …另 ").Append(items.Count - shown).Append(' ').Append(unit);
            }
            return sb.ToString();
        }

        /// <summary>预设按「成为精英的次数」降序；格式 <c>预设名[显示名] 精英数/判定数</c>。</summary>
        private static string TopPresets(List<StatsSnapshot.PresetStat> presets, int topN)
        {
            if (presets.Count == 0) return "（无）";

            var items = new List<StatsSnapshot.PresetStat>(presets);
            items.Sort((a, b) =>
            {
                int byElite = b.Elite.CompareTo(a.Elite);
                return byElite != 0 ? byElite : b.Tried.CompareTo(a.Tried);
            });

            var sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= topN) break;
                if (shown > 0) sb.Append(" | ");
                var p = items[i];
                sb.Append(p.Key);
                if (!string.IsNullOrEmpty(p.Label)) sb.Append('[').Append(p.Label).Append(']');
                sb.Append(' ').Append(p.Elite).Append('/').Append(p.Tried);
                shown++;
            }
            if (items.Count > shown)
            {
                sb.Append(" | …另 ").Append(items.Count - shown).Append(" 种");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 「启用 X/Y」+ 名单。三种形态，各对应一个真实问题：
        /// <list type="bullet">
        /// <item>全部启用 → 只报数目（名单没有信息量，列 50 个纯属占地方）。</item>
        /// <item>**一个都没启用** → 明确点名。这是"为什么精英不带词条"的直接答案，
        /// 而它在别处完全看不出来（没有词条的精英和"没生成精英"在日志里长得不一样，
        /// 但都很难反推）。</item>
        /// <item>其余 → 列名单，超过 <see cref="TopN"/> 折叠。</item>
        /// </list>
        /// </summary>
        private static string DescribeAffixes(StatsSnapshot s)
        {
            int total = s.AffixPoolTotal;
            int on = s.EnabledAffixes.Count;

            if (on == 0) return $"启用 0/{total} —— 词条池为空，精英不会带任何词条";
            if (total > 0 && on == total) return $"启用 {on}/{total}（全部）";

            var sb = new StringBuilder();
            sb.Append("启用 ").Append(on).Append('/').Append(total).Append(": ");

            int shown = on < TopN ? on : TopN;
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(s.EnabledAffixes[i]);
            }
            if (on > shown) sb.Append(" | …另 ").Append(on - shown).Append(" 个");
            return sb.ToString();
        }

        /// <summary>0–1 的比例 → 整数百分比。</summary>
        private static string Pct0(float ratio) => ((int)(ratio * 100f + 0.5f)) + "%";

        /// <summary>部分/整体 → 一位小数百分比。整体为 0 时给 <c>0.0%</c> 而不是除零。</summary>
        private static string Ratio(int part, int whole)
            => (whole > 0 ? ((float)part / whole * 100f).ToString("F1") : "0.0") + "%";

        private static string Duration(float seconds)
        {
            int total = (int)seconds;
            int h = total / 3600, m = total % 3600 / 60, sec = total % 60;
            return h > 0 ? $"{h}h{m:D2}m" : m > 0 ? $"{m}m{sec:D2}s" : $"{sec}s";
        }

        /// <summary>
        /// 按**显示宽度**右侧补空格。
        ///
        /// <para>⚠ 不能用 <see cref="string.PadRight(int)"/>：它按 UTF-16 码元数算，
        /// 而中文在等宽终端里占**两个**字符位——那样算出来的列必然错位，
        /// 而这份报告正是靠列对齐才一眼看得出哪个来源在贡献。</para>
        /// </summary>
        private static string Pad(string text, int width)
        {
            text = text ?? "";
            int w = 0;
            for (int i = 0; i < text.Length; i++) w += IsWide(text[i]) ? 2 : 1;

            if (w >= width) return text;

            var sb = new StringBuilder(text);
            sb.Append(' ', width - w);
            return sb.ToString();
        }

        /// <summary>CJK / 全角区段判定（够用即可，不求覆盖 Unicode East Asian Width 全表）。</summary>
        private static bool IsWide(char c)
            => (c >= 0x1100 && c <= 0x115F)      // 谚文字母
            || (c >= 0x2E80 && c <= 0x303E)      // 部首、标点
            || (c >= 0x3041 && c <= 0x33FF)      // 假名、注音、CJK 兼容
            || (c >= 0x3400 && c <= 0x4DBF)      // 扩展 A
            || (c >= 0x4E00 && c <= 0x9FFF)      // 基本汉字
            || (c >= 0xA000 && c <= 0xA4CF)      // 彝文
            || (c >= 0xAC00 && c <= 0xD7A3)      // 谚文音节
            || (c >= 0xF900 && c <= 0xFAFF)      // CJK 兼容汉字
            || (c >= 0xFE30 && c <= 0xFE6F)      // CJK 兼容形式
            || (c >= 0xFF00 && c <= 0xFF60)      // 全角形式
            || (c >= 0xFFE0 && c <= 0xFFE6);
    }
}
