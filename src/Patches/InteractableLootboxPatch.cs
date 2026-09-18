using System;
using EliteEnemies.Affixes;
using EliteEnemies.Loot;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// <c>InteractableLootbox.CreateFromItem</c> 的**唯一** Harmony 补丁入口。
    ///
    /// <para><b>为什么要这样一个编排点</b>：这个方法上有三件互不相关的事要做，
    /// 而它们**有先后要求**。原先三个类各自挂 <c>[HarmonyPatch]</c>，
    /// 顺序由 Harmony 的补丁排序决定——那取决于 <c>PatchAll()</c> 扫描类型的顺序，
    /// 而扫描顺序又取决于类型在程序集里的排布（<b>连挪动源文件都可能改变它</b>）。
    /// 也就是说：顺序一直是未定义的，只是碰巧能用。</para>
    ///
    /// <para><b>顺序为什么不能错</b>：第 1 步给箱子**填**精英掉落，第 3 步把分裂体的箱子
    /// **削减**到 2 件（词条说明是「分身仅掉落 2 件物品」）。第 3 步内部有
    /// <c>if (items.Count &lt;= 2) return;</c> 的提前返回——所以一旦第 3 步先跑，
    /// 它什么也不做，随后第 1 步把箱子填满，分裂体就拿到了全额掉落。
    /// 这正是第 3 步要防的「海量掉落」。</para>
    ///
    /// <para><b>顺序现在是代码里可见的调用顺序</b>，不再依赖框架的隐式排序——
    /// 以后挪文件、加补丁都不会改变它。</para>
    /// </summary>
    [HarmonyPatch(typeof(InteractableLootbox), nameof(InteractableLootbox.CreateFromItem))]
    internal static class LootboxPatch
    {
        private const string LogTag = "[EliteEnemies.Lootbox]";

        [HarmonyPostfix]
        private static void Postfix(InteractableLootbox __result, Item item, Vector3 position)
        {
            if (__result == null || item == null) return;

            // 角色**只反查一次**（三步原先各查一遍，而那次反查带层级遍历兜底）。
            // 三步在"角色为 null 就整段早退"上语义一致（逐个 diff 过），所以这里的提前返回与
            // 各自早退等价。
            CharacterMainControl character = ResolveCharacter(item);
            if (character == null) return;

            // ⚠ 三步的顺序是有约束的，改动前先读类注释与各步骤自己的说明。
            //
            // 每一步**各自隔离异常**：这是**死亡链上的补丁**（`CharacterMainControl.OnDead`
            // → `InteractableLootbox.CreateFromItem`），一个步骤抛异常既不该带走另外两步，
            // 更不该把游戏的死亡处理中断掉。第 ③ 步尤其不能被跳过——它防的是
            // "分裂体拿全额掉落"（见类注释）。
            Step("① 填充精英掉落", () => EliteLootSystem.Apply(__result, item, character, position));
            Step("② 史莱姆箱子物理化", () => SlimeLootboxPhysics.Apply(__result, character));
            Step("③ 削减分裂体箱子", () => SplitCloneLoot.Reduce(__result, character));
        }

        /// <summary>
        /// 反查掉落箱归属的角色（三步共用）。
        ///
        /// <para>⚠ <b>必须自己兜异常</b>：这一步原本在 <c>EliteLootSystem.Apply</c> 自己的 try 里跑，
        /// 提上来之后若不兜，异常会顺着 <c>InteractableLootbox.CreateFromItem</c> 冒进
        /// **死亡链**（<c>CharacterMainControl.OnDead</c> → 这里），
        /// 把本模组的一个 bug 变成"游戏的死亡处理被中断"。</para>
        /// </summary>
        private static CharacterMainControl ResolveCharacter(Item item)
        {
            try
            {
                return EliteLootSystem.GetCharacterFromItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 反查掉落箱归属角色失败（已隔离，本次跳过全部掉落步骤）: {ex}");
                return null;
            }
        }

        /// <summary>跑一个掉落处理步骤：异常只记录、不重抛（理由见 Postfix 的注释）。</summary>
        private static void Step(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 掉落步骤「{name}」失败（已隔离）: {ex}");
            }
        }
    }
}
