using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EliteEnemies.EliteEnemy.AttributeModifier;
using UnityEngine;
using ItemStatsSystem;
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
        
        // 保底子弹映射表
        private static readonly Dictionary<string, int> CaliberToBulletId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "SMG", 598 }, // 手枪、冲锋枪，S高级弹
            { "AR", 607 }, // 步枪，AR高级弹
            { "PWS", 1162 }, // 小型能量弹
            { "PWL",918 }, // 风暴枪，大型能量弹
            { "MAG", 709 }, // Mag高级弹
            {"Candy",1262}, // 糖果枪，糖果弹
            {"Pop", 944}, // 噗噗枪，噗噗弹
            { "SHT", 634 }, // 喷子，高级霰弹
            { "BR", 616 }, // L弹
            { "SNP", 701 }, // 狙击枪
            {"Rocket", 326}, // 火箭筒，火箭弹lv1
            { "GL" , 95815} // 榴弹发射器，榴弹 (MOD 武器)
        };

        private CharacterMainControl _owner;
        private Action<DamageInfo> _lootHook;

        private static bool _isReflectionInitialized = false;
        private static bool _hasCustomModelMod = false;
        private static Type _modelHandlerType, _bundleType, _infoType;
        private static FieldInfo _bundleInfoField;
        private static PropertyInfo _modelInfoProperty;
        private static MethodInfo _initMethod, _loadMethod;

        public override void OnEliteInitialized(CharacterMainControl enemy)
        {
            if (enemy == null || enemy.CharacterItem == null) return;
            _owner = enemy;

            var player = CharacterMainControl.Main;
            if (player == null || player.CharacterItem == null) return;

            ClearWeaponSlots(enemy);

            Item srcPrimary = GetSlotItem(player, "PrimaryWeapon");
            Item srcHelmet = GetSlotItem(player, "Helmat");
            Item srcArmor = GetSlotItem(player, "Armor");

            CloneAndSetupWeapon(srcPrimary, enemy, player);
            CloneToSlot(srcHelmet, enemy, "Helmat");
            CloneToSlot(srcArmor, enemy, "Armor");

            CopyPlayerModel(enemy, player);
            EnhanceAIBehavior(enemy);
                
            _lootHook = delegate(DamageInfo _) { ClearAllDrops(_owner); };
            enemy.BeforeCharacterSpawnLootOnDead += _lootHook;
        }

        private void CloneAndSetupWeapon(Item srcWeapon, CharacterMainControl enemy, CharacterMainControl player)
        {
            if (srcWeapon == null) return;

            GameObject go = UnityEngine.Object.Instantiate(srcWeapon.gameObject, enemy.transform.position, Quaternion.identity);
            Item clone = go.GetComponent<Item>();
            if (clone == null) return;

            clone.Detach();
            clone.AgentUtilities.ReleaseActiveAgent();
            
            enemy.PickupItem(clone);
            enemy.ChangeHoldItem(clone);

            var gun = clone.GetComponent<ItemSetting_Gun>();
            if (gun != null)
            {
                string caliber = srcWeapon.Constants.GetString("Caliber");
                //Debug.Log($"{LogTag} [Caliber Check] 武器名称: {srcWeapon.DisplayName}, 检测到口径字符串: '{caliber}'");

                int bulletId = -1;
                string sourceFound = "None";

                var srcGun = srcWeapon.GetComponent<ItemSetting_Gun>();
                if (srcGun != null && srcGun.TargetBulletID > 0)
                {
                    bulletId = srcGun.TargetBulletID;
                    sourceFound = "PlayerWeapon";
                }

                // 如果枪里没有，查找保底字典
                if (bulletId <= 0 && !string.IsNullOrEmpty(caliber))
                {
                    if (CaliberToBulletId.TryGetValue(caliber, out bulletId))
                    {
                        sourceFound = "ManualMapping";
                    }
                }

                // Debug.Log($"{LogTag} [ID Selection] 选取的物品ID: {bulletId}, 来源: {sourceFound}");

                if (bulletId > 0)
                {
                    Item bulletSeed = ItemAssetsCollection.InstantiateSync(bulletId);
                    if (bulletSeed != null)
                    {
                        bulletSeed.Initialize();
                        
                        if (bulletSeed.Stackable)
                        {
                            bulletSeed.StackCount = Mathf.Min(100, bulletSeed.MaxStackCount);
                        }
                        else
                        {
                            Debug.LogWarning($"{LogTag} 选取的物品 '{bulletSeed.DisplayName}' (ID:{bulletId}) 不可堆叠");
                            bulletSeed.StackCount = 1;
                        }

                        enemy.CharacterItem.Inventory.AddAndMerge(bulletSeed);
                        
                        gun.SetTargetBulletType(bulletId);
                        clone.Variables.SetInt("BulletCount".GetHashCode(), gun.Capacity);
                    }
                    else
                    {
                        Debug.LogError($"{LogTag} 无法实例化物品ID: {bulletId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"{LogTag} 未能为武器: {srcWeapon.DisplayName} 找到口径 '{caliber}' 合法的弹药ID");
                }
            }
        }

        private void CloneToSlot(Item src, CharacterMainControl enemy, string slotName)
        {
            if (src == null) return;
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, enemy.transform.position, Quaternion.identity);
            Item clone = go.GetComponent<Item>();
            if (clone != null)
            {
                clone.Detach();
                clone.AgentUtilities.ReleaseActiveAgent();
                enemy.PickupItem(clone);
            }
        }

        private static Item GetSlotItem(CharacterMainControl c, string slotName)
        {
            if (c?.CharacterItem?.Slots == null) return null;
            return c.CharacterItem.Slots.FirstOrDefault(s => s != null && s.Key == slotName)?.Content;
        }

        private static void ClearWeaponSlots(CharacterMainControl c)
        {
            string[] slots = { "PrimaryWeapon", "SecondaryWeapon", "MeleeWeapon" };
            foreach (string s in slots) GetSlotItem(c, s)?.DestroyTree();
        }

        private void CopyPlayerModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            InitializeReflection();
            if (!_hasCustomModelMod) { CopyVanillaFace(enemy, player); return; }
            try {
                Component playerHandler = player.GetComponent(_modelHandlerType);
                if (playerHandler == null) { CopyVanillaFace(enemy, player); return; }
                object bundleInfo = _bundleInfoField.GetValue(playerHandler);
                object modelInfo = _modelInfoProperty.GetValue(playerHandler);
                Component enemyHandler = enemy.GetComponent(_modelHandlerType) ?? enemy.gameObject.AddComponent(_modelHandlerType);
                _initMethod.Invoke(enemyHandler, new object[] { enemy, "AllAICharacters" });
                _loadMethod.Invoke(enemyHandler, new object[] { bundleInfo, modelInfo });
                HideEquipmentVisuals(enemy);
            } catch { CopyVanillaFace(enemy, player); }
        }

        private static void InitializeReflection()
        {
            if (_isReflectionInitialized) return;
            _isReflectionInitialized = true;
            try {
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _infoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");
                if (_modelHandlerType == null) return;
                _bundleInfoField = _modelHandlerType.GetField("_currentModelBundleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                _modelInfoProperty = _modelHandlerType.GetProperty("CurrentModelInfo", BindingFlags.Public | BindingFlags.Instance);
                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), typeof(string) });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _infoType });
                if (_loadMethod != null) _hasCustomModelMod = true;
            } catch { _hasCustomModelMod = false; }
        }

        private void CopyVanillaFace(CharacterMainControl enemy, CharacterMainControl player)
        {
            if (enemy?.characterModel?.CustomFace == null || player?.characterModel?.CustomFace == null) return;
            enemy.characterModel.SetFaceFromData(player.characterModel.CustomFace.ConvertToSaveData());
        }

        private void HideEquipmentVisuals(CharacterMainControl enemy)
        {
            if (enemy?.characterModel == null) return;
            Transform[] sockets = { enemy.characterModel.HelmatSocket, enemy.characterModel.ArmorSocket };
            foreach (var s in sockets) {
                if (s == null) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }

        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            Modify(enemy, StatModifier.Attributes.ViewDistance, 1.5f, true);
            Modify(enemy, StatModifier.Attributes.ViewAngle, 1.3f, true);
            Modify(enemy, StatModifier.Attributes.HearingAbility, 1.5f, true);
            Modify(enemy, StatModifier.Attributes.TurnSpeed, 1.5f, true);
            Modify(enemy, StatModifier.Attributes.AimTurnSpeed, 1.5f, true);
            
            Modify(enemy, StatModifier.Attributes.GunScatterMultiplier, 0.8f, true);
            
            Modify(enemy, AIFieldModifier.Fields.ShootCanMove, 1.0f, false);
            Modify(enemy, AIFieldModifier.Fields.CanDash, 1.0f, false);
        }
        
        // 改为只随机保留一件物品，其他全部销毁
        private void ClearAllDrops(CharacterMainControl c)
        {
            if (c?.CharacterItem == null) return;
            try
            {
                List<Item> allItems = new List<Item>();
                
                if (c.CharacterItem.Inventory != null)
                {
                    allItems.AddRange(c.CharacterItem.Inventory.Where(it => it != null));
                }
                
                if (c.CharacterItem.Slots != null)
                {
                    allItems.AddRange(c.CharacterItem.Slots.Where(s => s?.Content != null).Select(s => s.Content));
                }

                allItems = allItems.Distinct().ToList();

                if (allItems.Count > 0)
                {
                    int luckyIndex = UnityEngine.Random.Range(0, allItems.Count);
                    Item luckyItem = allItems[luckyIndex];

                    foreach (var it in allItems)
                    {
                        if (it != luckyItem)
                        {
                            it.DestroyTree();
                        }
                    }
                    
                    //Debug.Log($"{LogTag} 仿身泪滴已销毁 {allItems.Count - 1} 件物品，仅保留: {luckyItem.DisplayName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 掉落逻辑处理异常: " + e.Message);
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            if (character != null && _lootHook != null)
            {
                character.BeforeCharacterSpawnLootOnDead -= _lootHook;
            }

            _owner = null;
            _lootHook = null;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) => OnCleanup(character);
    }
}