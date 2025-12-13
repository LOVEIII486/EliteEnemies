using EliteEnemies.EliteEnemy.Core;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.BehaviorsPatches
{
    [HarmonyPatch(typeof(InteractableLootbox), "CreateFromItem")]
    public static class SlimeLootboxPhysicsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(InteractableLootbox __result, Item item)
        {
            if (__result == null || item == null) return;

            // 获取角色
            CharacterMainControl character = GetCharacterFromItem(item);
            if (character == null) return;

            // 检查是否有史莱姆行为组件
            var behaviorComponent = character.GetComponent<EliteBehaviorComponent>();
            if (behaviorComponent == null) return;

            // 检查是否有史莱姆词缀
            var marker = character.GetComponent<EliteEnemyCore.EliteMarker>();
            if (marker == null || marker.Affixes == null || !marker.Affixes.Contains("Slime"))
                return;

            // 让史莱姆的战利品箱使用物理掉落
            var rb = __result.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            var collider = __result.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
        }

        private static CharacterMainControl GetCharacterFromItem(Item item)
        {
            if (item == null) return null;

            try
            {
                var method = item.GetType().GetMethod("GetCharacterItem");
                if (method != null)
                {
                    Item characterItem = method.Invoke(item, null) as Item;
                    if (characterItem != null)
                    {
                        var character = characterItem.GetComponent<CharacterMainControl>();
                        if (character != null) return character;
                    }
                }
            }
            catch
            {
            }

            Transform current = item.transform;
            int depth = 0;
            while (current != null && depth < 10)
            {
                var character = current.GetComponent<CharacterMainControl>();
                if (character != null) return character;
                current = current.parent;
                depth++;
            }

            return null;
        }
    }
}