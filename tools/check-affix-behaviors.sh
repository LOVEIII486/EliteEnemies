#!/usr/bin/env bash
# 校验 src/Affixes/Behaviors/ 里"词条效果实现"的质量。
#
# 判据来自 docs\词条模块审查与设计.md §6（下一阶段的评审标准）：
#   ① 不重复反射 —— 反射 API 又**没有** A 类理由注释 → 门（报错）
#   ② 不重复花大成本找对象 —— FindObjects* / Resources.* → 门（报错，应为空）
#   ③ 有官方接口就别自造 —— GetComponent 调用点清单 + 迁移进度 → **报告**（不影响退出码）
#
# 为什么 ③ 只报告不判错：评审是一个词条一个词条做的，中途必然还有没迁完的。
# 把它做成报告，脚本就能当"这一阶段的清单"用；做成门的话每一步都会被自己的进度卡住。
#
# 用法：bash tools/check-affix-behaviors.sh [仓库根目录]
# 退出码：0 = 门全过；1 = 门有问题（报告项不影响）
#
# ⚠ 两个门都按本工程的规矩做过**故障注入**验证（见提交信息），不是"跑通就算数"。

set -u

ROOT="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
BEH="$ROOT/src/Affixes/Behaviors"

bad=0
fail() { echo "  ❌ $1"; bad=$((bad + 1)); }

if [ ! -d "$BEH" ]; then echo "找不到 $BEH"; exit 1; fi

# 反射 API 的判据：与 AGENT.md §3.2 那条命令同源（覆盖全部写法：直接取成员、
# Harmony 的 AccessTools 系列、以及从运行期 Type 取构造器）。
#
# ⚠ 这里写的是 `AccessTools\.`（整个 API 面），不是只列 Field/Method/Property——
#   同一条教训：换个入口（`AccessTools.TypeByName("…")`）也是同一类静默失效，
#   而只列举"你最先想到的那几个"必然漏。行为目录里出现任何 AccessTools 都值得评审。
REFLECT_RE='GetField\(|GetMethod\(|GetProperty\(|SetValue\(|BindingFlags|Activator\.|Type\.GetType\(|AccessTools\.'

# 注释不算代码：把整行注释滤掉，否则"注释里提到旧写法"会被当成违规——
# 本工程正因为这类假阳性吃过亏（告警一旦长期是噪音，真问题就没人看了）。
# 判定：行首（可带空白）为 `//` 的行整行跳过；行尾注释不动（那是代码行）。
strip_line_comments() { grep -vE '^[^:]+:[0-9]+:[[:space:]]*//|^[0-9]+:[[:space:]]*//' || true; }

# A 类理由注释的判据：出现这几个字样之一即认为作者写明了依据。
# （写法约定见 AGENT.md §3.7——"规范允许的例外要写明属于 03 §2.3 的哪一类"。）
MARKER_RE='03 篇|03-编码规范|A2'

# ── 1. 反射必须带 A 类理由 ──
echo "── 1. 行为里的反射有没有写明 A 类理由 ──"
for f in "$BEH"/*.cs; do
    hits=$(grep -nE "$REFLECT_RE" "$f" | strip_line_comments)
    [ -z "$hits" ] && continue

    if ! grep -qE "$MARKER_RE" "$f"; then
        fail "$(basename "$f") 用了反射但**没有**写明 A 类理由（03 §2.3）：
$hits"
    fi
done

# ── 2. 高成本查找必须为空 ──
#    依据：这些调用会遍历场景/全部资源，绝不能出现在每次生成/每帧/每次受击的路径上。
#    需要引用请用 AffixContext（框架已按敌人解析一次）。
echo "── 2. 行为里有没有 FindObjects* / Resources.* ──"
hits=$(grep -rnE 'FindObjectsOfType|FindObjectsByType|FindFirstObject|FindAnyObject|Resources\.(Find|Load)' "$BEH" --include=*.cs | strip_line_comments)
[ -n "$hits" ] && fail "行为里出现了高成本查找（应改用 AffixContext 或缓存）："$'\n'"$hits"

# ── 3. 报告：GetComponent 调用点（评审时逐个对照） ──
echo "── 3. 各行为的 GetComponent 调用点（报告，供逐个评审） ──"
total=0
for f in "$BEH"/*.cs; do
    out=$(awk -v file="$(basename "$f")" '
        /^[[:space:]]*\/\// { next }
        /^[[:space:]]*(public|private|protected|internal)[A-Za-z_ <>\[\],]*\(/ { m=$0; sub(/^[[:space:]]+/, "", m); sub(/\(.*/, "", m) }
        /GetComponent/ {
            line=$0; sub(/^[[:space:]]+/, "", line)
            printf("  %s:%d  [%s]  %s\n", file, FNR, m, line)
        }' "$f")
    if [ -n "$out" ]; then
        echo "$out"
        total=$((total + $(printf '%s\n' "$out" | wc -l)))
    fi
done
echo "  —— 合计 $total 处（数字会随评审推进下降；本项目不把它写进文档当常数）"

# ── 4. 报告：按类型名分支（同类静默失效风险，尚未定性） ──
echo "── 4. 按类型名/类名字符串分支（报告） ──"
typehits=$(grep -rnE 'GetType\(\)\.Name|\.GetType\(\) ==' "$BEH" --include=*.cs | strip_line_comments)
if [ -n "$typehits" ]; then echo "$typehits"; else echo "  （无）"; fi

# ── 5. 报告：迁移进度 ──
echo "── 5. 迁移进度：用上框架提供的两样东西的行为 ──"
echo "  Ctx（一次解析的上下文）:        $(grep -rl '\bCtx\b' "$BEH" --include=*.cs 2>/dev/null | wc -l) 个文件"
echo "  StartManagedCoroutine（托管协程）: $(grep -rl 'StartManagedCoroutine' "$BEH" --include=*.cs 2>/dev/null | wc -l) 个文件"
echo "  自行挑协程宿主（待迁移）:        $(grep -rlE 'ModBehaviour\.Instance\?\.StartCoroutine|\.StartCoroutine\(' "$BEH" --include=*.cs 2>/dev/null | wc -l) 个文件"

echo
if [ "$bad" -eq 0 ]; then
    echo "✅ 门全过（报告项见上）"
    exit 0
else
    echo "❌ $bad 项问题"
    exit 1
fi
