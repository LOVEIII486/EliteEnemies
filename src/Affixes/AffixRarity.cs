using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Affixes
{
/// <summary>
/// 词条稀有度。**枚举值同时就是抽取权重**（<see cref="AffixData.Weight"/> 直接返回它），
/// 显示颜色只是按它查表（<see cref="AffixRarityColor"/>）。
///
/// <para>⚠ 两件事绑在同一个值上：改"稀有度"会**顺带改抽取概率**。要分开，
/// 得给 <see cref="AffixData"/> 加一个独立的 <c>Weight</c> 字段，而不是改这里的值。</para>
///
/// <para>⚠ 每档后面的数字**必须与枚举值一致**：曾漂移过一次——`Rare` 的注释写着"权重 20"，
/// 而枚举值是 35，照着注释推算平衡就会推错。改值时请连同注释一起改。</para>
/// </summary>
public enum AffixRarity
{
    Common = 100,   // 普通 —— 权重 100 —— 基础词缀
    Uncommon = 50,  // 高级 —— 权重 50 —— 略强词缀
    Rare = 35,      // 稀有 —— 权重 35 —— 强力词缀
    Epic = 20,      // 史诗 —— 权重 20 —— 极强词缀
    Legendary = 10  // 传说 —— 权重 10 —— 顶级词缀
}

    /// <summary>稀有度 → 显示颜色。放在枚举旁边，因为配色是稀有度的属性。</summary>
    public static class AffixRarityColor
    {
    private static readonly Dictionary<AffixRarity, Color> Colors = new Dictionary<AffixRarity, Color>
    {
        [AffixRarity.Common]    = new Color(0.90f, 0.90f, 1.00f),
        [AffixRarity.Uncommon]  = new Color(0.40f, 0.90f, 0.40f),
        [AffixRarity.Rare]      = new Color(0.129f, 0.737f, 1.00f),
        [AffixRarity.Epic]      = new Color(0.78f, 0.31f, 1.00f),
        [AffixRarity.Legendary] = new Color(1.00f, 0.58f, 0.00f),
    };
    
    public static Color Of(AffixRarity rarity)
    {
        return Colors.TryGetValue(rarity, out Color color) ? color : Color.white;
    }
    }
}
