#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把「字段里直接存 GetText 结果」改写成「字段里存键」。

    Name = LocalizationManager.GetText("K", "兜底"),
    ↓
    Name = new LocalizedText("K", "兜底"),

**为什么必须脚本化**：`src/Affixes/EliteAffixes.cs` 里有 100 处这种赋值。
手改一遍容易漏；而漏掉的那一处**不报错、不抛异常**，只是那个词条从此不跟随
语言切换——正是本工程最忌讳的「静默失效」。

**为什么用正则跨行匹配而不是逐行替换**：实际存在**跨行**写法，
    Description =
        LocalizationManager.GetText("..."),
逐行替换会漏掉它。`\\s*` 里含换行，两种形态一并覆盖。

**为什么带预期条数断言**：每条规则的命中数都写死。数量对不上就**报错退出、不写文件**。
这样「将来又冒出第三种写法」会当场暴露，而不是静默漏改几处——
AGENT.md §3.2 那条「检测命令要覆盖同一个问题的全部写法」的教训。

**可复跑**：第二次运行时正则已匹配不到任何东西，脚本报告 0 处并原样退出。

用法（Windows 上 `python` 可能只是应用商店别名，用 `py` 启动器）：
    py tools/apply-localized-text.py            # 改写
    py tools/apply-localized-text.py --dry-run  # 只报告会改哪些处
"""

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


class Rule:
    """一条改写规则：某文件里、匹配 pattern 的地方，换成 replacement。

    `expected` 是**必须**命中的条数。设为 None 表示不校验（改过之后再跑就是 0）。
    """

    def __init__(self, rel_path, pattern, replacement, expected, note):
        self.rel_path = rel_path
        self.pattern = re.compile(pattern)
        self.replacement = replacement
        self.expected = expected
        self.note = note


RULES = [
    Rule(
        "src/Affixes/EliteAffixes.cs",
        # `\b` 防止匹配到 `CustomName =` / `DisplayDescription =` 这类词尾。
        # `\s*` 含换行，覆盖跨行写法。
        r"(?P<prefix>\b(?:Name|Description)\s*=\s*)LocalizationManager\.GetText\(",
        r"\g<prefix>new LocalizedText(",
        expected=100,  # = 50 个词条 × (Name + Description)
        note="词条表：Name/Description 赋值",
    ),
    Rule(
        "src/Combos/EliteComboRegistry.cs",
        # 形态不同：这里是 EliteComboDefinition 构造函数的实参，不是 `X =` 赋值。
        r"LocalizationManager\.GetText\((?P<args>\"EliteEnemies_Combo_[A-Za-z]+_Name\", \"[^\"]*\")\)",
        r"new LocalizedText(\g<args>)",
        expected=8,  # = 8 个 combo
        note="combo 表：构造函数的名字实参",
    ),
]


def process(rule, dry_run):
    path = REPO_ROOT / rule.rel_path
    if not path.is_file():
        print(f"  !! 找不到 {rule.rel_path}", file=sys.stderr)
        return None

    original = path.read_text(encoding="utf-8")
    new, count = rule.pattern.subn(rule.replacement, original)

    if count == 0:
        # 0 处只有两种正当情形：已经改过（幂等重跑），或规则写错了。
        print(f"  {rule.rel_path}: 0 处（已改过，或写法变了——若是后者请检查规则）")
        return 0

    if rule.expected is not None and count != rule.expected:
        print(
            f"  !! {rule.rel_path}: 命中 {count} 处，但预期 {rule.expected} 处"
            f"（{rule.note}）。\n"
            f"     多半是代码里又出现了新的写法。**已中止，未写入任何文件。**",
            file=sys.stderr,
        )
        return None

    if dry_run:
        print(f"  {rule.rel_path}: 会改 {count} 处（{rule.note}）")
        return count

    path.write_text(new, encoding="utf-8")
    print(f"  {rule.rel_path}: 已改 {count} 处（{rule.note}）")
    return count


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    opts = ap.parse_args()

    print("改写为 LocalizedText：" + ("（演练）" if opts.dry_run else ""))

    # 先全部校验、再全部写盘：任一条不符预期就整体中止，
    # 避免「改了一半」这种既不是旧状态也不是新状态的中间态。
    results = [(rule, process(rule, opts.dry_run)) for rule in RULES]

    if any(r is None for _, r in results):
        print("已中止，未写入任何文件。", file=sys.stderr)
        return 1

    print(f"合计 {sum(r for _, r in results)} 处。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
