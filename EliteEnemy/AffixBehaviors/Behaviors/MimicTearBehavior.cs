using System;
using System.Collections.Generic;
using System.Reflection;
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

            // 1 读取玩家需要复制的三个槽位
            Item srcPrimary = GetSlotItem(player, "PrimaryWeapon");
            Item srcHelmet = GetSlotItem(player, "Helmat");
            Item srcArmor = GetSlotItem(player, "Armor");

            // 2 清空敌人所有武器槽，防止旧武器抢用
            ClearWeaponSlots(enemy);

            // 3 仅克隆并拾取 主武器/头盔/护甲
            Item clonedPrimary = CloneViaInstantiate(srcPrimary, enemy.transform.position, enemy, "PrimaryWeapon");
            CloneViaInstantiate(srcHelmet, enemy.transform.position, enemy, "Helmat");
            CloneViaInstantiate(srcArmor, enemy.transform.position, enemy, "Armor");

            // 4 直接持握主武器 并 注入无限弹药逻辑
            if (clonedPrimary != null)
            {
                enemy.ChangeHoldItem(clonedPrimary);

                if (clonedPrimary.Variables != null)
                {
                    clonedPrimary.Variables.SetInt("BulletCount", 9999);
                }

                var gunComponent = clonedPrimary.GetComponent<ItemSetting_Gun>();
                if (gunComponent != null)
                {
                    var field = gunComponent.GetType()
                        .GetField("_bulletCountCache", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(gunComponent, 9999);
                    }
                }
            }

            // 5 复制玩家模型
            //CopyPlayerModel(enemy);
            // 6 强化AI
            EnhanceAIBehavior(enemy); // 快速传送会导致敌人AI来不及加载
            
            // 7 死亡前清空掉落
            _lootHook = delegate(DamageInfo _) { SafeClearAllDrops(_owner); };
            enemy.BeforeCharacterSpawnLootOnDead += _lootHook;
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
        
        /// <summary>
        /// 全面强化 AI 行为
        /// </summary>
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            var aiEnhancements = new Dictionary<string, float>
            {
                [AIFieldModifier.Fields.ReactionTime] = 0.15f,
                [AIFieldModifier.Fields.ShootDelay] = 0.2f,
                [AIFieldModifier.Fields.ShootCanMove] = 1f,
                [AIFieldModifier.Fields.CanDash] = 1f,
                [AIFieldModifier.Fields.SightDistance] = 1.5f,
                [AIFieldModifier.Fields.SightAngle] = 1.3f,
                [AIFieldModifier.Fields.HearingAbility] = 1.5f,
                [AIFieldModifier.Fields.NightReactionTimeFactor] = 0.5f,
                [AIFieldModifier.Fields.PatrolTurnSpeed] = 1.3f,
                [AIFieldModifier.Fields.CombatTurnSpeed] = 1.5f
            };
 
            AIFieldModifier.ModifyDelayedBatch(enemy, aiEnhancements, multiply: true);
            //Debug.Log($"[MimicTear] 已对 {enemy.characterPreset.nameKey} {enemy.GetHashCode()} 应用全面 AI 强化 (共 {aiEnhancements.Count} 项)");
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
                    foreach (var it in c.CharacterItem.Inventory)
                        if (it != null && LooksLikeWeapon(it))
                            kill.Add(it);
                    foreach (var it in kill)
                    {
                        it.DestroyTree();
                    }
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
                   n.Contains("ar_") || n.Contains("pistol") || n.Contains("shotgun") ||
                   n.Contains("weapon");
        }

        private static Item CloneViaInstantiate(Item src, Vector3 pos, CharacterMainControl owner,
            string targetTagForLog)
        {
            if (src == null || owner == null) return null;

            try
            {
                GameObject go =
                    UnityEngine.Object.Instantiate(src.gameObject, pos + Vector3.up * 0.05f, Quaternion.identity);
                Item clone = (go != null) ? go.GetComponent<Item>() : null;
                if (clone == null) return null;
                
                clone.Detach();
                clone.AgentUtilities.ReleaseActiveAgent();
                clone.Inspected = true;
                owner.PickupItem(clone);

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
            catch
            {
                return null;
            }
        }

        private static void SafeClearAllDrops(CharacterMainControl c)
        {
            if (c == null || c.CharacterItem == null) return;

            try
            {
                List<Item> buf = new List<Item>();
                if (c.CharacterItem.Inventory != null)
                    foreach (Item it in c.CharacterItem.Inventory)
                        if (it != null)
                            buf.Add(it);
                foreach (Slot s in c.CharacterItem.Slots)
                    if (s != null && s.Content != null)
                        buf.Add(s.Content);
                foreach (Item it in buf)
                {
                    it.DestroyTree();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Clear drops exception: " + e.Message);
            }
        }
    }
}