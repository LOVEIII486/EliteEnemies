using System;
using System.Collections.Generic;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 词条 × 预设 的适配规则：**哪些词条允许出现在哪些敌人身上**。
    ///
    /// <para>它是一张按**词条名**索引的数据表——加一个词条时，它的适用敌人在<em>这里</em>配，
    /// 而不是去改决策逻辑。两个方向同时存在，判定顺序是**先黑后白**：</para>
    /// <list type="bullet">
    /// <item><see cref="Blacklist"/> 先排除，**优先级高于白名单**</item>
    /// <item><see cref="Whitelist"/> 列了就以它为准——只允许出现在列出的预设上；
    /// 列出但为空集合等价于「不限」</item>
    /// <item>两边都没列到该词条 → **默认允许**</item>
    /// </list>
    ///
    /// <para>从 <c>EliteEnemyCore</c> 拆出来的理由只有一个：这是**字面量表**，
    /// 而加词条的人不该在几百行的决策逻辑里翻找它。</para>
    /// </summary>
    internal static class AffixPresetRules
    {
        internal static readonly Dictionary<string, HashSet<string>> Whitelist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MimicTear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Enemies.Scav,
                    NPCPresetNames.Enemies.ScavElite,
                    NPCPresetNames.Enemies.ScavFarm,
                    NPCPresetNames.Enemies.ScavIce,
                    NPCPresetNames.Enemies.ScavLow,
                    NPCPresetNames.Enemies.ScavLowAK,
                    NPCPresetNames.Enemies.ScavSnow,

                    NPCPresetNames.Enemies.USECFarm,
                    NPCPresetNames.Enemies.USECHidden,
                    NPCPresetNames.Enemies.USECLow,
                    NPCPresetNames.Enemies.USECIce,
                    NPCPresetNames.Enemies.USECIceMilitary,
                    NPCPresetNames.Enemies.USECSnowMilitary,

                    NPCPresetNames.Enemies.Raider,
                    NPCPresetNames.Minions.BALeaderChild,
                    NPCPresetNames.Minions.ThreeShotChild,
                    NPCPresetNames.Minions.SpeedyChild,
                    NPCPresetNames.Minions.Storm1Child,
                    NPCPresetNames.Minions.SpeedyIceChild
                },
                // ⚠ 这里曾经还有三条：MandarinDuck / Guardian / Slime = { GunTurret }。
                //    白名单的语义是「**只允许**出现在这些预设上」，于是这三个词条被限死在
                //    炮台上——而炮台永远不会被精英化（`PresetDirectory.IgnoredGenericPresets`
                //    含 `NPCPresetNames.Special.All`），**结果是这三个词条一个都出不来**。
                //    Guardian / Slime 只能经 combo 出现（combo 路径绕过本表），
                //    MandarinDuck 连 combo 都没有，从来没能出现过。
                //
                //    已删除，而不是移进 Blacklist：「炮台不会成为精英」这条事实
                //    已经由 IgnoredGenericPresets 保证，**本表不必重述**——同一条事实
                //    写在两处必然漂移，而这里漂掉的还是语义方向（本该是排除，写成了只允许）。
            };

        internal static readonly Dictionary<string, HashSet<string>> Blacklist =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mimic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "EnemyPreset_Boss_Kamakoto_Special"
                },
                // ItemMimic 同理：BOSS 不该伪装成地上的一件物品。
                ["ItemMimic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "EnemyPreset_Boss_Kamakoto_Special"
                },
                ["Explosive"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NPCPresetNames.Enemies.JLabInvisible
                }
            };

        /// <summary>
        /// 该词条是否允许出现在该预设上。判定顺序见类型注释（先黑后白，都没列则允许）。
        /// </summary>
        internal static bool IsAffixAllowedForPreset(string affixName, string resourceName)
        {
            if (string.IsNullOrEmpty(affixName) || string.IsNullOrEmpty(resourceName))
                return false;

            if (Blacklist.TryGetValue(affixName, out var blacklist))
            {
                if (blacklist != null && blacklist.Contains(resourceName))
                    return false;
            }

            if (Whitelist.TryGetValue(affixName, out var whitelist))
                return whitelist == null || whitelist.Count == 0 || whitelist.Contains(resourceName);

            return true;
        }
    }
}
