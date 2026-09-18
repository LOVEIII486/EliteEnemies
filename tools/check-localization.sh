#!/usr/bin/env bash
# 校验 localization/*.csv 与代码里的键引用是否自洽。
#
# 为什么要它：CSV 的坑**全是静默的**——
#   · 键拼错 / 忘加前缀 → 运行时 `GetText` 返回兜底值，界面显示 *键名*，不报错
#   · 某个语言文件漏译 → 该语言下显示别的语言（或键名）
#   · 值里含 ASCII 逗号却没加引号 → 整行错位，且 MiniExcel 会**静默**读错
# 没有任何一条会在构建期或运行期报出来。所以由这个脚本一次性查掉。
#
# 用法：bash tools/check-localization.sh [仓库根目录]
# 退出码：0 = 全部通过；1 = 有问题（问题逐条打印）

set -u

ROOT="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
LOC="$ROOT/localization"
SRC="$ROOT/src"
PREFIX="EliteEnemies_"

bad=0
fail() { echo "  ❌ $1"; bad=$((bad + 1)); }

if [ ! -d "$LOC" ]; then echo "找不到 $LOC"; exit 1; fi

# ── 1. 每一行都是 4 列（引号感知——值里的逗号必须被引号包住） ──
echo "── 1. 列数（应为 4，引号感知） ──"
for f in "$LOC"/*.csv; do
    awk -F, '
        { n = 1; inq = 0
          for (i = 1; i <= length($0); i++) {
              c = substr($0, i, 1)
              if (c == "\"") inq = !inq
              else if (c == "," && !inq) n++
          }
          if (n != 4) printf("  ❌ %s:%d 有 %d 列（含逗号的值要加引号）: %s\n", FILENAME, NR, n, $0)
        }' "$f"
done
# awk 不设退出码，用 grep 复核一次「偶数个引号」
for f in "$LOC"/*.csv; do
    if [ $(( $(tr -cd '"' < "$f" | wc -c) % 2 )) -ne 0 ]; then fail "$(basename "$f") 的引号不成对"; fi
done

# ── 2. 键：无重复、无空、都带前缀 ──
echo "── 2. 键的形式 ──"
for f in "$LOC"/*.csv; do
    name=$(basename "$f")
    dup=$(awk -F, 'NR > 1 && $1 != "" { print $1 }' "$f" | sort | uniq -d)
    [ -n "$dup" ] && fail "$name 有重复键："$'\n'"$dup"
    noprefix=$(awk -F, -v p="$PREFIX" 'NR > 1 && $1 != "" && index($1, p) != 1 { print $1 }' "$f")
    [ -n "$noprefix" ] && fail "$name 有未加前缀 '$PREFIX' 的键："$'\n'"$noprefix"
done

# ── 3. 各语言键集合一致 ──
echo "── 3. 各语言键集合是否一致 ──"
base=$(ls "$LOC"/*.csv | head -1)
awk -F, 'NR > 1 && $1 != "" { print $1 }' "$base" | sort > /tmp/.loc_base.txt
for f in "$LOC"/*.csv; do
    [ "$f" = "$base" ] && continue
    awk -F, 'NR > 1 && $1 != "" { print $1 }' "$f" | sort > /tmp/.loc_cur.txt
    miss=$(comm -23 /tmp/.loc_base.txt /tmp/.loc_cur.txt)
    extra=$(comm -13 /tmp/.loc_base.txt /tmp/.loc_cur.txt)
    [ -n "$miss" ] && fail "$(basename "$f") 缺少（以 $(basename "$base") 为准）："$'\n'"$miss"
    [ -n "$extra" ] && fail "$(basename "$f") 多出："$'\n'"$extra"
done

# ── 4. 代码里出现的 "EliteEnemies_…" 字面量都在 CSV 里 ──
#
# ⚠ **不能只查 `GetText("…")`。** 本项原先正是那么写的，而设置项改成规格表之后，
# 描述键是**作为变量**传进 GetText 的（`LocalizationManager.GetText(DescKey)`）——
# 字面量出现在 `GameConfig.Specs` 的声明里，不在 GetText 的实参位置。
# 只查 GetText("…") 会**完全看不见**这些键：键名写错不会有任何提示，
# 只在界面上静默显示成 `*EliteEnemies_Settings_Whatever*`。
#
# 判据 = ①所有 `GetText("…")` 的实参
#      ∪ ②`src/Settings/` 下所有 `"EliteEnemies_…"` 字面量
#      ∪ ③所有 `new LocalizedText("…")` 的实参
#
# ⚠ ②**不能**扩大成「全工程的 `"EliteEnemies_…"` 字面量」——实测那会误报三处：
#   `ModBehaviour.cs` 里 `new GameObject("EliteEnemies_LootHelper")` 这类**对象名**。
#   它们的形状与键一模一样，只有用途不同。
#   限定在 `src/Settings/` 是安全的：那个目录里不会创建 GameObject，
#   且将来新增设置项的描述键会自动被覆盖到。
#
# ⚠ ③ 是 2026-09-18 补的：**词条表**（`EliteAffixes.cs`，50 条 × Name/Description）
#   与 **Combo 表**（`EliteComboRegistry.cs`）的键**全部**是 `new LocalizedText("…")` 写法，
#   而它在上面两条的覆盖范围之外——"新增词条漏加 CSV 键"正是本模块最容易漏的一步
#   （见 docs\词条模块审查与设计.md §5）；漏了不会报错，只会在非中文界面静默显示兜底文本。
#   这里刻意写全 `new LocalizedText("`，而不是只匹配 `"EliteEnemies_…"`——
#   后者会踩上面那个"对象名误报"的坑，前者形状窄，只会出现在真正用本地化的地方。
echo "── 4. 代码引用的键是否都在 CSV 里 ──"
awk -F, 'FNR > 1 && $1 != "" { print $1 }' "$LOC"/*.csv | sort -u > /tmp/.loc_keys.txt
{
    grep -rhoE 'GetText\("[A-Za-z_0-9]+"' "$SRC" --include=*.cs 2>/dev/null \
        | sed 's/.*GetText("//; s/"//'
    grep -rhoE '"EliteEnemies_[A-Za-z_0-9]*"' "$SRC/Settings" --include=*.cs 2>/dev/null \
        | tr -d '"'
    grep -rhoE 'new LocalizedText\("[A-Za-z_0-9]+"' "$SRC" --include=*.cs 2>/dev/null \
        | sed 's/.*LocalizedText("//; s/"//'
} | sort -u > /tmp/.loc_used.txt
missing=$(comm -23 /tmp/.loc_used.txt /tmp/.loc_keys.txt)
[ -n "$missing" ] && fail "代码引用了 CSV 里没有的键："$'\n'"$missing"

# ── 5. 代码里不存在「未加前缀」的键 ──
#    域前缀从 CSV 自己推出来（键形如 EliteEnemies_<域>_…），所以新增域也不用改脚本。
echo "── 5. 代码里有没有漏加前缀的键 ──"
domains=$(sed "s/^$PREFIX//" /tmp/.loc_keys.txt | awk -F_ '{ print $1 }' | sort -u | paste -sd'|')
if [ -n "$domains" ]; then
    hits=$(grep -rnoE "\"($domains)_[A-Za-z_0-9]*\"" "$SRC" --include=*.cs 2>/dev/null || true)
    if [ -n "$hits" ]; then
        fail "以下字面量看起来是本地化键但**没有** '$PREFIX' 前缀："$'\n'"$hits"
    fi
fi

# ── 6. 代码里不存在「把 GetText 结果存进字段」的写法 ──
#    这类写法的病是**静默**的：存下来那一刻的语言被焊死，之后切语言永远读到旧文本。
#    正确写法是把键存起来（LocalizedText），或写成 `private string X => GetText(...)` 属性。
#    本工程为这个病栽过一次（切语言后精英头顶词条名永远是中文），故设此闸。
#
#    两条判据分别对应两种已知形态，见 AGENT.md §3.5 的四形态表：
echo "── 6. 有没有把 GetText 结果存进字段（冻结） ──"

# 用 awk 把**整条成员声明**拼起来再判定，而不是逐行匹配。
#
# ⚠ 这一项最初写成两条 grep，结果**两条都不响**（故障注入一试就现形）：
#   · 一条用了 `grep -P`，而本机 locale 下它直接报错、被我重定向掉，永远返回空；
#   · 另一条逐行匹配，于是「`=` 在第一行、`GetText(` 在第二行」的跨行写法漏掉了——
#     而那恰恰是 `EliteAffixes.cs` 里真实存在的写法。
#   教训与 AGENT.md §3.2 同一条：**检测要覆盖同一个问题的全部写法**，否则它会静默地不响。
#
# 判定规则（对每条成员声明）：
#   · 含 `Lazy<` 且含 `GetText(`          → 违规（Lazy 的语义就是「首次使用即冻结」）
#   · 含 `GetText(` 且是 `=` 赋值而非 `=>` 属性 → 违规
#   · 排除 `string GetText(` 的方法声明本身
awk_prog='
    /^[[:space:]]*(private|public|internal|protected|static|readonly)/ {
        buf = $0; start = FNR
        # 累积到语句结束——但**遇到 `{` 就停**：那是方法/属性体的开始，不是字段初始化器。
        # 不设这个条件的话，方法签名行（不以 `;` 结尾）会把整个方法体吃进来，
        # 于是「方法内取文本」被判成「存进字段」，闸门全是误报。
        while (buf !~ /;[[:space:]]*$/ && buf !~ /\{/ && (getline nxt) > 0) buf = buf " " nxt
        gsub(/\r/, "", buf)
        if (buf ~ /\{/) next
        if (buf ~ /string[[:space:]]+GetText\(/) next          # GetText 自己的方法声明
        if (buf !~ /GetText\(/) next
        if (buf ~ /Lazy</) {
            printf("  ❌ %s:%d Lazy 里取本地化文本（首次使用即冻结），应改为直接属性：%s\n", FILENAME, start, buf)
            bad++
        } else if (buf ~ /=/ && buf !~ /=>/) {
            printf("  ❌ %s:%d 把 GetText 结果存进了字段（切语言后不更新），应存键或写成 => 属性：%s\n", FILENAME, start, buf)
            bad++
        }
    }
    END { exit (bad > 0 ? 1 : 0) }
'
if ! find "$SRC" -name '*.cs' -print0 | xargs -0 awk "$awk_prog" bad=0; then
    fail "第 6 项有命中（见上）"
fi

echo
if [ "$bad" -eq 0 ]; then
    echo "✅ 全部通过（$(wc -l < /tmp/.loc_keys.txt) 个键 / $(ls "$LOC"/*.csv | wc -l) 个语言文件）"
    exit 0
fi
echo "❌ $bad 项问题"
exit 1
