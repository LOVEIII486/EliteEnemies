using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Utilities;
using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 词缀：仿生泪滴
    /// </summary>
    public class MimicTearBehavior : AffixBehaviorBase
    {
        public override string AffixName => "MimicTear";
        private const string LogTag = "[EliteEnemies.MimicTear]";
        
        // 静态缓存区域
        private static bool _isReflectionInitialized = false;
        private static bool _hasCustomModelMod = false;

        // 类型缓存
        private static Type _modelHandlerType;
        private static Type _bundleType;
        private static Type _infoType;
        private static Type _targetEnumType;

        // 方法与字段缓存
        private static FieldInfo _bundleInfoField;
        private static FieldInfo _modelInfoField;
        private static MethodInfo _initMethod;
        private static MethodInfo _loadMethod;
        private static MethodInfo _changeMethod;
        private static object _aiTargetEnumValue; // 缓存枚举值

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
                    }
                    else
                    {
                        EnsureGunHasBullet(clonedPrimary, gunComponent, enemy);
                    }
                    enemy.OnShootEvent += _ => RefillAmmoIfNeeded(clonedPrimary, gunComponent);
                }
            }

            // 5 复制玩家模型
            CopyPlayerModel(enemy, CharacterMainControl.Main);
            
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
        /// 全面强化 AI 行为
        /// </summary>
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            // --- Stat 修改 ---
            // 使用 PercentageMultiply，数值为增量 (0.5 = +50%)
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewDistance, 0.5f, ModifierType.PercentageMultiply); 
            StatModifier.AddModifier(enemy, StatModifier.Attributes.ViewAngle, 0.3f, ModifierType.PercentageMultiply); 
            StatModifier.AddModifier(enemy, StatModifier.Attributes.HearingAbility, 0.5f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.TurnSpeed, 0.3f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.AimTurnSpeed, 0.5f, ModifierType.PercentageMultiply);

            // --- AI 字段修改 ---
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ShootCanMove, 1f, false);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.CanDash, 1f, false);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.PatrolRange, 1.3f, true);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.CombatMoveRange, 1.5f, true);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ForgetTime, 0.6f, true);
        }

        private void CopyPlayerModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            InitializeReflection();

            bool customSuccess = false;
            if (_hasCustomModelMod)
            {
                customSuccess = TryCopyCustomModel(enemy, player);
            }

            if (!customSuccess)
            {
                CopyVanillaFace(enemy, player);
            }
            else
            {
                HideEquipmentVisuals(enemy);
            }
        }
        
        private static void InitializeReflection()
        {
            if (_isReflectionInitialized) return;
            _isReflectionInitialized = true;

            try
            {
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _infoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");
                _targetEnumType = Type.GetType("DuckovCustomModel.Core.Data.ModelTarget, DuckovCustomModel.Core");

                if (_modelHandlerType == null || _bundleType == null || _infoType == null || _targetEnumType == null)
                {
                    _hasCustomModelMod = false;
                    return;
                }

                _bundleInfoField = _modelHandlerType.GetField("_currentModelBundleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                _modelInfoField = _modelHandlerType.GetField("_currentModelInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), _targetEnumType });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _infoType });
                _changeMethod = _modelHandlerType.GetMethod("ChangeToCustomModel");

                _aiTargetEnumValue = Enum.Parse(_targetEnumType, "AICharacter");;

                if (_initMethod != null && _loadMethod != null && _changeMethod != null)
                {
                    _hasCustomModelMod = true;
                    Debug.Log($"{LogTag} 自定义模型功能已就绪。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化异常: {ex.Message}");
                _hasCustomModelMod = false;
            }
        }
        
        private void HideEquipmentVisuals(CharacterMainControl enemy)
        {
            try
            {
                if (enemy == null || enemy.characterModel == null) return;
                
                Transform[] socketsToHide = new Transform[] 
                {
                    enemy.characterModel.HelmatSocket,
                    enemy.characterModel.ArmorSocket
                };

                foreach (var socket in socketsToHide)
                {
                    if (socket == null) continue;
                    
                    Renderer[] renderers = socket.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        r.enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MimicTear] 隐藏装备失败: {ex.Message}");
            }
        }
        
        private bool TryCopyCustomModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            try
            {
                Component playerHandler = player.GetComponent(_modelHandlerType);
                if (playerHandler == null) return false;

                object bundleInfo = _bundleInfoField.GetValue(playerHandler);
                object modelInfo = _modelInfoField.GetValue(playerHandler);

                if (bundleInfo == null || modelInfo == null) return false;

                Component enemyHandler = enemy.GetComponent(_modelHandlerType);
                if (enemyHandler == null)
                {
                    enemyHandler = enemy.gameObject.AddComponent(_modelHandlerType);
                }

                _initMethod.Invoke(enemyHandler, new object[] { enemy, _aiTargetEnumValue });
                _loadMethod.Invoke(enemyHandler, new object[] { bundleInfo, modelInfo });
                _changeMethod.Invoke(enemyHandler, null);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 复制过程异常: {ex.Message}");
                return false;
            }
        }

        private void CopyVanillaFace(CharacterMainControl enemy, CharacterMainControl player)
        {
            try
            {
                if (enemy.characterModel == null || player.characterModel == null) return;

                var playerFaceInstance = player.characterModel.CustomFace;
                if (playerFaceInstance == null) return;
                
                var faceData = playerFaceInstance.ConvertToSaveData();
                enemy.characterModel.SetFaceFromData(faceData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 复制原版外观失败: {ex.Message}");
            }
        }
        
        private void RefillAmmoIfNeeded(Item gunItem, ItemSetting_Gun gunComponent)
        {
            try
            {
                int actualCount = gunComponent.GetBulletCount();
        
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
                        ForceUpdateBulletCount(gunItem, gunComponent);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 补充弹药异常: {ex.Message}");
            }
        }
        
        private void EnsureGunHasBullet(Item gunItem, ItemSetting_Gun gunComponent, CharacterMainControl owner)
        {
            try
            {
                Item currentBullet = gunComponent.GetCurrentLoadedBullet();
                if (currentBullet != null)
                {
                    if (currentBullet.StackCount < 100)
                    {
                        currentBullet.StackCount = 100;
                    }
                    ForceUpdateBulletCount(gunItem, gunComponent);
                    return;
                }

                Item bulletToAdd = GetDefaultBulletForGun(gunItem);
                if (bulletToAdd == null)
                {
                    Debug.LogWarning($"{LogTag} 无法为枪械 {gunItem.DisplayName} 找到合适的子弹");
                    return;
                }

                bulletToAdd.Inspected = true;
                bulletToAdd.StackCount = 100;
                gunItem.Inventory.AddAndMerge(bulletToAdd);
                ForceUpdateBulletCount(gunItem, gunComponent);
                owner.TryToReload();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 添加子弹失败: {ex.Message}");
            }
        }

        private Item GetDefaultBulletForGun(Item gunItem)
        {
            try
            {
                Item gunPrefab = ItemAssetsCollection.GetPrefab(gunItem.TypeID);
                if (gunPrefab == null) return null;

                ItemSetting_Gun gunSetting = gunPrefab.GetComponent<ItemSetting_Gun>();
                if (gunSetting == null || gunSetting.bulletPfb == null) return null;

                int bulletTypeId = gunSetting.TargetBulletID;
                if (bulletTypeId <= 0)
                {
                    bulletTypeId = TryGetCommonBulletId(gunSetting);
                }

                if (bulletTypeId <= 0) return null;
                
                Item bulletItem = ItemAssetsCollection.InstantiateSync(bulletTypeId);
                if (bulletItem != null) bulletItem.Initialize();

                return bulletItem;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 获取默认子弹失败: {ex.Message}");
                return null;
            }
        }

        private int TryGetCommonBulletId(ItemSetting_Gun gunSetting)
        {
            try
            {
                int caliberHash = "Caliber".GetHashCode();
                string caliber = gunSetting.Item.Constants.GetString(caliberHash);

                if (string.IsNullOrEmpty(caliber)) return -1;

                var caliberToBulletId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "S", 598 }, { "AR", 607 }, { "SHT", 634 }, { "L", 616 },
                    { "SNP", 701 }, { "MAG", 709 }, { "PWS", 1162 },
                };

                if (caliberToBulletId.TryGetValue(caliber, out int bulletId)) return bulletId;
                return -1;
            }
            catch { return -1; }
        }
        
        private void ForceUpdateBulletCount(Item gunItem, ItemSetting_Gun gunComponent)
        {
            try
            {
                int actualCount = gunComponent.GetBulletCount();
                int bulletCountHash = "BulletCount".GetHashCode();
                if (gunItem.Variables != null)
                {
                    gunItem.Variables.SetInt(bulletCountHash, actualCount);
                }
        
                var cacheField = gunComponent.GetType()
                    .GetField("_bulletCountCache", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cacheField != null)
                {
                    cacheField.SetValue(gunComponent, actualCount);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 更新弹药计数失败: {ex.Message}");
            }
        }

        private static void ClearWeaponSlots(CharacterMainControl c)
        {
            try
            {
                string[] weaponSlots = new string[] { "PrimaryWeapon", "SecondaryWeapon", "MeleeWeapon" };
                foreach (string sName in weaponSlots)
                {
                    Item it = GetSlotItem(c, sName);
                    if (it != null) it.DestroyTree();
                }

                if (c.CharacterItem != null && c.CharacterItem.Inventory != null)
                {
                    List<Item> kill = new List<Item>();
                    foreach (var it in c.CharacterItem.Inventory)
                        if (it != null && LooksLikeWeapon(it))
                            kill.Add(it);
                    foreach (var it in kill) it.DestroyTree();
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

        private static Item CloneViaInstantiate(Item src, Vector3 pos, CharacterMainControl owner, string targetTagForLog)
        {
            if (src == null || owner == null) return null;

            try
            {
                GameObject go = UnityEngine.Object.Instantiate(src.gameObject, pos + Vector3.up * 0.05f, Quaternion.identity);
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
                        if (it != null) buf.Add(it);
                foreach (Slot s in c.CharacterItem.Slots)
                    if (s != null && s.Content != null) buf.Add(s.Content);
                foreach (Item it in buf)
                {
                    it.DestroyTree();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Clear drops exception: " + e.Message);
            }
        }
    }
}