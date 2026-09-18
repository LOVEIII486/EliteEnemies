#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
掉落效果模拟：把 `LootItemHelper` / `EliteLootSystem` 的选择逻辑照搬成 Python，
用**从 `EliteAffixes.cs` 实抽的真实掉落配置**跑出各配置下的掉落情况。

为什么要有它：
  · 掉率是个**统计量**，看代码只能得到定性印象。想回答「这套配置实际掉什么」、
    「某个设置调到 X 会怎样」、「加大批 mod 物品会不会让品质偏好失效」，
    都必须真的算一遍。
  · 尤其是最后那个问题——它的答案取决于**代码结构**（先抽品阶还是先抽物品），
    而不是取决于具体数据。模拟能把这个结构差异直接跑出来。

⚠ 本脚本**镜像**游戏侧逻辑，不是调用它。所以：
  · 两个选择函数是逐行对照 `src/Loot/LootItemHelper.cs` 写的，改那边要同步改这里；
  · 物品池（各品阶有多少件物品）**没有真实数据**——游戏侧由
    `ItemAssetsCollection.Search` 在运行时枚举，无法离线取得。
    脚本因此把「每品阶物品数」参数化，并在 §D 特意证明：
    **本文要回答的那个问题，答案与池子大小无关。**

用法（Windows 上 `python` 可能是商店别名，用 `py`）：
    py tools/simulate-loot.py
"""

import random
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
AFFIX_SRC = REPO / "src" / "Affixes" / "EliteAffixes.cs"

# 与 GameConfig.QualityTierBiases 一致——反解出来的「等距」7 档
TIER_BIASES = [-3.13, -1.79, -1.00, -0.44, 0.0, 0.44, 1.00]

APPROX_EPS = 1e-6          # 对应 Mathf.Approximately(x, 0f)


# ======================================================================
# §0  从 EliteAffixes.cs 抽真实掉落配置
# ======================================================================

def _balanced(text, start):
    """返回从 text[start] 处的 '(' 起、配平的那一段内容（不含外层括号）。"""
    depth = 0
    for i in range(start, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return text[start + 1:i], i
    raise ValueError("括号不配平")



def _split_args(inner):
    """把实参列表切成顶层各项，并**剥掉具名实参的 `名字:` 前缀**。

    ⚠ 必须同时接受 `(1, 3, 1, 1f, ...)` 与 `(minQuality: 1, maxQuality: 3, ...)` 两种写法：
    本工程的调用点已改成具名实参，而**位置参数的正则在那之后一条都匹配不上**——
    实测模拟脚本因此从 29 条掉到 0 条。剥掉名字后按位置解释，两种写法都能读，
    且将来调换具名实参顺序也不会漏。
    逗号切分要**同时**避开圆括号与花括号（`new[] { "a", "b" }` 里有逗号）。
    """
    parts, depth, cur = [], 0, ""
    for ch in inner:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur); cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    return [re.sub(r"^\s*[A-Za-z_]\w*\s*:\s*", "", p).strip() for p in parts]


def _num(tok):
    """把 C# 数值字面量转成 Python 数。**必须剥掉可选的 `f` 后缀**——
    原先由正则顺手吃掉，改成按 token 取值后不剥就会 float("1f") 抛异常。"""
    return float(tok.strip().rstrip("fF"))


def parse_affixes():
    """返回 {词条名: {'rarity':str, 'groups':[[LootEntry...]], 'randoms':[config...]}}"""
    src = AFFIX_SRC.read_text(encoding="utf-8")

    # 按 ["X"] = new AffixData 切块
    marks = [(m.start(), m.group(1)) for m in re.finditer(r'\["([A-Za-z]+)"\]\s*=\s*new AffixData', src)]
    out = {}
    for idx, (pos, name) in enumerate(marks):
        end = marks[idx + 1][0] if idx + 1 < len(marks) else len(src)
        body = src[pos:end]

        rarity = re.search(r"Rarity\s*=\s*AffixRarity\.(\w+)", body)
        entry = {"rarity": rarity.group(1) if rarity else "Common",
                 "groups": [], "randoms": []}

        # WithLootGroup( ... )：组内多个 LootEntry
        for m in re.finditer(r"\.WithLootGroup\s*\(", body):
            inner, _ = _balanced(body, m.end() - 1)
            items = [tuple(int(x) for x in g[:3]) + (float(g[3]),)
                     for g in re.findall(r"new LootEntry\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)f?\s*\)", inner)]
            if items:
                entry["groups"].append(items)

        # 先匹配 Range（更长），再匹配不带 Range 的
        for m in re.finditer(r"\.WithRandomLootRange\s*\(", body):
            inner, _ = _balanced(body, m.end() - 1)
            args = _split_args(inner)
            if len(args) >= 4:
                tags = re.search(r"new\[\]\s*\{([^}]*)\}", inner)
                entry["randoms"].append({
                    "minQ": int(_num(args[0])), "maxQ": int(_num(args[1])),
                    "itemCount": int(_num(args[2])), "chance": _num(args[3]),
                    "tags": [t.strip().strip('"') for t in tags.group(1).split(",") if t.strip()] if tags else [],
                })

        body_no_range = re.sub(r"\.WithRandomLootRange\s*\(", "\x00R\x00(", body)
        for m in re.finditer(r"\.WithRandomLoot\s*\(", body_no_range):
            inner, _ = _balanced(body_no_range, m.end() - 1)
            args = _split_args(inner)
            if len(args) >= 3:
                tags = re.search(r"new\[\]\s*\{([^}]*)\}", inner)
                entry["randoms"].append({
                    "minQ": int(_num(args[0])), "maxQ": int(_num(args[0])),
                    "itemCount": int(_num(args[1])), "chance": _num(args[2]),
                    "tags": [t.strip().strip('"') for t in tags.group(1).split(",") if t.strip()] if tags else [],
                })

        out[name] = entry
    return out


# ======================================================================
# §1  逐行对照 LootItemHelper 的选择逻辑
# ======================================================================

def pick_quality_weighted(min_q, max_q, bias, rnd):
    """对应 LootItemHelper.PickQualityByWeight（LootItemHelper.cs:421）。"""
    if abs(bias) < APPROX_EPS:
        return rnd.randint(min_q, max_q)

    weights = []
    for q in range(min_q, max_q + 1):
        base = (8 - q) if bias < 0 else q
        if base <= 0:
            base = 1
        weights.append(base ** abs(bias))

    total = sum(weights)
    r = rnd.random() * total
    acc = 0.0
    for i, w in enumerate(weights):
        acc += w
        if r <= acc:
            return min_q + i
    return max_q


def pick_quality_from_valid(valid_qualities, bias, rnd):
    """对应 PickQualityByWeightFromValidQualities（:449）。
    注意：只按**品阶**取权重，与每个品阶里有多少件匹配物品**无关**。"""
    if not valid_qualities:
        return None
    if abs(bias) < APPROX_EPS:
        return rnd.choice(valid_qualities)

    weights = []
    for q in valid_qualities:
        base = (8 - q) if bias < 0 else q
        if base <= 0:
            base = 1
        weights.append(base ** abs(bias))

    total = sum(weights)
    r = rnd.random() * total
    acc = 0.0
    for i, w in enumerate(weights):
        acc += w
        if r <= acc:
            return valid_qualities[i]
    return valid_qualities[-1]


def pick_quality_uniform_over_items(pools, min_q, max_q, rnd):
    """对应 CreateItemWithTagsFromAllQualities（:346）——**这条路径不看 bias**：
    它把所有品阶的候选物品摊成一张平表后均匀取，于是命中概率 ∝ 该品阶的物品数。
    （按件数加权与「摊平后均匀取」等价，这里用加权实现以免造几万条的列表。）"""
    qs = [q for q in range(min_q, max_q + 1) if pools.get(q, 0) > 0]
    if not qs:
        return None
    total = sum(pools[q] for q in qs)
    r = rnd.random() * total
    acc = 0
    for q in qs:
        acc += pools[q]
        if r <= acc:
            return q
    return qs[-1]


# ======================================================================
# 物品池模型（各品阶有多少件物品）
# ======================================================================

# 仅作示意：真实数量由游戏在运行时枚举，离线取不到。
# §D 会证明：本文要回答的问题，结论与这张表无关。
BASELINE_POOLS = {1: 120, 2: 140, 3: 130, 4: 110, 5: 80, 6: 55, 7: 30}


def histogram(pools, bias, trials, rnd, min_q=1, max_q=7):
    """品阶 → 次数。物品池只用于「该品阶有没有东西」。"""
    hist = {q: 0 for q in range(1, 8)}
    for _ in range(trials):
        q = pick_quality_weighted(min_q, max_q, bias, rnd)
        if pools.get(q):
            hist[q] += 1
    return hist


def pct(hist, total=None):
    total = total or sum(hist.values())
    return {q: (v / total * 100 if total else 0.0) for q, v in hist.items()}


def avg_quality(dist):
    tot = sum(dist.values())
    return sum(q * v for q, v in dist.items()) / tot if tot else 0.0


def bar(p, width=28):
    return "█" * int(round(p / 100 * width))


# ======================================================================

def section(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def main():
    rnd = random.Random(20260917)
    trials = 300_000

    affixes = parse_affixes()
    from collections import Counter

    n_entries = sum(len(g) for a in affixes.values() for g in a["groups"])
    entry_chances = [e[3] for a in affixes.values() for g in a["groups"] for e in g]
    rand_chances = [c["chance"] for a in affixes.values() for c in a["randoms"]]

    section("§0  抽取校验（先证明抽出来的数据是对的，再拿它算）")
    print(f"  词条数                  : {len(affixes)}      （期望 50）")
    print(f"  固定掉落条目数(LootEntry): {n_entries}      （期望 67）")
    print(f"  随机掉落配置数          : {len(rand_chances)}")

    # 已知基准**只统计 LootEntry 的掉率**（早先单独数过一次），随机配置不含在内
    hist_c = Counter(round(c, 2) for c in entry_chances)
    expected = {1.0: 20, 0.8: 1, 0.7: 5, 0.6: 6, 0.5: 11, 0.4: 3,
                0.3: 2, 0.2: 5, 0.15: 1, 0.1: 8, 0.07: 2, 0.05: 3}
    same = dict(hist_c) == expected
    print(f"  固定掉落掉率直方图      : {'与已知基准一致 ✓' if same else '不一致 ✗ ' + str(dict(hist_c))}")

    # ⚠ 随机配置的条数必须也校验。**这条是踩过坑才加的**：
    #   掉率在 C# 里写作 `1f` / `1` / `0.6f` / `0.6` 都合法，最初的正则强制要求 `f` 后缀，
    #   于是 `WithRandomLoot(-1, 1, 1, ...)`（Chef）这种**整条被漏掉**，24 条里少了 5 条。
    #   更糟的是当时的「已知基准」也是用同样要求 `f` 的 grep 得出的——**基准和解析器犯了同一个错**，
    #   所以校验没拦住。现在用**不依赖后缀**的方式分别数调用点，写死在这里。
    # 注意 `\.WithRandomLoot\s*\(` 不会匹配 `WithRandomLootRange(`（后面是字母 R，不是括号），
    # 所以两个计数互不重叠、可直接相加——**不要**再从前者里减后者。
    src_text = AFFIX_SRC.read_text(encoding="utf-8")
    n_range = len(re.findall(r"\.WithRandomLootRange\s*\(", src_text))
    n_plain = len(re.findall(r"\.WithRandomLoot\s*\(", src_text))
    print(f"  随机配置 解析/实际      : {len(rand_chances)} / {n_range + n_plain}"
          f"  （Range {n_range} + 普通 {n_plain}）")
    if not (same and len(affixes) == 50 and n_entries == 67
            and len(rand_chances) == n_range + n_plain):
        print("  !! 抽取结果与已知事实不符，后续数字不可信——先修抽取。")
        return 1

    # ---------------- A ----------------
    section("§A  品质档位的实际效果（当前配置：整数档位 0–6，1–7 品阶全开）")
    print("  档位 内部bias | 平均品阶 |  Q1..Q7 分布")
    print("  -------------|---------|----------------------------------------")
    for tier, bias in enumerate(TIER_BIASES):
        h = histogram(BASELINE_POOLS, bias, trials, rnd)
        d = pct(h)
        dist = " ".join(f"{d[q]:4.1f}%" for q in range(1, 8))
        print(f"   {tier}   {bias:+5.2f}  |  {avg_quality(d):5.2f}  | {dist}")
    print("\n  提示：档位 4（bias=0）是**各品阶均等**，落在正中。")
    print("        档位 k 的平均品阶 ≈ 2.0 + 0.5×k —— 这是选档位的依据。")

    # ---------------- B ----------------
    section("§B  地图品质上限对分布的影响（上限把范围截断，形状不变、绝对值平移）")
    print("  档位 | 上限7 平均 | 上限5 平均 | 上限3 平均")
    print("  -----|-----------|-----------|----------")
    for tier, bias in enumerate(TIER_BIASES):
        row = [avg_quality(pct(histogram(BASELINE_POOLS, bias, trials // 3, rnd, 1, cap)))
               for cap in (7, 5, 3)]
        print(f"   {tier}  |   {row[0]:5.2f}   |   {row[1]:5.2f}   |   {row[2]:5.2f}")
    print("\n  注意：范围收窄后**档位之间的相对高低仍保持**，只是绝对值一起下移。")

    # ---------------- C ----------------
    section("§C  几种配置下的期望掉落（用真实词条配置算）")
    print("  倍率语义 = 判定次数；期望件数 = 倍率 × 概率 × 件数")
    print("  以下一律取 penalty = 1（非弱怪）、地图上限 7，以单看设置项本身的效果。\n")

    # —— A) 每词条的贡献 ——
    fixed_vals, rand_vals = [], []
    for a in affixes.values():
        f = 0.0
        for g in a["groups"]:
            # 组内随机取 1 个 ⇒ 该组期望 = 组内各条目 (概率 × 件数) 的平均
            f += sum(c * ((mn + mx) / 2) for (_, mn, mx, c) in g) / len(g)
        fixed_vals.append(f)
        r = 0.0
        for c in a["randoms"]:
            r += c["chance"] * c["itemCount"]      # 所有配置都用默认堆叠 1..1
        rand_vals.append(r)
    avg_fixed = sum(fixed_vals) / len(fixed_vals)
    avg_rand = sum(rand_vals) / len(rand_vals)

    print(f"  A) 每**词条**的贡献（{len(affixes)} 个词条取平均；其中 {sum(1 for v in affixes.values() if v['randoms'])} 个带随机配置）")
    print(f"     {'倍率':<6}{'固定掉落':>10}{'随机配置':>10}{'小计':>10}")
    for mult in (0.5, 1.0, 1.5, 2.0, 3.0):
        f, r = avg_fixed * mult, avg_rand * mult
        print(f"     {mult:<6.1f}{f:>10.2f}{r:>10.2f}{f + r:>10.2f}")

    # —— B) 每只精英的稀有度奖励（**每只一次**，不随词条数线性叠加）——
    rarity_score = {"Common": 1, "Uncommon": 2, "Rare": 3, "Epic": 4, "Legendary": 5}
    avg_score = sum(rarity_score[a["rarity"]] for a in affixes.values()) / len(affixes)

    def bonus_chance(power):
        return min(1.0, 0.30 + power * 0.05)

    print(f"\n  B) 每**只精英**的稀有度奖励（平均稀有度分 {avg_score:.2f}；这是**每只一次**，不是每词条）")
    print(f"     {'精英':<20}{'powerScore':>11}{'基础概率':>10}{'倍率1.0':>9}{'倍率2.0':>9}")
    elites = [("普通精英 · 1 词条", avg_score),
              ("精英 · 2 词条", avg_score * 2),
              ("精英 · 3 词条", avg_score * 3),
              ("Boss · 3 词条", 5 + avg_score * 3)]
    for label, power in elites:
        bc = bonus_chance(power)
        print(f"     {label:<20}{power:>11.1f}{bc:>10.2f}{bc * 1.0:>9.2f}{bc * 2.0:>9.2f}")

    # —— C) 典型整只精英合计 ——
    print(f"\n  C) 典型整只精英的期望掉落件数（词条贡献 × 词条数 + 稀有度奖励）")
    print(f"     {'精英':<20}{'倍率1.0':>9}{'倍率2.0':>9}{'倍率3.0':>9}")
    for label, power, n in (("普通精英 · 1 词条", avg_score, 1),
                            ("精英 · 3 词条", avg_score * 3, 3),
                            ("Boss · 3 词条", 5 + avg_score * 3, 3)):
        bc = bonus_chance(power)
        row = [n * (avg_fixed + avg_rand) * m + bc * m for m in (1.0, 2.0, 3.0)]
        print(f"     {label:<20}{row[0]:>9.2f}{row[1]:>9.2f}{row[2]:>9.2f}")
    print("\n  说明：固定掉落按「组内随机取 1 个」计；稀有度奖励只发 1 件/次判定，")
    print("        且**每只精英只判一次**——所以它不随词条数线性增长。")

    # —— C-2) 件数**分布**（期望之外：玩家实际感受到的是「掉了几件」）——
    print("\n  C-2) 件数分布（**期望不等于「每次都掉这么多」**；倍率 1.0）")
    print("       固定掉落的每次成功各自掷数量，故分布并不集中在期望附近。\n")

    rareness = {"Common": 1, "Uncommon": 2, "Rare": 3, "Epic": 4, "Legendary": 5}

    def rollout(rnd, mult):
        """按倍率决定判定次数（与 EliteLootSystem.RollCount 一致）。"""
        whole = int(mult)
        return whole + (1 if rnd.random() < (mult - whole) else 0)

    def simulate(names, is_boss, mult, trials, rnd):
        dist = Counter()
        for _ in range(trials):
            n = 0
            for name in names:
                a = affixes[name]
                for g in a["groups"]:                       # 每组：组内均匀取 1 条，再看它的概率
                    entry = rnd.choice(g)
                    for _ in range(rollout(rnd, mult)):
                        if rnd.random() <= entry[3]:
                            n += rnd.randint(entry[1], entry[2])
                for c in a["randoms"]:
                    for _ in range(rollout(rnd, mult)):
                        if rnd.random() <= c["chance"]:
                            n += c["itemCount"]
            power = (5 if is_boss else 0) + sum(rareness[affixes[x]["rarity"]] for x in names)
            for _ in range(rollout(rnd, mult)):
                if rnd.random() <= min(1.0, 0.30 + power * 0.05):
                    n += 1
            dist[n] += 1
        return dist

    # 随机取词条组合跑（等价于「一只随机出现的精英」）
    all_names = list(affixes.keys())
    print(f"     {'精英':<26}{'0件':>7}{'1件':>7}{'2件':>7}{'3件':>7}{'4件':>7}{'5件':>7}  | 平均")
    for label, k, boss in (("普通精英 · 1 词条", 1, False),
                           ("精英 · 2 词条", 2, False),
                           ("Boss · 3 词条", 3, True)):
        r = random.Random(4242)
        dist = Counter()
        sub = max(1, trials // 8)
        for _ in range(sub):
            names = [r.choice(all_names) for _ in range(k)]
            for cnt, c in simulate(names, boss, 1.0, 400, r).items():
                dist[cnt] += c
        tot = sum(dist.values())
        mean = sum(kk * v for kk, v in dist.items()) / tot
        ps = [dist.get(kk, 0) / tot for kk in range(0, 6)]
        print(f"     {label:<26}" + " ".join(f"{p*100:5.1f}%" for p in ps) +
              f"  | {mean:5.2f}   {ps[0]*100:4.1f}%")

    # 单个词条之间的差异有多大——「普通精英」不是一个同质群体
    print("\n     单个词条的差异（1 词条精英，倍率 1.0）——「普通精英」并不同质：")
    per = []
    for name in all_names:
        r = random.Random(99)
        d = simulate([name], False, 1.0, 4000, r)
        t = sum(d.values())
        per.append((name, sum(kk * v for kk, v in d.items()) / t, d.get(0, 0) / t))
    per.sort(key=lambda x: x[1])
    print(f"       {'最低 5 个词条':<34}{'期望件数':>9}{'P(0件)':>9}")
    for name, m, p0 in per[:5]:
        print(f"       {name:<34}{m:>9.2f}{p0*100:>8.1f}%")
    print(f"       {'最高 5 个词条':<34}{'期望件数':>9}{'P(0件)':>9}")
    for name, m, p0 in per[-5:][::-1]:
        print(f"       {name:<34}{m:>9.2f}{p0*100:>8.1f}%")
    guaranteed = sum(1 for _, _, p0 in per if p0 < 0.001)
    print(f"\n      ⇒ 50 个词条里，有 **{guaranteed}** 个是「必掉至少 1 件」（P(0件)=0）。")

    # —— 加权「典型普通精英」：词条数本身就是按权重分布的 ——
    AFFIX_COUNT_W = {1: 50, 2: 30, 3: 15, 4: 4, 5: 1}   # GameConfig 默认 AffixCountWeights
    ks = list(AFFIX_COUNT_W)
    ws = [AFFIX_COUNT_W[k] for k in ks]
    r = random.Random(31337)
    dist = Counter()
    sub = max(1, trials // 16)
    for _ in range(sub):
        k = r.choices(ks, weights=ws)[0]
        names = [r.choice(all_names) for _ in range(k)]
        for cnt, c in simulate(names, False, 1.0, 400, r).items():
            dist[cnt] += c
    tot = sum(dist.values())
    mean = sum(kk * v for kk, v in dist.items()) / tot
    ps = [dist.get(kk, 0) / tot for kk in range(0, 6)]
    print(f"\n      **加权「典型普通精英」**（词条数按默认权重 {ws[0]}/{ws[1]}/{ws[2]}/{ws[3]}/{ws[4]}）：")
    print(f"         " + " ".join(f"{kk}件 {p*100:4.1f}%" for kk, p in enumerate(ps)) + f"   | 平均 {mean:.2f}")
    print(f"         ⇒ P(至少 1 件) = {(1-ps[0])*100:.1f}%   —— **并非必掉**")

    # ---------------- D ----------------
    section("§D  ★ 核心问题：加入大量『最高品质』的 mod 物品，品质偏好会不会失效？")
    print("  实验：把 Q7 的物品池从 30 件扩到 30030 件（+30000 件 Q7 mod 物品），")
    print("        看品阶分布是否改变。\n")

    modded = dict(BASELINE_POOLS)
    modded[7] = 30 + 30_000

    print("  档位 | 原池 平均品阶 | +30000件Q7 平均品阶 | 分布是否改变")
    print("  -----|---------------|--------------------|--------------")
    all_same = True
    for tier, bias in enumerate(TIER_BIASES):
        h1 = histogram(BASELINE_POOLS, bias, trials, rnd)
        h2 = histogram(modded, bias, trials, rnd)
        d1, d2 = pct(h1), pct(h2)
        same = all(abs(d1[q] - d2[q]) < 0.6 for q in range(1, 8))
        all_same &= same
        print(f"   {tier}  |     {avg_quality(d1):5.2f}      |       {avg_quality(d2):5.2f}       |   {'未改变 ✓' if same else '改变了 ✗'}")
    print(f"\n  ⇒ 品阶分布{'**完全没变**' if all_same else '变了'}。")

    print("\n  但**拿到的东西是不是 mod 物品**变了。以档位 1（默认）为例：")
    d = pct(histogram(BASELINE_POOLS, TIER_BIASES[1], trials, rnd))
    p7 = 30 / (30 + 30_000)
    print(f"    Q7 被抽中的概率        : {d[7]:.2f}%（**与池子大小无关**）")
    print(f"    抽中 Q7 时命中 mod 物品 : 原池 {BASELINE_POOLS[7]}/{sum(BASELINE_POOLS.values())} → 现 {p7*100:.3f}%")
    print(f"    ⇒ mod 物品占全部掉落   : 约 {d[7] * p7:.4f}%")

    print("\n  ⚠ 真正的风险不在上面这条路，而在另一条**目前没有调用者**的代码路径：")
    print("     `CreateItemWithTagsFromAllQualities`（LootItemHelper.cs:346）")
    print("     它把所有品阶的候选物品摊成一张平表后**均匀取**，完全不看 bias。")
    print("     一旦有人给 CreateItemWithTagsWeighted 传 minQuality = -1 就会走到那里——")
    print("     那时命中概率 ∝ 该品阶的物品数，**加 mod 物品会直接把偏好淹没**。\n")
    print("     实测对比（档位 1，默认）：")
    h_bias = histogram(BASELINE_POOLS, TIER_BIASES[1], trials, rnd)
    print(f"       走 bias 的路径            : Q7 占 {pct(h_bias)[7]:5.2f}%")
    for label, pools in (("原池", BASELINE_POOLS), ("+30000件Q7", modded)):
        r = random.Random(7)
        c = Counter(pick_quality_uniform_over_items(pools, 1, 7, r) for _ in range(trials))
        print(f"       走平表均匀取（{label:<11}）: Q7 占 {c[7] / trials * 100:5.2f}%")
    print("\n     ⇒ 同样是「加 30000 件 Q7」这一个动作：走 bias 的路径纹丝不动，")
    print("        走平表的路径则被彻底淹没。**差别只在有没有看 bias**。")
    print("        目前无调用者（两个调用点传的都是 ≥1 的值），但这是一个待堵的口子。")

    # ---------------- E ----------------
    section("§E  结论")
    print("  1. 品质偏好**不会**因为加入大量高品阶 mod 物品而失效——")
    print("     因为品阶是**先按权重抽**、再在该品阶池内取物品；权重只看（档位, bias），")
    print("     与各品阶有多少件物品**无关**。§D 已用 +30000 件 Q7 实测验证。")
    print("  2. mod 物品改变的是『同一品阶内拿到哪一个』，不是『拿到几品』。")
    print("  3. **唯一会让它失效的是那条平表均匀取的路径**（bias 被绕过），")
    print("     目前不可达，建议加断言/日志堵住，而不是等它哪天被调用。")
    print("  4. 注意品阶分布还会被**地图上限**与**弱怪降级**截断（§B），")
    print("     那是范围问题，不是偏好失效。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
