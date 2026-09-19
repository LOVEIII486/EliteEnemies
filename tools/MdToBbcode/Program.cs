using System.Text;
using System.Text.RegularExpressions;

namespace MdToBbcode;

/// <summary>
/// 把 <c>workshop\description\*.md</c> 转成可直接粘贴到 Steam 创意工坊的 BBCode。
///
/// <para><b>为什么要有这个工具</b>：Steam 的创意工坊简介只吃 BBCode，而仓库里可读性最好的
/// 是 Markdown。历史上这两份是各写一份、手工同步的，结果**双向漂移**，且已经产生了
/// 事实性矛盾——例如「分裂」分身的掉落件数在一份里写 1 件、另一份写 2 件；
/// 「时停」的稀有度在一份里归【稀有】、另一份归【史诗】（后者经源码
/// <c>EliteAffix.cs</c> 的 <c>AffixRarity.Rare</c> 判定为错）。
/// 现在改为：<b>Markdown 是唯一源</b>，BBCode 由本工具生成，不再手工维护。</para>
/// </summary>
internal static class Program
{
    /// <summary>
    /// 所有输出行的前缀。**必须与 <c>EliteEnemies.csproj</c> 里的 <c>_EliteTag</c>
    /// 逐字相同**——本工具是被构建用 <c>Exec</c> 拉起来的，它的 stdout 会被原样
    /// 转发进构建日志，前缀不统一就分不清哪句是模组说的、哪句是 MSBuild 自带。
    /// 统一格式：<c>[EliteEnemies] &lt;阶段&gt; · &lt;内容&gt;</c>。
    /// </summary>
    private const string Tag = "[EliteEnemies]";

    private static int Main(string[] args)
    {
        // 默认路径相对仓库根目录，即本文件所在目录的上两级
        string repoRoot = FindRepoRoot();
        string sourceDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(repoRoot, "workshop", "description");
        string outputDir = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.Combine(repoRoot, "artifacts", "workshop");

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"找不到源目录：{sourceDir}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        string[] files = Directory.GetFiles(sourceDir, "*.md");
        if (files.Length == 0)
        {
            Console.Error.WriteLine($"在 {sourceDir} 下没找到 .md 文件");
            return 1;
        }

        // 无 BOM 的 UTF-8：粘贴到 Steam 时不会把 BOM 一起带进去
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        int problemCount = 0;

        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file, Encoding.UTF8);
            List<string> bbcode = Convert(lines, out List<string> warnings);

            string target = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file) + ".bbcode.txt");
            File.WriteAllLines(target, bbcode, utf8NoBom);

            Console.WriteLine($"{Tag} 工坊简介 · 生成 {target}（{bbcode.Count} 行）");

            // ⚠ 这些不是"小瑕疵"：转换不出来的写法会**原样**出现在 Steam 页面上，
            //    而生成过程一切正常、没有任何报错——属于本工程最忌讳的静默失效。
            foreach (string w in warnings)
            {
                Console.WriteLine($"{Tag} 工坊简介 · ⚠ {Path.GetFileName(file)}: {w}");
            }
            problemCount += warnings.Count;
        }

        // ⚠ **有转换不了的写法就返回非零**：产物是直接往 Steam 上贴的，
        //    "生成成功但页面上多了一堆竖线"不该被当成成功。
        if (problemCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{Tag} 工坊简介 · ❌ {problemCount} 处写法转换不了（见上）。产物已生成，但**先修好再粘**。");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"{Tag} 工坊简介 · 把生成的文件内容整段复制到 Steam 创意工坊的简介栏即可。");
        return 0;
    }

    /// <summary>从当前目录向上找含 EliteEnemies.csproj 的目录。</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EliteEnemies.csproj")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static List<string> Convert(string[] lines, out List<string> warnings)
    {
        var output = new List<string>();
        var localWarnings = new List<string>();
        warnings = localWarnings;

        bool inList = false;
        bool warnedTable = false;

        void CloseList()
        {
            if (inList)
            {
                output.Add("[/list]");
                inList = false;
            }
        }

        void OpenList()
        {
            if (!inList)
            {
                output.Add("[list]");
                inList = true;
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            // 去掉行尾空白。Markdown 用「行尾两个空格」表示强制换行，BBCode 不需要。
            string line = lines[i].TrimEnd();

            // ── 空行 ────────────────────────────────────────────────
            // ⚠ **空行不一定结束列表**——这条规则是发布版结构的关键：
            //   ① 列表**内部**的空行要保留（上架稿用它在稀有度分组之间留白）；
            //   ② 列表**之前**的空行要丢掉（BBCode 的 [list] 自带间距，多一个空行版面就变了）。
            // 判据是「后面还有没有列表内容」，见 NextIsListContent。
            if (line.Length == 0)
            {
                if (NextIsListContent(lines, i + 1))
                {
                    if (inList)
                    {
                        output.Add(string.Empty);
                    }
                    // 不在列表里：这个空行丢掉，紧接着的列表项会开一个 [list]
                }
                else
                {
                    CloseList();
                    output.Add(string.Empty);
                }
                continue;
            }

            // ── Markdown 表格：转换不了 ──────────────────────────────
            //   BBCode 没有表格语义。原样输出的结果是页面上出现一堆竖线，
            //   而生成过程一切正常、不报任何错——典型的静默失效。
            //   这里只负责**发现**，是否算失败由调用方决定（见 Main 的退出码）。
            if (line.TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                if (!warnedTable)
                {
                    warnedTable = true;
                    localWarnings.Add("检测到 Markdown 表格——BBCode 不支持，会原样输出成带竖线的文本。" +
                                      "请改用 `*` 列表：本工具只认列表 / 标题 / 分隔线 / 引用这四类结构。");
                }
            }

            // ── 水平线 ──────────────────────────────────────────────
            if (Regex.IsMatch(line, @"^\s*-{3,}\s*$"))
            {
                CloseList();
                output.Add("[hr][/hr]");
                continue;
            }

            // ── 列表项 "* xxx" / "- xxx" ────────────────────────────
            // 连续的列表项要包在 [list]...[/list] 里，Steam 才会渲染成项目符号
            Match listItem = Regex.Match(line, @"^\s*[*-]\s+(.*)$");
            if (listItem.Success)
            {
                OpenList();
                output.Add("[*]" + ConvertInline(listItem.Groups[1].Value));
                continue;
            }

            // ── 标题 ────────────────────────────────────────────────
            //   #   → [h1]
            //   ##  → [h2]
            //   ### → 降级为加粗，且**当它是列表的分组标题时留在列表内部**
            Match heading = Regex.Match(line, @"^\s*(#{1,6})\s+(.*)$");
            if (heading.Success)
            {
                int level = heading.Groups[1].Value.Length;
                string text = ConvertInline(heading.Groups[2].Value);

                // "### **【普通】**" 这种分组标题：下一行就是列表项 ⇒ 它是列表内容，
                // 必须**开/续列表**而不是结束它。发布版的 [list] 是**一个**连续块，
                // 五个稀有度分组都在里面；早先的实现会在每个分组标题处切一刀，
                // 于是生成出五个 [list]，与上架稿的版面不一致。
                if (level >= 3 && NextIsListItem(lines, i + 1))
                {
                    OpenList();
                    output.Add(AsBold(text));
                    continue;
                }

                CloseList();

                if (level >= 3)
                {
                    output.Add(AsBold(text));
                }
                else
                {
                    output.Add(level == 1 ? $"[h1]{text}[/h1]" : $"[h2]{text}[/h2]");
                }
                continue;
            }

            // ── 引用 "> xxx"（可连续多行）────────────────────────────
            Match quote = Regex.Match(line, @"^\s*>\s*(.*)$");
            if (quote.Success)
            {
                CloseList();
                output.Add("[quote]");
                output.Add(ConvertInline(quote.Groups[1].Value));

                while (i + 1 < lines.Length)
                {
                    string next = lines[i + 1].TrimEnd();
                    Match nextQuote = Regex.Match(next, @"^\s*>\s*(.*)$");
                    if (!nextQuote.Success)
                    {
                        break;
                    }
                    i++;
                    output.Add(ConvertInline(nextQuote.Groups[1].Value));
                }

                output.Add("[/quote]");
                continue;
            }

            // ── 列表项的续行（缩进 ≥2 空格，且前面没有空行）──────────
            // Markdown 里一个列表项可以跨多行，BBCode 里直接跟在 [*] 后面即可。
            // 缩进要去掉——BBCode 不认它，留着只会在页面上多出空白。
            if (inList)
            {
                output.Add(ConvertInline(line.TrimStart()));
                continue;
            }

            // ── 普通段落 ────────────────────────────────────────────
            output.Add(ConvertInline(line));
        }

        CloseList();
        return output;
    }

    /// <summary>
    /// 把标题文字包成粗体。源里常写成 <c>### **【普通】**</c>，行内转换已经把
    /// <c>**..**</c> 变成 <c>[b]..[/b]</c> 了，这里再加一层会得到 <c>[b][b]..[/b][/b]</c>。
    /// </summary>
    private static string AsBold(string text) =>
        text.StartsWith("[b]", StringComparison.Ordinal) && text.EndsWith("[/b]", StringComparison.Ordinal)
            ? text
            : $"[b]{text}[/b]";

    /// <summary>跳过空行后，下一行是不是列表项（<c>* xxx</c> / <c>- xxx</c>）。</summary>
    private static bool NextIsListItem(string[] lines, int start)
    {
        for (int i = start; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }
            return Regex.IsMatch(line, @"^\s*[*-]\s+");
        }
        return false;
    }

    /// <summary>
    /// 空行之后的第一个非空行，是否**仍然属于列表**——决定这个空行是「列表内留白」还是
    /// 「列表到此结束」。
    /// <para>三种都算：列表项、缩进续行、以及后跟列表项的分组标题（<c>###</c>）。</para>
    /// </summary>
    private static bool NextIsListContent(string[] lines, int start)
    {
        for (int i = start; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            if (Regex.IsMatch(line, @"^\s*[*-]\s+")) return true;  // 列表项
            if (Regex.IsMatch(line, @"^\s{2,}\S")) return true;    // 缩进续行

            Match heading = Regex.Match(line, @"^\s*(#{3,6})\s+(.*)$");
            if (heading.Success && NextIsListItem(lines, i + 1)) return true;  // 分组标题

            return false;
        }
        return false;
    }

    /// <summary>行内标记转换。</summary>
    private static string ConvertInline(string text)
    {
        string s = text;

        // 图片先处理，避免被链接规则吃掉
        s = Regex.Replace(s, @"!\[([^\]]*)\]\(([^)]*)\)", "[img]$2[/img]");

        // 链接 [文字](url)。
        // 空 url 的形式（如 [Elite Enemies]()）只保留文字——原 BBCode 就是这么处理的，
        // 而且粘到 Steam 后空链接会变成不可点的死链。
        s = Regex.Replace(s, @"\[([^\]]*)\]\(\s*\)", "$1");
        s = Regex.Replace(s, @"\[([^\]]*)\]\(([^)]+)\)", "[url=$2]$1[/url]");

        // 粗体先于斜体，否则 **x** 会被斜体规则从中间切开
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "[b]$1[/b]");
        s = Regex.Replace(s, @"(?<![\*\w])\*([^\*\n]+?)\*(?![\*\w])", "[i]$1[/i]");

        // 行内代码 `x` → 去掉反引号（Steam 没有对应标记）
        s = Regex.Replace(s, @"`([^`]*)`", "$1");

        return s;
    }
}
