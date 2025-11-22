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

                // 4.1 获取枪械组件并确保有子弹
                var gunComponent = clonedPrimary.GetComponent<ItemSetting_Gun>();
                if (gunComponent != null)
                {
                    Item currentBullet = gunComponent.GetCurrentLoadedBullet();
        
                    if (currentBullet != null)
                    {
                        currentBullet.StackCount = 100;
                        ForceUpdateBulletCount(clonedPrimary, gunComponent);
                        //Debug.Log($"[MimicTear] 已为 {clonedPrimary.DisplayName} 设置初始弹药 x10，当前: {gunComponent.BulletCount}");
                    }
                    else
                    {
                        EnsureGunHasBullet(clonedPrimary, gunComponent, enemy);
                    }
                    enemy.OnShootEvent += _ => RefillAmmoIfNeeded(clonedPrimary, gunComponent);
                }
            }

            // 5 复制玩家模型
            //CopyPlayerModel(enemy);
            
            // 6 强化AI
            EnhanceAIBehavior(enemy);
                
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
        /// 弹药补充方法
        /// </summary>
        private void RefillAmmoIfNeeded(Item gunItem, ItemSetting_Gun gunComponent)
        {
            try
            {
                // 先强制刷新弹药计数
                int actualCount = gunComponent.GetBulletCount();
        
                // 弹匣剩余子弹少于10发时补充
                if (actualCount < 10)
                {
                    int bulletTypeId = gunComponent.TargetBulletID;
                    if (bulletTypeId <= 0) return;
            
                    Item newBullet = ItemAssetsCollection.InstantiateSync(bulletTypeId);
                    if (newBullet != null)
                    {
                        newBullet.Initialize();
                        newBullet.Inspected = true;
                        newBullet.StackCount = 100;
                        gunItem.Inventory.AddAndMerge(newBullet);
                
                        // 强制刷新内部缓存
                        ForceUpdateBulletCount(gunItem, gunComponent);
                
                        // Debug.Log($"[MimicTear] 补充弹药成功，当前: {gunComponent.BulletCount}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MimicTear] 补充弹药异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 确保枪械有可用的子弹
        /// </summary>
        private void EnsureGunHasBullet(Item gunItem, ItemSetting_Gun gunComponent, CharacterMainControl owner)
        {
            try
            {
                // 检查枪械当前是否有子弹
                Item currentBullet = gunComponent.GetCurrentLoadedBullet();
                if (currentBullet != null)
                {
                    // 已有子弹，确保堆叠数量足够并刷新计数
                    if (currentBullet.StackCount < 100)
                    {
                        currentBullet.StackCount = 100;
                    }
                    ForceUpdateBulletCount(gunItem, gunComponent);
                    return;
                }

                // 尝试从枪械预制体获取默认子弹
                Item bulletToAdd = GetDefaultBulletForGun(gunItem);
                if (bulletToAdd == null)
                {
                    Debug.LogWarning($"[MimicTear] 无法为枪械 {gunItem.DisplayName} 找到合适的子弹");
                    return;
                }

                // 添加子弹到枪械的 Inventory
                bulletToAdd.Inspected = true;
                bulletToAdd.StackCount = 100;
                gunItem.Inventory.AddAndMerge(bulletToAdd);
        
                // 强制刷新计数
                ForceUpdateBulletCount(gunItem, gunComponent);
        
                // 尝试重新装填
                owner.TryToReload();

                //Debug.Log($"[MimicTear] 为枪械 {gunItem.DisplayName} 添加了子弹 {bulletToAdd.DisplayName}，当前弹药: {gunComponent.BulletCount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MimicTear] 添加子弹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取枪械的默认子弹
        /// </summary>
        private Item GetDefaultBulletForGun(Item gunItem)
        {
            try
            {
                // 从枪械预制体获取弹道信息
                Item gunPrefab = ItemAssetsCollection.GetPrefab(gunItem.TypeID);
                if (gunPrefab == null)
                {
                    Debug.LogWarning($"[MimicTear] 无法获取枪械预制体: {gunItem.TypeID}");
                    return null;
                }

                ItemSetting_Gun gunSetting = gunPrefab.GetComponent<ItemSetting_Gun>();
                if (gunSetting == null || gunSetting.bulletPfb == null)
                {
                    Debug.LogWarning($"[MimicTear] 枪械预制体没有子弹信息");
                    return null;
                }

                // 获取子弹的 TypeID
                int bulletTypeId = gunSetting.TargetBulletID;
                
                if (bulletTypeId <= 0)
                {
                    // 如果没有设置目标子弹，尝试从口径信息推断
                    //Debug.LogWarning($"[MimicTear] 枪械 {gunItem.DisplayName} 没有设置目标子弹类型");
                    
                    // 尝试使用一个通用的子弹ID
                    bulletTypeId = TryGetCommonBulletId(gunSetting);
                }

                if (bulletTypeId <= 0)
                {
                    return null;
                }
                
                Item bulletItem = ItemAssetsCollection.InstantiateSync(bulletTypeId);
                if (bulletItem != null)
                {
                    bulletItem.Initialize();
                }

                return bulletItem;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MimicTear] 获取默认子弹失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 尝试根据口径获取通用子弹ID
        /// </summary>
        private int TryGetCommonBulletId(ItemSetting_Gun gunSetting)
        {
            try
            {
                // 获取口径信息
                int caliberHash = "Caliber".GetHashCode();
                string caliber = gunSetting.Item.Constants.GetString(caliberHash);

                if (string.IsNullOrEmpty(caliber))
                {
                    return -1;
                }

                // 常见口径的子弹ID
                var caliberToBulletId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "S", 598 },
                    { "AR", 607 },
                    { "SHT", 634 },
                    { "L", 616 },
                    { "SNP", 701 },
                    { "MAG", 709 },
                    { "PWS", 1162 },
                };

                if (caliberToBulletId.TryGetValue(caliber, out int bulletId))
                {
                    return bulletId;
                }

                Debug.LogWarning($"[MimicTear] 未找到口径 {caliber} 对应的子弹ID");
                return -1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MimicTear] 获取通用子弹ID失败: {ex.Message}");
                return -1;
            }
        }
        
        private void ForceUpdateBulletCount(Item gunItem, ItemSetting_Gun gunComponent)
        {
            try
            {
                // 1. 从 Inventory 重新计算实际子弹数量
                int actualCount = gunComponent.GetBulletCount();
        
                // 2. 更新 Variables（界面显示用）
                int bulletCountHash = "BulletCount".GetHashCode();
                if (gunItem.Variables != null)
                {
                    gunItem.Variables.SetInt(bulletCountHash, actualCount);
                }
        
                // 3. 更新内部缓存字段
                var cacheField = gunComponent.GetType()
                    .GetField("_bulletCountCache", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cacheField != null)
                {
                    cacheField.SetValue(gunComponent, actualCount);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MimicTear] 更新弹药计数失败: {ex.Message}");
            }
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
                [AIFieldModifier.Fields.DefaultWeaponOut] = 1f,
                
                [AIFieldModifier.Fields.SightDistance] = 1.5f,
                [AIFieldModifier.Fields.SightAngle] = 1.3f,
                [AIFieldModifier.Fields.HearingAbility] = 1.5f,
                [AIFieldModifier.Fields.ForceTracePlayerDistance] = 2f,
                [AIFieldModifier.Fields.NightReactionTimeFactor] = 0.5f,
                
                [AIFieldModifier.Fields.PatrolRange] = 1.3f,
                [AIFieldModifier.Fields.CombatMoveRange] = 1.5f,
                [AIFieldModifier.Fields.ForgetTime] = 0.6f,
                
                [AIFieldModifier.Fields.PatrolTurnSpeed] = 1.3f,
                [AIFieldModifier.Fields.CombatTurnSpeed] = 1.5f,
                
                [AIFieldModifier.Fields.ItemSkillChance] = 1.5f,
                [AIFieldModifier.Fields.ItemSkillCoolTime] = 0.6f,
            };

            AIFieldModifier.ModifyDelayedBatch(enemy, aiEnhancements, multiply: true);
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