using System;
using System.Collections.Generic;
using Duckov.Utilities;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：仿生泪滴
    /// </summary>
    public class MimicTearBehavior : AffixBehaviorBase
    {
        public override string AffixName => "MimicTear";

        private CharacterMainControl _owner;
        private Action<DamageInfo> _lootHook;

        public override void OnEliteInitialized(CharacterMainControl enemy)
        {
            if (enemy == null || enemy.Health == null || enemy.CharacterItem == null) return;
            _owner = enemy;

            var player = CharacterMainControl.Main;
            if (player == null || player.CharacterItem == null) return;

            string eName = (enemy.characterPreset != null) ? enemy.characterPreset.nameKey : enemy.name;
            string pHold = (player.CurrentHoldItemAgent != null && player.CurrentHoldItemAgent.Item != null)
                           ? player.CurrentHoldItemAgent.Item.name : "(none)";

            // 1 读取玩家需要复制的三个槽位
            Item srcPrimary = GetSlotItem(player, "PrimaryWeapon");
            Item srcHelmet  = GetSlotItem(player, "Helmat");
            Item srcArmor   = GetSlotItem(player, "Armor");

            // 2 清空敌人所有武器槽，防止旧武器抢用
            ClearWeaponSlots(enemy);

            // 3 仅克隆并拾取 主武器/头盔/护甲
            Item clonedPrimary = CloneViaInstantiate(srcPrimary, enemy.transform.position, enemy, "PrimaryWeapon");
            CloneViaInstantiate(srcHelmet,  enemy.transform.position, enemy, "Helmat");
            CloneViaInstantiate(srcArmor,   enemy.transform.position, enemy, "Armor");
            

            // 4 直接持握主武器
            if (clonedPrimary != null)
            {
                try
                {
                    enemy.ChangeHoldItem(clonedPrimary);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("ChangeHoldItem failed: " + ex.Message);
                }
            }

            // 5 死亡前清空掉落
            _lootHook = delegate (DamageInfo _)
            {
                try { SafeClearAllDrops(_owner); } catch (Exception ex) { Debug.LogWarning("BeforeLoot: " + ex.Message); }
            };
            enemy.BeforeCharacterSpawnLootOnDead += _lootHook;

            enemy.PopText("仿身复制完成");
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character != null && _lootHook != null)
                character.BeforeCharacterSpawnLootOnDead -= _lootHook;
            _owner = null;
            _lootHook = null;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }
        

        private static void ClearWeaponSlots(CharacterMainControl c)
        {
            try
            {
                // 依次销毁
                string[] weaponSlots = new string[] { "PrimaryWeapon", "SecondaryWeapon", "MeleeWeapon" };
                foreach (string sName in weaponSlots)
                {
                    Item it = GetSlotItem(c, sName);
                    if (it != null)
                    {
                        it.DestroyTree();
                    }
                }

                if (c.CharacterItem != null && c.CharacterItem.Inventory != null)
                {
                    List<Item> kill = new List<Item>();
                    foreach (var it in c.CharacterItem.Inventory) if (it != null && LooksLikeWeapon(it)) kill.Add(it);
                    foreach (var it in kill) { it.DestroyTree();  }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ClearWeaponSlots exception: " + ex.Message);
            }
        }

        private static bool LooksLikeWeapon(Item it)
        {
            if (it == null || string.IsNullOrEmpty(it.name)) return false;
            string n = it.name.ToLowerInvariant();
            return n.Contains("smg") || n.Contains("rifle") || n.Contains("br_") ||
                   n.Contains("ar_")  || n.Contains("pistol") || n.Contains("shotgun") ||
                   n.Contains("weapon");
        }

        private static Item CloneViaInstantiate(Item src, Vector3 pos, CharacterMainControl owner, string targetTagForLog)
        {
            if (src == null || owner == null) return null;

            try
            {
                GameObject go = UnityEngine.Object.Instantiate(src.gameObject, pos + Vector3.up * 0.05f, Quaternion.identity);
                Item clone = (go != null) ? go.GetComponent<Item>() : null;
                if (clone == null) return null;

                try { clone.Detach(); } catch { }
                try { clone.AgentUtilities.ReleaseActiveAgent(); } catch { }
                clone.Inspected = true;

                bool picked = owner.PickupItem(clone);

                return clone;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Instantiate " + src.name + " failed: " + e.Message);
                return null;
            }
        }

        private static Item GetSlotItem(CharacterMainControl c, string slotName)
        {
            try
            {
                return (c.CharacterItem != null && c.CharacterItem.Slots != null)
                    ? c.CharacterItem.Slots[slotName].Content
                    : null;
            }
            catch { return null; }
        }

        private static void SafeClearAllDrops(CharacterMainControl c)
        {
            if (c == null || c.CharacterItem == null) return;

            try
            {
                List<Item> buf = new List<Item>();
                if (c.CharacterItem.Inventory != null)
                    foreach (Item it in c.CharacterItem.Inventory) if (it != null) buf.Add(it);
                foreach (Slot s in c.CharacterItem.Slots)
                    if (s != null && s.Content != null) buf.Add(s.Content);
                foreach (Item it in buf) { it.DestroyTree();}
            }
            catch (Exception e)
            {
                Debug.LogWarning("Clear drops exception: " + e.Message);
            }
        }
    }
}
