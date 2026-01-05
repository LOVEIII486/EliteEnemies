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
        
        // 静态反射缓存
        private static bool _isReflectionInitialized = false;
        private static bool _hasCustomModelMod = false;

        private static Type _modelHandlerType;
        private static Type _bundleType;
        private static Type _infoType;

        private static FieldInfo _bundleInfoField;
        private static PropertyInfo _modelInfoProperty;
        private static MethodInfo _initMethod;
        private static MethodInfo _loadMethod;

        private CharacterMainControl _owner;
        private Action<DamageInfo> _lootHook;

        public override void OnEliteInitialized(CharacterMainControl enemy)
        {
            if (enemy == null || enemy.Health == null || enemy.CharacterItem == null) return;
            _owner = enemy;

            var player = CharacterMainControl.Main;
            if (player == null || player.CharacterItem == null) return;

            // 1. 克隆玩家装备 (主武器、头盔、护甲)
            Item srcPrimary = GetSlotItem(player, "PrimaryWeapon");
            Item srcHelmet = GetSlotItem(player, "Helmat");
            Item srcArmor = GetSlotItem(player, "Armor");

            ClearWeaponSlots(enemy);

            Item clonedPrimary = CloneViaInstantiate(srcPrimary, enemy.transform.position, enemy, "PrimaryWeapon");
            CloneViaInstantiate(srcHelmet, enemy.transform.position, enemy, "Helmat");
            CloneViaInstantiate(srcArmor, enemy.transform.position, enemy, "Armor");

            // 2. 注入无限弹药逻辑
            if (clonedPrimary != null)
            {
                enemy.ChangeHoldItem(clonedPrimary);
                var gunComponent = clonedPrimary.GetComponent<ItemSetting_Gun>();
                if (gunComponent != null)
                {
                    enemy.OnShootEvent += _ => RefillAmmoIfNeeded(clonedPrimary, gunComponent);
                }
            }

            // 3. 核心修复：复制玩家模型
            CopyPlayerModel(enemy, player);
            
            // 4. AI 强化
            EnhanceAIBehavior(enemy);
                
            // 5. 死亡清理
            _lootHook = delegate(DamageInfo _) { SafeClearAllDrops(_owner); };
            enemy.BeforeCharacterSpawnLootOnDead += _lootHook;
        }

        private void CopyPlayerModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            InitializeReflection();

            if (!_hasCustomModelMod)
            {
                CopyVanillaFace(enemy, player);
                return;
            }

            try
            {
                // 获取玩家的 ModelHandler
                Component playerHandler = player.GetComponent(_modelHandlerType);
                if (playerHandler == null)
                {
                    CopyVanillaFace(enemy, player);
                    return;
                }

                // 获取玩家当前模型数据
                object bundleInfo = _bundleInfoField.GetValue(playerHandler);
                object modelInfo = _modelInfoProperty.GetValue(playerHandler);

                if (bundleInfo == null || modelInfo == null)
                {
                    CopyVanillaFace(enemy, player);
                    return;
                }

                // 获取或添加敌人的 ModelHandler
                Component enemyHandler = enemy.GetComponent(_modelHandlerType);
                if (enemyHandler == null) enemyHandler = enemy.gameObject.AddComponent(_modelHandlerType);
                _initMethod.Invoke(enemyHandler, new object[] { enemy, "AllAICharacters" });
                // 跳过配置检查
                _loadMethod.Invoke(enemyHandler, new object[] { bundleInfo, modelInfo });

                // 隐藏精英怪原本的装备挂件，防止模型穿模
                HideEquipmentVisuals(enemy);
                //Debug.Log($"{LogTag} 仿生泪滴自定义模型复制成功");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 复制自定义模型失败，退回原版外观: {ex.Message}");
                CopyVanillaFace(enemy, player);
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

                if (_modelHandlerType == null || _bundleType == null || _infoType == null) return;

                _bundleInfoField = _modelHandlerType.GetField("_currentModelBundleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                _modelInfoProperty = _modelHandlerType.GetProperty("CurrentModelInfo", BindingFlags.Public | BindingFlags.Instance);

                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), typeof(string) });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _infoType });

                if (_bundleInfoField != null && _modelInfoProperty != null && _initMethod != null && _loadMethod != null)
                {
                    _hasCustomModelMod = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 反射初始化严重异常: {ex.Message}");
                _hasCustomModelMod = false;
            }
        }

        private void CopyVanillaFace(CharacterMainControl enemy, CharacterMainControl player)
        {
            try
            {
                if (enemy == null || enemy.characterModel == null || 
                    player == null || player.characterModel == null) return;

                var playerFaceInstance = player.characterModel.CustomFace;
                if (playerFaceInstance == null) return;
                
                var faceData = playerFaceInstance.ConvertToSaveData();
                enemy.characterModel.SetFaceFromData(faceData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 复制原版外观失败: {ex.Message}");
            }
        }

        private void HideEquipmentVisuals(CharacterMainControl enemy)
        {
            if (enemy?.characterModel == null) return;
            var sockets = new Transform[] { enemy.characterModel.HelmatSocket, enemy.characterModel.ArmorSocket };
            foreach (var s in sockets)
            {
                if (s == null) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }

        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            StatModifier.AddModifier(enemy, StatModifier.Attributes.TurnSpeed, 0.5f, ModifierType.PercentageMultiply);
            StatModifier.AddModifier(enemy, StatModifier.Attributes.AimTurnSpeed, 0.5f, ModifierType.PercentageMultiply);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ShootCanMove, 1f, false);
            AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.CanDash, 1f, false);
        }

        private void RefillAmmoIfNeeded(Item gunItem, ItemSetting_Gun gun)
        {
            if (gun.GetBulletCount() < 5)
            {
                Item newBullet = ItemAssetsCollection.InstantiateSync(gun.TargetBulletID);
                if (newBullet != null)
                {
                    newBullet.Initialize();
                    newBullet.StackCount = 100;
                    gunItem.Inventory.AddAndMerge(newBullet);
                }
            }
        }

        private static void ClearWeaponSlots(CharacterMainControl c)
        {
            string[] weaponSlots = { "PrimaryWeapon", "SecondaryWeapon", "MeleeWeapon" };
            foreach (string s in weaponSlots) GetSlotItem(c, s)?.DestroyTree();
        }

        private static Item CloneViaInstantiate(Item src, Vector3 pos, CharacterMainControl owner, string tag)
        {
            if (src == null) return null;
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, pos, Quaternion.identity);
            Item clone = go.GetComponent<Item>();
            if (clone != null)
            {
                clone.Detach();
                clone.AgentUtilities.ReleaseActiveAgent();
                owner.PickupItem(clone);
            }
            return clone;
        }

        private static Item GetSlotItem(CharacterMainControl c, string slotName) => 
            (c.CharacterItem?.Slots != null) ? c.CharacterItem.Slots[slotName].Content : null;

        private static void SafeClearAllDrops(CharacterMainControl c)
        {
            if (c?.CharacterItem == null) return;
            List<Item> buf = new List<Item>();
            if (c.CharacterItem.Inventory != null) buf.AddRange(c.CharacterItem.Inventory.Where(it => it != null));
            foreach (var s in c.CharacterItem.Slots) if (s?.Content != null) buf.Add(s.Content);
            foreach (var it in buf) it.DestroyTree();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character != null && _lootHook != null)
                character.BeforeCharacterSpawnLootOnDead -= _lootHook;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) => OnCleanup(character);
    }
}