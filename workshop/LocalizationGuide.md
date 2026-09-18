# 🌍 Elite Enemies — Localization Guide

Thanks for helping translate the mod! This document covers everything you need.
You do **not** need to write code, and you do **not** need a compiler.

---

## 🚀 Quick start

1. Take **`English.csv`** as your template.
2. Save it as **`<Language>.csv`** — see the naming table below.
3. Translate **only the `value` column.** Leave `key`, `version` and `sheet` exactly as they are.
4. Send the file back.

That's it. [Testing your translation](#-testing-your-translation) is optional but recommended.

---

## 📄 File naming

**The file name *is* the language identifier** — it must match Unity's `SystemLanguage` enum.

| File name | Language |
|---|---|
| `ChineseSimplified.csv` | 简体中文 |
| `ChineseTraditional.csv` | 繁體中文 |
| `English.csv` | English |
| `Russian.csv` | Русский |
| `Japanese.csv` | 日本語 |
| `Korean.csv` | 한국어 |
| `French.csv`, `German.csv`, `Spanish.csv`, … | any other `SystemLanguage` name |

⚠ Chinese is special-cased: Unity's `SystemLanguage.Chinese` also resolves to
`ChineseSimplified.csv`. For every other language the rule is simply `<SystemLanguage>.csv`,
so a new language needs **no code change at all** — just drop the file in.

Currently shipped: **ChineseSimplified · ChineseTraditional · English · Russian** — **189 keys**.

---

## 🧱 File structure

Four columns plus a header row:

```csv
key,value,version,sheet
EliteEnemies_Affix_Tanky_Name,肉盾,2.3,AffixNames
```

| Column | Touch it? | Meaning |
|---|---|---|
| `key` | **Never** | The identifier the code looks up. Change it and that text **disappears silently**. |
| `value` | **Yes — this is the job** | What the player actually sees. |
| `version` | Leave as is | The author's own bookkeeping. **Not read at runtime.** |
| `sheet` | Leave as is | Category, for sorting in a spreadsheet. **Not read at runtime.** |

### CSV syntax

- Save as **UTF-8**.
- A value containing a **comma** must be wrapped in double quotes:
  `EliteEnemies_Some_Key,"Hello, world",2.3,AffixNames`
- Don't add or remove rows, and don't reorder the columns.
- A plain spreadsheet editor is fine — just make sure it exports real CSV.

---

## ⚠️ Backslashes are escape characters

**This is the most common way a translation breaks, and it fails *silently*.**

The game reads our CSV through `Regex.Unescape` (`CSVFileLocalizor.cs`, `convertFromEscapes`),
so a backslash in the `value` column is **not** a literal backslash:

| You write | Player sees | |
|---|---|---|
| `第一行\n第二行` | a real line break | ✅ intended — keep it |
| `前\t后` | a real tab | ✅ |
| `C:\\Games` | `C:\Games` | ✅ |
| `C:\Games` | — | ❌ **breaks this row** |
| `\o/` | — | ❌ **breaks this row** |
| `trailing\` | `trailing` | ⚠ the backslash vanishes, no warning |

> **Rule of thumb: never write a lone backslash.** If you genuinely need one, write `\\`.

Everything else passes through **unchanged**, so these are all safe:
`%` `<` `>` `{` `}` `$` `“ ”` `'` `[` `]` `|` `#` `@` `&` `+` `=` `;` `:` `!` `?`

### What "breaks this row" actually means

The one affected row is **skipped during loading** — that single string falls back to the
hard-coded Chinese text, everything else in your file still works, and `Player.log` gets an error
like:

    [EliteEnemies] 键 'EliteEnemies_Affix_Xxx_Name' 在表里存在，但游戏本地化器解析不到——推送漏了它

Nothing tells you it was the backslash. If you see a few strings stuck in Chinese while the rest
of your translation works, **search your file for `\`** — that is almost always the cause.

---

## ✨ Special fields

### `|`-separated lists

Two keys pack **several lines into one field**, separated by `|`:

- `EliteEnemies_Affix_Talkative_Messages` — the taunts a *Talkative* elite shouts in combat
- `EliteEnemies_Affix_Invisible_Messages`

Feel free to **add, remove or rewrite these** — the mod handles any number of lines, and they are
meant to be fun rather than literal. Just keep the `|` separators.

### `{0}` placeholders

Four keys contain format placeholders. **They must survive translation** — removing one makes the
number vanish (or throws at runtime):

| Key | Placeholder |
|---|---|
| `EliteEnemies_Affix_Hardening_PopText_1` | `{0}` |
| `EliteEnemies_Affix_Vampirism_PopText_1` | `{0}` |
| `EliteEnemies_Affix_Chaos_PopText_1` | `{0}` |
| `EliteEnemies_Affix_Tear_PopText_1` | `{0:0}` — keep the `:0` **exactly** |

### Rich-text tags `<color=…>`

22 keys wrap their text in Unity rich-text tags, for example:

```csv
EliteEnemies_Affix_Tear_PopText_1,<color=#FF0000>护甲撕裂 {0:0}%</color>,2.3,AffixBehavior
```

Keep the tags and the hex colour intact; translate only the text between them.

---

## 🔁 What happens if a key is missing

**There is deliberately no fallback to English.** A missing key falls back to the hard-coded
Chinese string in the code.

This is on purpose: falling back to English would *hide* the gap and it would never get fixed.
So an incomplete translation shows a mix of your language and Chinese — treat that as a to-do
list, not as a bug. Use `bash tools/check-localization.sh` (in the source repo) to list missing
keys; it compares all language files against each other.

---

## 🧪 Testing your translation

1. Drop your `.csv` into the mod's `Localization\` folder:
   `<Duckov>\Duckov_Data\Mods\EliteEnemies\Localization\`
2. Launch the game; it picks up your system language automatically.
3. Check `%USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log` for:

       [EliteEnemies] 已向游戏本地化器推送 189 条文本（语言 Xxx）

   - A **lower** number, or a `推送漏了它` line → see the backslash section above.
   - A `与别的来源撞车` suffix → one of your key names collides with another mod's. Report it.

---

## 📮 Submitting

Attach the `.csv` to a post on the Steam Workshop page, or send it to the feedback group
(**1030349064**). Please mention the language, and whether you'd like to be credited in the
mod description — translators are credited under **💖 特别鸣谢 / Special Thanks**.

---

## 🔧 Notes for maintainers

- **Every key must start with `EliteEnemies_`.** That prefix is what keeps the mod from colliding
  with other mods' localization; `tools/check-localization.sh` enforces it.
- **Adding a key means adding it to *all* language files**, otherwise the checker's
  "key sets match" rule fails and the other languages silently fall back to Chinese.
- `sheet` values in use (11): `Groups`, `BasicSettings`, `VisualSettings`, `AffixNames`,
  `AffixDescription`, `AffixBehavior`, `BuffNames`, `BuffDescriptions`, `ComboNames`,
  `ComboSettings`, `FromInfoKey`.
- Run before committing:

      bash tools/check-localization.sh     # structure, key sets, prefix, missing keys
      py   tools/check-affix-text.py       # UI text vs the workshop page vs the code
