using EliteEnemies.Affixes;
using EliteEnemies.Core;
using EliteEnemies.Loot;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// <see cref="LootboxPatch"/> 的第二步：让史莱姆的战利品箱走物理掉落，
    /// 否则它会违反重力悬在空中。
    ///
    /// <para>**它不是 Harmony 补丁类**，只是被编排入口按序调用的一步。
    /// 原先它是独立挂 <c>[HarmonyPatch]</c> 的——那样顺序不可控，见 <see cref="LootboxPatch"/>。</para>
    /// </summary>
    internal static class SlimeLootboxPhysics
    {
        /// <summary>
        /// <paramref name="character"/> 由编排入口 <see cref="LootboxPatch"/> **统一反查一次**后传入——
        /// 三步都要它，而那次反查带层级遍历兜底（见 <c>EliteLootSystem.GetCharacterFromItem</c>），
        /// 各自查一遍等于把同一份开销付三遍。
        /// </summary>
        internal static void Apply(InteractableLootbox lootbox, CharacterMainControl character)
        {
            if (lootbox == null || character == null) return;

            // 必须有史莱姆行为组件，且确实带 Slime 词条——两个条件缺一不可
            if (character.GetComponent<EliteBehaviorComponent>() == null) return;

            var marker = character.GetComponent<EliteMarker>();
            if (marker == null || marker.Affixes == null || !marker.Affixes.Contains("Slime"))
            {
                return;
            }

            var rb = lootbox.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            var collider = lootbox.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
        }
    }
}
