using System.Collections.Generic;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条互斥规则：某些词条同时出现会互相抵消或导致荒谬结果（例如「巨大化 + 迷你」）。
    ///
    /// <para>规则是**双向**判定的——只在一侧登记也能被识别，见 <see cref="AreMutuallyExclusive"/>。</para>
    /// </summary>
    public static class AffixExclusivity
    {
    /// <summary>
    /// 互斥词缀字典：Key = 词缀名，Value = 与该词缀互斥的词缀集合
    /// </summary>
    public static readonly Dictionary<string, HashSet<string>> Rules =
        new Dictionary<string, HashSet<string>>
        {
            ["Giant"] = new HashSet<string> { "Mini" },
            ["Slime"] = new HashSet<string> { "Mini","Split" },
            ["Undead"] = new HashSet<string> { "Explosive" },
            ["Talkative"] = new HashSet<string> { "Invisible" },
            //["Regeneration"] = new HashSet<string> { "Tanky" },
            ["Split"] = new HashSet<string> { "MimicTear", "ChickenBro" , "MandarinDuck"},
            ["MagazineCurse"] = new HashSet<string> { "DungEater","Sticky" },
            ["Knockback"] = new HashSet<string> { "Phase" },
            ["Fisherman"] =  new HashSet<string> { "Chef","Musician" },
            ["Chef"] =  new HashSet<string> { "Fisherman","Musician" },
            ["ChickenBro"] =  new HashSet<string> { "MandarinDuck" },
            ["Guardian"] =  new HashSet<string> { "Undead","Split","ChickenBro","MandarinDuck" },
            ["Slippery"] =  new HashSet<string> { "Slow" },
            // Talkative：伪装成箱子的敌人不该喊话。**加这一条是必要的**——话痨是直接
            //   `character.PopText(...)`（`TalkativeBehavior.cs:82`），**绕过 `canTalk`**，
            //   所以拟态那套"伪装期间闭嘴"的压制对它无效；两者只会在玩家贴脸时互相拆台。
            ["Mimic"] = new HashSet<string> { "MimicTear", "Split", "ChickenBro", "MandarinDuck", "Guardian","Giant","Slime","Reflect", "Talkative" },
            // ItemMimic：= Mimic 那一行的成员 **+ "Mimic" 自身**。
            //   两个伪装词条同时出现会互抢 Hide()/Show() 与血条闸门 ⇒ 敌人半隐半现。
            //   其余成员的理由与 Mimic 完全相同（那条的注释在上面），Talkative 尤其不能少。
            ["ItemMimic"] = new HashSet<string> { "Mimic", "MimicTear", "Split", "ChickenBro", "MandarinDuck", "Guardian","Giant","Slime","Reflect", "Talkative" },
        };

    /// <summary>
    /// 检查两个词缀是否互斥
    /// </summary>
    public static bool AreMutuallyExclusive(string affix1, string affix2)
    {
        if (string.IsNullOrEmpty(affix1) || string.IsNullOrEmpty(affix2))
            return false;
        
        if (Rules.TryGetValue(affix1, out var exclusions1))
        {
            if (exclusions1.Contains(affix2))
                return true;
        }
        if (Rules.TryGetValue(affix2, out var exclusions2))
        {
            if (exclusions2.Contains(affix1))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查某个词缀是否与已选词缀列表中的任何一个互斥
    /// </summary>
    public static bool ConflictsWithAny(string affix, IEnumerable<string> selectedAffixes)
    {
        if (string.IsNullOrEmpty(affix) || selectedAffixes == null)
            return false;

        foreach (var selected in selectedAffixes)
        {
            if (AreMutuallyExclusive(affix, selected))
                return true;
        }

        return false;
    }
    }
}
