#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
从模组代码生成《精英敌人词缀总览》——一篇独立的 Steam 指南（BBCode）。

    py tools/gen-affix-overview.py

产物：`workshop\affix-overview.bbcode.txt`（**纳入版本控制**——见下）

## 为什么产物要提交进仓库，而不是丢在 artifacts\

它是**贴到 Steam 指南区的一篇独立文档**，不是构建中间物。放在 `artifacts\`（gitignore）
的话：改了数值 → 重新生成了 → **git 里看不出发生了什么**，玩家看到的版本与仓库里
哪一版对不上也无从追溯。放进 `workshop\` 之后，数值一变 `git diff` 就是文档的改动，
改了什么一目了然，也随时能翻回上一版。

⚠ 因此它是**构建的产物同时也是版本控制的文件**：每次构建都会重写，
**没有变化时内容逐字节相同**（所以不会平白多出 diff）。

## 为什么是生成器而不是手写文档

词缀的属性倍率、稀有度、掉落都写在 `src\Affixes\EliteAffixes.cs` 里，**它们是会变的**。
手抄一份文档，改数值时必然漏掉它——而漏掉的那一处**不报错、不崩**，只是玩家看到的
资料与游戏对不上。这份文档是要公开给玩家做平衡讨论用的，数字错了比没有更糟。

## 数据来源（每条都可追溯）

| 内容 | 来源 |
|---|---|
| 中文名 / 稀有度 / 三个乘数 | `EliteAffixes.cs` 的 `AffixData` |
| 掉落物品名 | `..\\Docs\\ItemDatabase原版.xlsx`（ID → 显示名） |
| 掉落标签名 | 同一张表的 `TagsZH` 列 |
| 英文名（词条说明里的括号） | `localization\\English.csv` |
| **效果说明的措辞** | **`workshop\\affix-notes.txt`（手写）** |
| 版本号 | `EliteEnemies.csproj` 的 `<Version>`（全工程唯一来源） |

⚠ **只有效果说明是手写的**，其余全部来自代码/资产。手写意味着它会漂移，
所以 `load_notes()` 会校验"词条表与说明文件**一一对应**"并在不一致时**中止**——
能挡的是 key 拼错、词条增删忘了同步；**措辞有没有跟上代码的行为改动，机器判不了**。

## ⚠ 两个必须知道的口径

1. **注释不算代码。** `EliteAffixes.cs` 里有被注释掉的掉落条目（如时停的 ID 385），
   逐行解析时**必须滤掉 `//`**——否则会把没生效的东西写进公开文档。
   （这不是假设：第一版就是这么错的，靠核对才发现。）
2. **三个词条的倍率不在表里**（巨大化 / 迷你 / 史莱姆），它们由行为脚本动态计算。
   `DYNAMIC` 里写的是从各自 `Behavior.cs` 读出的常量，**行为改了要跟着改**——
   生成器会在表格里把它们标成「动态」，提示读者去看备注。
r"""

import csv
import io
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(os.path.dirname(ROOT), "Docs")
ITEM_DB = os.path.join(DOCS, "ItemDatabase原版.xlsx")

RARITY_ZH = {"Common": "普通", "Uncommon": "罕见", "Rare": "稀有",
             "Epic": "史诗", "Legendary": "传说"}

# 游戏里这些标签的中文名对玩家有误导，或干脆没翻译，这里给个能读的写法。
# ⚠ 出现**没有覆盖**的新标签时，生成器会打警告并原样输出——不会静默。
TAG_LABEL = {
    "Bullet": "弹药",       # 游戏内 TagsZH 写的是「武器」，但挂它标签的全是弹
    "JLab": "实验室物品",    # 游戏内没翻译，TagsZH 字面就是 *Tag_JLab*
    "MiniGame": "小游戏物品",  # 同上，*Tag_MiniGame*
}

# 表里三个乘数不反映实际值的词条——它们的倍率在行为脚本里算。
DYNAMIC = {
    "Giant": "动态（体型/生命 ×1.5–3.5，与史莱姆同场时上限 2.8；移速 ×0.8）",
    "Mini": "动态（体型 ×0.4–0.8，生命 ×0.6–0.9；移速 ×1.2）",
    "Slime": "动态（初始体型/生命 ×3.5，随血量缩小并增伤 ×0.65→1.5）",
}


def load_item_db():
    """物品库：ID → (显示名, 该物品各标签的中文名)。"""
    ns = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
    z = zipfile.ZipFile(ITEM_DB)
    shared = [''.join(t.text or '' for t in si.iter(ns + 't'))
              for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall(ns + 'si')]

    rows = []
    for row in ET.fromstring(z.read('xl/worksheets/sheet1.xml')).iter(ns + 'row'):
        cells = {}
        for c in row.findall(ns + 'c'):
            col = re.match(r'[A-Z]+', c.get('r')).group(0)
            v = c.find(ns + 'v')
            if v is None:
                continue
            cells[col] = shared[int(v.text)] if c.get('t') == 's' else v.text
        if cells:
            rows.append(cells)

    head = {name: col for col, name in rows[0].items()}
    names, tag_zh = {}, {}
    for r in rows[1:]:
        iid = r.get(head['ID'])
        if not iid or not iid.isdigit():
            continue
        names[int(iid)] = r.get(head['DisplayName'], '')
        ens = (r.get(head['TagsEN']) or '').split(';')
        zhs = (r.get(head['TagsZH']) or '').split(';')
        for i, en in enumerate(ens):
            if en and i < len(zhs) and en not in tag_zh:
                tag_zh[en] = zhs[i]
    return names, tag_zh


def strip_comment(line):
    """返回 (去掉注释的代码, 行尾注释文本)。`//` 一开始就切。"""
    idx = line.find('//')
    if idx < 0:
        return line, ''
    return line[:idx], line[idx + 2:].strip()


def parse_affixes(names):
    """逐行解析 EliteAffixes.cs。返回 [{key,name,rarity,hp,dmg,spd,loot[]}]，保持文件顺序。"""
    path = os.path.join(ROOT, r"src\Affixes\EliteAffixes.cs")
    lines = open(path, encoding='utf-8-sig').read().split('\n')

    csv_zh = {r['key']: r['value'] for r in csv.DictReader(
        open(os.path.join(ROOT, r'localization\ChineseSimplified.csv'),
             encoding='utf-8-sig', newline=''))}

    out, cur = [], None
    warned_tags = []

    for raw in lines:
        code, comment = strip_comment(raw)

        m = re.search(r'\["(\w+)"\]\s*=\s*new AffixData', code)
        if m:
            cur = {"key": m.group(1), "loot": []}
            out.append(cur)
            continue
        if cur is None:
            continue

        for pat, field in ((r'Name\s*=\s*new LocalizedText\("([^"]+)"', "namekey"),
                           (r'Rarity\s*=\s*AffixRarity\.(\w+)', "rarity"),
                           (r'HealthMultiplier\s*=\s*([0-9.]+)f', "hp"),
                           (r'DamageMultiplier\s*=\s*([0-9.]+)f', "dmg"),
                           (r'MoveSpeedMultiplier\s*=\s*([0-9.]+)f', "spd")):
            mm = re.search(pat, code)
            if mm:
                cur[field] = mm.group(1)

        # ── 固定掉落 ──（注释已被剥掉，所以被注释掉的条目不会进来）
        mm = re.search(r'new LootEntry\((\d+),', code)
        if mm:
            iid = int(mm.group(1))
            label = names.get(iid) or comment or ('ID %d' % iid)
            if not names.get(iid):
                warned_tags.append('掉落 ID %d 不在物品库里，用了代码注释：%s' % (iid, label))
            cur["loot"].append(label)

        # ── 按标签随机 ──
        mm = re.search(r'WithRandomLoot(?:Range)?\(', code)
        if mm:
            cur.setdefault("pending", True)

        tm = re.search(r'tagNames:\s*new\[\]\s*\{([^}]*)\}', code)
        if tm and cur.get("pending"):
            for t in re.findall(r'"(\w+)"', tm.group(1)):
                label = TAG_LABEL.get(t) or tag_for(t)
                if label is None:
                    warned_tags.append('标签 "%s" 没有中文名也没有覆盖，已原样输出' % t)
                    label = t
                cur["loot"].append('随机：' + label)
            cur.pop("pending", None)

    for a in out:
        a["name"] = csv_zh.get(a.get("namekey", ""), a["key"])

    # 标签中文名的解析器要能拿到 tag_zh，用闭包传进来
    return out, warned_tags


def load_notes(affixes):
    """读 `workshop\\affix-notes.txt`（**手写**），返回 {词条key: 说明}。

    ⚠ 措辞是作者手写的，生成器判不了"它有没有跟上代码"——那是人工核对的事。
       能查的是**结构性**的不一致，而且查不到就**中止**，不静默：

         · key 拼错 / 词条已被删 → "文件里有、代码里没有"
         · 新增词条忘了写说明   → "代码里有、文件里漏了"
         · 同一个 key 写了两遍

       这三条恰好是手写文件最容易出的错，而且都是**静默**的（少一条说明，
       总览里就少一个词条，没人会发现）。
    """
    path = os.path.join(ROOT, r"workshop\affix-notes.txt")
    notes, dup = {}, []
    for lineno, raw in enumerate(open(path, encoding='utf-8-sig').read().split('\n'), 1):
        line = raw.strip()
        if not line or line.startswith('#'):
            continue
        if '=' not in line:
            raise SystemExit("✗ affix-notes.txt:%d 不是 「key = 说明」的形式：%s" % (lineno, raw))
        k, v = line.split('=', 1)
        k, v = k.strip(), v.strip()
        if k in notes:
            dup.append(k)
        notes[k] = v

    keys = [a["key"] for a in affixes]
    problems = []
    if dup:
        problems.append("重复的 key：%s" % "、".join(sorted(set(dup))))
    unknown = sorted(k for k in notes if k not in keys)
    missing = sorted(k for k in keys if k not in notes)
    if unknown:
        problems.append("affix-notes.txt 里有、代码里没有（key 拼错或词条已删）：%s" % "、".join(unknown))
    if missing:
        problems.append("代码里有、affix-notes.txt 里漏了（新增词条忘了写说明）：%s" % "、".join(missing))
    if problems:
        raise SystemExit("✗ affix-notes.txt 与词条表对不上：\n  - " + "\n  - ".join(problems))

    return notes


def load_en_names():
    """读 `localization\\English.csv`，返回 {本地化key: 值}。总览里要在中文名后标英文名。"""
    path = os.path.join(ROOT, r'localization\English.csv')
    return {r['key']: r['value'] for r in csv.DictReader(open(path, encoding='utf-8-sig'))}


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    names, tag_zh = load_item_db()
    global tag_for
    tag_for = lambda t: tag_zh.get(t)

    affixes, warned = parse_affixes(names)

    version = re.search(r'<Version>([^<]+)</Version>',
                        open(os.path.join(ROOT, "EliteEnemies.csproj"), encoding='utf-8-sig').read())
    version = version.group(1).strip() if version else '?'

    def cell(a, field):
        if a["key"] in DYNAMIC:
            return "动态"
        return a.get(field, '?')

    def loot_text(a):
        if not a["loot"]:
            return "—"
        seen, parts = set(), []
        for x in a["loot"]:
            if x in seen:
                continue
            seen.add(x)
            parts.append(x)
        if len(parts) > 4:
            return "、".join(parts[:3]) + " 等 %d 项" % len(parts)
        return "、".join(parts)

    notes = load_notes(affixes)
    en_names = load_en_names()

    body = ["[h1]⚔️ 精英敌人词缀总览（v%s）[/h1]" % version, "",
            "[i]本帖整理 v%s 的全部 %d 个精英词缀：效果说明、属性倍率、稀有度与掉落。[/i]" % (version, len(affixes)),
            "[i]表中标注「动态」的倍率由行为脚本实时计算，见文末备注。[/i]",
            ""]

    # 按稀有度分组显示。⚠ 词条表（`EliteAffixes.Pool`）的字典顺序是**交错的**
    # ——普通/罕见/稀有/罕见/稀有/史诗… 直接照抄会让读者以为分组坏了。
    # 组内保持文件原序（那才是作者摆放的顺序）。
    rank = {"Common": 0, "Uncommon": 1, "Rare": 2, "Epic": 3, "Legendary": 4}
    affixes = sorted(affixes, key=lambda a: rank.get(a.get("rarity", ""), 9))

    # ── 第一节：词条说明（效果文案，来自手写的 affix-notes.txt）──
    #
    # ⚠ 与下面那张表**刻意分成两节**：说明是散文（要读的），表是数据（要查的）。
    #   挤进同一张表会把每格撑得很长，两边都难用。
    body += ["[hr][/hr]", "", "[h2]📜 词条说明[/h2]", ""]

    last_rarity = None
    for a in affixes:
        rar = RARITY_ZH.get(a.get("rarity", ""), a.get("rarity", "?"))
        if rar != last_rarity:
            if last_rarity is not None:
                body.append("")
            in_group = sum(1 for x in affixes if RARITY_ZH.get(x.get("rarity", ""), "?") == rar)
            body.append("[b]【%s】 %d 个[/b]" % (rar, in_group))
            last_rarity = rar

        en = en_names.get("EliteEnemies_Affix_%s_Name" % a["key"], "")
        label = "%s（%s）" % (a["name"], en) if en else a["name"]
        body.append("* [b]%s[/b] — %s" % (label, notes[a["key"]]))

    # ── 第二节：属性与掉落一览（从代码生成）──
    body += ["", "[hr][/hr]", "", "[h2]📊 词缀属性与掉落一览[/h2]", "",
             "[table]",
             "[tr][th]词缀[/th][th]稀有度[/th][th]生命×[/th][th]伤害×[/th][th]移速×[/th][th]掉落[/th][/tr]"]

    last_rarity = None
    for a in affixes:
        rar = RARITY_ZH.get(a.get("rarity", ""), a.get("rarity", "?"))
        if rar != last_rarity:
            if last_rarity is not None:
                body.append("[tr][td][/td][td][/td][td][/td][td][/td][td][/td][td][/td][/tr]")
            last_rarity = rar
        body.append("[tr][td][b]%s[/b][/td][td]%s[/td][td]%s[/td][td]%s[/td][td]%s[/td][td]%s[/td][/tr]"
                    % (a["name"], rar, cell(a, "hp"), cell(a, "dmg"), cell(a, "spd"), loot_text(a)))
    body += ["[/table]", "", "[hr][/hr]", "", "[h2]📝 备注[/h2]", "", "[i]"]

    for key, note in DYNAMIC.items():
        name = next((x["name"] for x in affixes if x["key"] == key), key)
        body.append("* [b]%s[/b] — %s" % (name, note))
    body.append("* [b]分裂[/b] — 子体是弱化分身，仅随机掉落 1 件物品。")
    body += ["[/i]", "", "[hr][/hr]", "",
             "[i]如发现词缀数值异常或掉落不合理，欢迎在评论区反馈平衡建议！[/i]", ""]

    # ⚠ 输出到 **workshop\**（纳入版本控制），不是 artifacts\ ——理由见文件头的说明。
    out_dir = os.path.join(ROOT, "workshop")
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "affix-overview.bbcode.txt")
    with open(out_path, "w", encoding='utf-8', newline='') as f:
        f.write("\n".join(body))

    print("生成 %s（%d 条词缀，%d 行）"
          % (out_path, len(affixes), len(body)))
    for w in warned:
        print("  ⚠ " + w)
    return 0


tag_for = lambda t: None   # 由 main 里赋值

if __name__ == "__main__":
    sys.exit(main())
