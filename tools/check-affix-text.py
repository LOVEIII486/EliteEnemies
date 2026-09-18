#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
核对「词条文案」三方是否一致：**总览页 ↔ 游戏内本地化 ↔ 代码**。

    workshop/affix-overview.bbcode.txt  （**权威来源**：生成物，收录全部词条）
    workshop/description/*.md            （简介页：每组只列代表作 + 数量）
    localization/*.csv                   （玩家在游戏里看到的）
    src/Affixes/EliteAffixes.cs          （事实来源：稀有度、内联兜底文本）

⚠ **全量清单在总览，不在简介页。** 简介页曾经承载全量清单，后来因为 Steam 简介
有字数上限、词条又涨到 53 个，清单整体迁去了总览页（`tools\gen-affix-overview.py`
生成的那份），简介页改成每组列几个代表作。所以 A/B/C 三节读的是**总览**——
若仍读简介页，会以为九成词条"不见了"。

**为什么要有这个检查**：同一个词条的「名称 / 稀有度 / 数值描述」散落在四个地方——
总览页、四个语言 CSV、`EliteAffixes.cs` 的内联兜底、以及行为代码里的常量——
而**没有任何机制保证它们同步**。调一次平衡数值，要手工改 4 个语言文件加一页总览，
漏掉哪一个都不会报错：游戏照跑，只是玩家看到的说明和实际效果对不上。

实测过一次全量核对，50 条词条里 **20 条**对不上，其中同一个错值最多在 4 个语言
文件里**同步错着**（例如「不死：无敌 2 秒」，实际是 2.5 秒，四个语言全写 2）。
所以这个脚本查的不是「有没有错别字」，而是「文案有没有跟着代码走」。

## 门 vs 报告

| 节 | 查什么 | 是否影响退出码 |
|---|---|---|
| A | 稀有度：代码 `AffixRarity` vs 总览页的分组 | **门** |
| B | 英文名：游戏内英文名 vs 总览页括号里的 | **门** |
| C | 中文名：代码兜底 vs 游戏内 vs 总览页 | **门** |
| D | 代码内联兜底文本 vs CSV（本地化加载失败时才显示，但不能留错的备份） | **门** |
| E | 总览页 vs 游戏内的**措辞**差异 | 报告（`-v`） |
| F | 简介页代表作：名字必须真实存在、数量必须与代码一致 | **门** |

E 只报告不判错：两边定位不同（总览页是资料、游戏内是效果说明），
措辞不同是**正常的**，只有人工看才能判断是不是语义冲突。做成门的话
每次都是几十条噪音，噪音久了就没人看了。

F 是后加的：简介页那五行「【稀有】17 个 — 拟态、不死、混沌、时停」里，
**数量每次增删词条都会变、名字每次改名都会失效**，而两者都不会报错。
（实测过：把 17 改成 16、把「小偷」改成「神偷」，两条都当场报出来了。）

## 它查不到什么（别指望它）

**描述里的数字与实际行为是否相符**——那要读行为代码，机器判不了。
本脚本只能保证「三处文案彼此一致」，不能保证「文案与代码一致」。
后者的做法是：把描述里可验证的数值（每秒回多少、减伤多少、冷却几秒）
逐个去 `src/Affixes/Behaviors/*.cs` 里核常量。**这一步必须人做。**

## 用法

    py tools/check-affix-text.py         # 只跑门
    py tools/check-affix-text.py -v      # 连 E 节的措辞差异一起列出来

退出码：0 = 门全过；1 = 有门不通过。
"""

import argparse
import csv as csvmod
import os
import re
import sys

# 判定「工坊页括号里的英文名」时用的归一化：忽略大小写与空格/连字符
# （工坊页为可读性写成 "Glass Cannon"，而代码键是 "GlassCannon"、"Multi-Shot"）
norm = lambda s: re.sub(r"[\s\-]", "", s or "").lower()

RARITY_ZH = {"Common": "普通", "Uncommon": "罕见", "Rare": "稀有",
             "Epic": "史诗", "Legendary": "传说"}

AFFIX_KEY_RE = re.compile(r'\["(\w+)"\]\s*=\s*new AffixData\s*\{(.*?)\n\s*\}', re.S)
# 词条清单有两种宿主格式，两套正则都要认：
#   · workshop\affix-overview.bbcode.txt —— **权威来源**（生成物，收录全部词条）
#        `* [b]肉盾（Tanky）[/b] — …`   `[b]【普通】 4 个[/b]`
#   · workshop\description\*.md —— Markdown（现在只列每组代表作）
#        `* **肉盾（Tanky）** — …`      `### **【普通】**`
MD_ITEM_RES = [
    re.compile(r'^\*\s+\[b\](.+?)（([\w\s\-]+)）\[/b\]\s*—\s*(.*)$'),   # bbcode 总览
    re.compile(r'^\*\s+\*\*(.+?)（([\w\s\-]+)）\*\*\s*—\s*(.*)$'),      # markdown
]
MD_GROUP_RES = [
    re.compile(r'^\[b\]【(.+?)】.*\[/b\]$'),                            # bbcode 总览
    re.compile(r'^###\s+\*\*【(.+?)】\*\*'),                            # markdown
]

# 简介页（代表作清单）：`* **【普通】4 个** — 肉盾、狂暴、…` / `* **【Common】4** — Tanky, …`
CONDENSED_RE = re.compile(r'^\*\s+\*\*【(.+?)】\s*(\d+)\s*个?\*\*\s*—\s*(.+)$')


def load_csv(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return {r["key"]: r["value"] for r in csvmod.DictReader(f)}


def parse_code(path):
    """从 EliteAffixes.cs 取出每个词条的键、名称键/兜底、描述键/兜底、稀有度。"""
    src = open(path, encoding="utf-8-sig").read()
    out = {}
    for m in AFFIX_KEY_RE.finditer(src):
        key, body = m.group(1), m.group(2)
        # 第二个参数（内联兜底文本）是可选的：有 5 条词条的 Name 只给了键。
        # 正则里那个 `(?:...)?` 不能省——早先漏了它，于是这 5 条被静默跳过。
        nm = re.search(r'Name\s*=\s*new LocalizedText\("([^"]+)"(?:\s*,\s*"([^"]*)")?\)', body)
        dm = re.search(r'Description\s*=\s*new LocalizedText\("([^"]+)"(?:\s*,\s*"((?:[^"\\]|\\.)*)")?\)', body, re.S)
        rm = re.search(r"Rarity\s*=\s*AffixRarity\.(\w+)", body)
        out[key] = {
            "line": src[: m.start()].count("\n") + 1,
            "name_key": nm.group(1) if nm else None,
            "name_zh": nm.group(2) if nm else None,
            "desc_key": dm.group(1) if dm else None,
            "desc_zh": dm.group(2) if dm and dm.group(2) is not None else None,
            "rarity": rm.group(1) if rm else None,
        }
    return out


def parse_md(path, lang_col):
    """工坊页：{代码键: {rarity, name_zh, desc, line}}。

    `lang_col` 用来把工坊页括号里的英文名映射回代码键——**不能直接当键用**，
    因为工坊页写的是游戏内显示名（"Frigid" 而代码键是 "Frozen"，
    "Multi-Shot" 而代码键是 "MultiShot"）。
    """
    md = {}
    cur = None
    for i, l in enumerate(open(path, encoding="utf-8").read().split("\n"), 1):
        g = next((m for m in (r.match(l) for r in MD_GROUP_RES) if m), None)
        if g:
            cur = g.group(1)
            continue
        b = next((m for m in (r.match(l) for r in MD_ITEM_RES) if m), None)
        if b and cur:
            md[b.group(2).strip()] = {"rarity": cur, "name_zh": b.group(1),
                                      "desc": b.group(3).strip(), "line": i}
    # 英文显示名 → 代码键
    en2key = {norm(v): k for k, v in lang_col.items() if v}
    return {en2key.get(norm(eng), eng): d for eng, d in md.items()}


def main():
    ap = argparse.ArgumentParser(description="核对词条文案三方一致性")
    ap.add_argument("-v", "--verbose", action="store_true",
                    help="额外列出工坊页与游戏内的措辞差异（E 节，不影响退出码）")
    args = ap.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    # ⚠ 词条清单的**权威来源是总览**（生成物，收录全部词条）。
    #   description 页现在只列每组代表作，不再承载全量清单——拿它做三方核对会漏一大片。
    p_md = os.path.join(root, "workshop", "affix-overview.bbcode.txt")
    p_code = os.path.join(root, "src", "Affixes", "EliteAffixes.cs")
    p_csv = os.path.join(root, "localization", "ChineseSimplified.csv")
    p_en = os.path.join(root, "localization", "English.csv")

    for p in (p_md, p_code, p_csv, p_en):
        if not os.path.exists(p):
            print("找不到 %s" % p)
            return 1

    zh_csv = load_csv(p_csv)
    en_csv = load_csv(p_en)
    code = parse_code(p_code)
    md = parse_md(p_md, {k: en_csv.get(v["name_key"]) for k, v in code.items()})

    bad = 0

    def fail(msg):
        nonlocal bad
        bad += 1
        print(msg)

    print("=" * 78)
    print("代码 %d 条 / 总览 %d 条 / 中文 CSV %d 条" % (len(code), len(md), len(zh_csv)))
    print("=" * 78)

    # ── A 稀有度 ────────────────────────────────────────────────
    print("\n【A】稀有度：代码 AffixRarity  vs  工坊页分组")
    n = 0
    for k in sorted(code, key=lambda x: (code[x]["rarity"] or "", x)):
        want = RARITY_ZH.get(code[k]["rarity"], code[k]["rarity"])
        got = md.get(k)
        if got is None:
            fail("  X %-16s 代码=【%s】  工坊页里找不到该词条" % (k, want)); n += 1
        elif got["rarity"] != want:
            fail("  X %-16s 代码=【%s】  工坊页=【%s】  (总览:%d)"
                 % (k, want, got["rarity"], got["line"])); n += 1
    if not n:
        print("  OK 全部一致")

    # ── B 英文名 ────────────────────────────────────────────────
    print("\n【B】英文名：游戏内英文名  vs  工坊页括号里的")
    n = 0
    for k in sorted(set(code) | set(md)):
        if k not in code:
            fail("  X 工坊页写了 '%s'，代码词条表里没有这个键  (总览:%d)"
                 % (k, md[k]["line"])); n += 1
        elif k not in md:
            want = en_csv.get(code[k]["name_key"], "(CSV 里也没有)")
            fail("  X %-16s 代码里有，工坊页里找不到（游戏内英文名 %s）" % (k, want)); n += 1
    if not n:
        print("  OK 全部一致")

    # ── C 中文名 ────────────────────────────────────────────────
    print("\n【C】中文名：代码兜底  vs  游戏内  vs  工坊页")
    n = 0
    for k in sorted(code):
        trio = (code[k]["name_zh"], zh_csv.get(code[k]["name_key"]),
                (md.get(k) or {}).get("name_zh"))
        if len({x for x in trio if x is not None}) > 1:
            fail("  X %-16s 代码=%-8s 游戏内=%-8s 工坊页=%s" % (k, *trio)); n += 1
    if not n:
        print("  OK 全部一致")

    # ── D 代码内联兜底 vs CSV ───────────────────────────────────
    print("\n【D】代码内联兜底描述  vs  CSV（游戏内以 CSV 为准）")
    n = 0
    for k in sorted(code):
        if code[k]["desc_zh"] is None:
            continue  # 该条只给键、没有内联兜底
        v = zh_csv.get(code[k]["desc_key"])
        if v != code[k]["desc_zh"]:
            fail("  X %-16s (EliteAffixes.cs:%d)\n        代码兜底: %s\n        CSV     : %s"
                 % (k, code[k]["line"], code[k]["desc_zh"], v)); n += 1
    if not n:
        print("  OK 全部一致")

    # ── E 措辞差异（报告）────────────────────────────────────────
    if args.verbose:
        print("\n【E】工坊页 vs 游戏内的措辞差异（报告，不影响退出码）")
        print("     两侧定位不同，措辞不同属正常；要人看的是**语义冲突**那几条")
        n = 0
        for k in sorted(code):
            m, v = md.get(k), zh_csv.get(code[k]["desc_key"])
            if not m or v is None or m["desc"] == v:
                continue
            n += 1
            print("  · %-16s\n      工坊页: %s\n      游戏内: %s" % (k, m["desc"], v))
        print("  （共 %d 条措辞不同）" % n)

    # ── F 简介页的代表作清单 ─────────────────────────────────────
    #
    # description 页现在每组只列几个代表作 + 一个数量。这两样都会腐烂：
    #   · 数量在**每次新增/删除词条**时都要改——忘了改没人会发现；
    #   · 词条改名后，代表作里会挂着一个不存在的名字。
    # 所以对它的要求只有两条：名字真实存在、数量与代码一致。
    print("\n【F】简介页代表作：名字必须真实存在，数量必须与代码一致")
    n = 0
    for lang, names in (("zh.md", {code[k]["name_key"]: zh_csv.get(code[k]["name_key"]) for k in code}),
                        ("en.md", {code[k]["name_key"]: en_csv.get(code[k]["name_key"]) for k in code})):
        valid = {v for v in names.values() if v}
        p_page = os.path.join(root, "workshop", "description", lang)
        if not os.path.exists(p_page):
            fail("  X 找不到 %s" % p_page); n += 1
            continue

        text = open(p_page, encoding="utf-8").read()
        seen_groups = 0
        for line in text.split("\n"):
            m = CONDENSED_RE.match(line)
            if not m:
                continue
            seen_groups += 1
            label, count, listed = m.group(1).strip(), int(m.group(2)), m.group(3)

            # 数量：拿这个稀有度在代码里的真实条数对（两种语言的分组名都要认）
            want = sum(1 for k in code if RARITY_ZH.get(code[k]["rarity"]) == label
                       or code[k]["rarity"] == label)
            if want != count:
                fail("  X %s 【%s】写着 %d 个，代码里是 %d 个" % (lang, label, count, want)); n += 1

            # 名字：中英用相同的分隔符
            sep = '、' if lang == 'zh.md' else ','
            for name in (x.strip() for x in listed.split(sep)):
                if name and name not in valid:
                    fail("  X %s 【%s】里的 '%s' 不是任何词条的名字（改名了？）"
                         % (lang, label, name)); n += 1

        if seen_groups == 0:
            fail("  X %s 里一条代表作都没解析到——页面格式变了？" % lang); n += 1

    if not n:
        print("  OK 全部一致")

    print()
    if bad:
        print("❌ 门未通过：%d 处不一致。文案与代码已经漂移，修完再提交。" % bad)
        return 1
    print("✅ 门全过（A/B/C/D/F）。")
    print("   ⚠ 本脚本只保证「三处文案彼此一致」，**不保证文案与代码行为一致**——")
    print("     描述里的数字要去 src/Affixes/Behaviors/*.cs 里人工核。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
