using System.Collections.Generic;
using EliteEnemies.Affixes.Behaviors;
using EliteEnemies.Loot;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// <see cref="LootboxPatch"/> 的**第三步**：把分裂体（<c>SplitCloneMarker</c>）的掉落箱
    /// 削减到 2 件。
    ///
    /// <para><b>⚠ 它必须跑在 <c>EliteLootSystem.Apply</c>（填充精英掉落）之后。</b>
    /// 本方法开头有 <c>if (items.Count &lt;= 2) return;</c> 的提前返回——
    /// 若箱子当时还是空的（即填充步骤尚未跑），这里什么也不做，
    /// 随后填充步骤会把箱子填满，分裂体就拿到全额掉落。
    /// 那正是「分身仅掉落 2 件物品」这条词条说明要防的情况。</para>
    ///
    /// <para>**它不是 Harmony 补丁类**，只是被编排入口按序调用的一步。
    /// 原先它是独立挂 <c>[HarmonyPatch]</c> 的——顺序不可控，见 <see cref="LootboxPatch"/>。</para>
    /// </summary>
    internal static class SplitCloneLoot
    {
        /// <summary>分裂体最多保留的掉落件数——与词条说明「分身仅掉落 2 件物品」一致。</summary>
        private const int MaxCloneLootCount = 2;

        /// <summary>
        /// <paramref name="character"/> 由编排入口 <see cref="LootboxPatch"/> **统一反查一次**后传入——
        /// 三步都要它，而那次反查带层级遍历兜底（见 <c>EliteLootSystem.GetCharacterFromItem</c>）。
        /// </summary>
        internal static void Reduce(InteractableLootbox lootbox, CharacterMainControl character)
        {
            if (lootbox == null || character == null) return;

            // 只处理分裂体的箱子；普通精英的箱子不在此处削减
            if (character.GetComponent<SplitBehavior.SplitCloneMarker>() == null) return;

            ClearCloneLootBoxInventory(lootbox);
        }

        private static void ClearCloneLootBoxInventory(InteractableLootbox lootbox)
        {
            var inventory = lootbox.Inventory;
            if (inventory == null) return;

            var items = new List<Item>();
            foreach (var it in inventory)
            {
                if (it != null) items.Add(it);
            }

            // 已经不多于上限就什么都不做。这个提前返回正是「顺序不能错」的原因，见类注释。
            if (items.Count <= MaxCloneLootCount) return;

            // 随机保留 MaxCloneLootCount 件，其余销毁。
            // 走到这里时 items.Count >= 3，因此下面必然能凑满 MaxCloneLootCount 个不同下标，不会死循环。
            HashSet<int> keepIndices = new HashSet<int>();
            while (keepIndices.Count < MaxCloneLootCount)
            {
                keepIndices.Add(Random.Range(0, items.Count));
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (keepIndices.Contains(i)) continue;
                items[i]?.DestroyTree();
            }
        }
    }
}
