# 🌍 Elite Enemies Localization Guide

---

## 📄 File Overview

Each language version of the mod uses a **CSV file**, for example:

    ChineseSimplified.csv
    English.csv
    French.csv
    German.csv

All files share the same structure and should contain the same keys.

---

## 🧱 File Structure

| Column | Description |
|---------|--------------|
| `key` | Unique identifier used in the code (do not modify) |
| `value` | Translated text shown in game |
| `version` | Version number (keep as is unless updating) |
| `sheet` | Category (e.g., settings, affix names) |

---

## ⚠️ Important Notes

### Translation Principles

- Please prioritize the **Chinese version** as the basis for translation. 
- Please use a suitable CSV editor to avoid syntax issues.

---

### Special Field: `Affix_Talkative_Messages`

This key is used for enemies that **speak random lines** during combat.  
All lines are combined into **one single CSV field**, separated by the `|` character.

Example:
    
    Affix_Talkative_Messages,"I'm elite!|Fight me!|You can't win!|Hahaha!|Too weak!",1.0,AffixBehavior

- You can freely **add or remove lines** — the mod automatically handles any number of messages.
- You **don’t need to translate each line one-by-one**;  
  feel free to **add creative, language-appropriate phrases**.
- Just make sure to keep using the `|` separator between messages.

Example (customized translator version):

    Affix_Talkative_Messages,"Come at me!|I'm unstoppable!|Too easy!|You'll regret this!|Victory is mine!|Hey, is that all you've got?",1.0,AffixBehavior

---

### File Encoding

- Save the file as **UTF-8** to ensure all characters display correctly.  

