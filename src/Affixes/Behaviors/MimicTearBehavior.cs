using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EliteEnemies.Modifiers;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemStatsSystem.Stats;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：仿生泪滴
    /// </summary>
    public class MimicTearBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
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
        // 装备隐藏：我们自己关渲染器（只影响这一只，不碰玩家的任何配置）。
        // 记下关过的渲染器与每个槽位上次处理过的 agent —— 清理时还原，换装时重扫。
        private const int EquipmentSlotCount = 5;
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private readonly ItemAgent[] _slotAgents = new ItemAgent[EquipmentSlotCount];

        public override void OnEliteInitialized(CharacterMainControl enemy)
        {
            if (enemy == null || enemy.CharacterItem == null) return;
            _owner = enemy;

            // ⚠ 这里**刻意**取 `CharacterMainControl.Main`——也就是**本机玩家**。
            //
            //   联机下的语义是「**只抄主机玩家的外观与装备**」（作者 2026-09-19 定）：
            //   本行为只会在主机上跑（客户端复制体没有行为组件），
            //   所以主机上的 `Main` 就是主机玩家——正是要抄的那个人。
            //
            //   ⇒ **不要**把它改成 `victim` 或"最近玩家"：那会让仿身泪滴随击杀者变化，
            //     与"抄主机玩家"这个设定不符，而且两端看到的外观会不一致。
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

                int bulletId = -1;

                var srcGun = srcWeapon.GetComponent<ItemSetting_Gun>();
                if (srcGun != null && srcGun.TargetBulletID > 0)
                {
                    bulletId = srcGun.TargetBulletID;
                }

                // 如果枪里没有，查保底映射表（原先是 `if (TryGetValue(...)) { }` 的空块写法，
                // 靠 out 参数的副作用赋值——读起来像"什么都没做"，实则不然）
                if (bulletId <= 0 && !string.IsNullOrEmpty(caliber))
                {
                    CaliberToBulletId.TryGetValue(caliber, out bulletId);
                }


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

        /// <summary>取某个槽位里的物品。用循环而不是 LINQ：本方法每次模仿要调 5 次，
        /// 而 `FirstOrDefault(λ)` 每次都会分配一个闭包。</summary>
        private static Item GetSlotItem(CharacterMainControl c, string slotName)
        {
            var slots = c?.CharacterItem?.Slots;
            if (slots == null) return null;

            foreach (var slot in slots)
            {
                if (slot != null && slot.Key == slotName) return slot.Content;
            }
            return null;
        }

        private static void ClearWeaponSlots(CharacterMainControl c)
        {
            string[] slots = { "PrimaryWeapon", "SecondaryWeapon", "MeleeWeapon" };
            foreach (string s in slots) GetSlotItem(c, s)?.DestroyTree();
        }

        /// <summary>
        /// 把玩家当前的模型复制到精英身上（仿身泪滴的核心外观）。
        ///
        /// <para>链路：读玩家 <c>ModelHandler</c> 的**当前模型**（私有字段 <c>_currentModelBundleInfo</c>
        /// + 公开属性 <c>CurrentModelInfo</c>）→ 给精英加/取一个 <c>ModelHandler</c> → 先
        /// <c>Initialize(enemy, targetTypeId)</c> → 再 <c>InitializeCustomModel(bundle, info)</c> 显式套用。</para>
        ///
        /// <para><b>为什么读私有字段而不是用 DCM 的公开 API</b>（2026-09-18 查过 DCM 仓库）：
        /// DCM 有公开的 <c>ModelManager.FindModelByID(id, out bundle, out info)</c> 与
        /// <c>ModelHandler.CurrentModelInfo</c>（后者能拿到 <c>ModelInfo.ModelID</c>），看起来可以
        /// "按 ID 重新解析"。但 <c>ModelBundleInfo</c> **没有公开访问口**（`CurrentModelDirectory`
        /// 只给目录字符串），而 DCM 内部会按目标类型做 <c>CreateFilteredCopy</c>——按 ID 重新解析
        /// 拿到的可能是**与玩家实际使用不同的那个实例**。所以这里坚持"原样复制玩家手上的那一对"，
        /// 代价是必须碰一个私有字段（A2 例外，见 <see cref="InitializeReflection"/>）。</para>
        /// </summary>
        private void CopyPlayerModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            InitializeReflection();

            // 分两路，目标都是"**完全**复制玩家现在长什么样"：
            //   · 玩家用了 DCM 自定义模型 → 走 DCM，**只给这一只敌人**换模型（不影响别的敌人）
            //   · 玩家用的是原版模型       → 连**原版身体模型**一起复制
            //     （只复制脸和装备是不够的：那会让拾荒者的身体顶着玩家的装备）
            if (_hasCustomModelMod)
            {
                try
                {
                    Component playerHandler = player.GetComponent(_modelHandlerType);
                    object bundleInfo = playerHandler != null ? _bundleInfoField.GetValue(playerHandler) : null;
                    object modelInfo = playerHandler != null ? _modelInfoProperty.GetValue(playerHandler) : null;

                    if (bundleInfo != null && modelInfo != null)
                    {
                        Component enemyHandler = enemy.GetComponent(_modelHandlerType) ?? enemy.gameObject.AddComponent(_modelHandlerType);
                        _initMethod.Invoke(enemyHandler, new object[] { enemy, MimicTargetTypeId });
                        _loadMethod.Invoke(enemyHandler, new object[] { bundleInfo, modelInfo });
                        ApplyEquipmentHiding(enemy);   // 立刻藏，不必等下一帧的 OnUpdate
                        return;   // 模型交给 DCM；装备隐藏仍是我们自己做的（见 ApplyEquipmentHiding）
                    }
                }
                catch (Exception ex)
                {
                    // 原先这里是 `catch { ... }`——**静默**回落到原版。现在报出来：
                    // 模型复制失败是玩家看得见的功能缺失，不该只在日志里留白。
                    Debug.LogError($"{LogTag} 用 DCM 复制玩家模型失败，回落到原版模型: {ex}");
                }
            }

            CopyVanillaModel(enemy, player);
        }

        /// <summary>
        /// 复制玩家的**原版**身体模型（玩家没装 DCM 模型、或 DCM 那条路失败时走这里）。
        ///
        /// <para>用游戏自己的换模型口子 <c>CharacterMainControl.SetCharacterModel</c>
        /// （<c>CharacterMainControl.cs:1923</c>）：它会先把当前武器**存下**（<c>StoreHoldWeaponBeforeUse</c>）、
        /// 换完再**装回**（<c>SwitchToWeaponBeforeUse</c>），所以本行为刚克隆给敌人的武器不会被弄丢；
        /// 它同时会重挂模型插槽、按新模型半径更新根碰撞体。<c>CharacterCreator.cs:13-15</c> 用的是同一对 API。</para>
        /// </summary>
        private void CopyVanillaModel(CharacterMainControl enemy, CharacterMainControl player)
        {
            try
            {
                var prefab = player != null ? player.defaultCharacterModelPrefab : null;
                if (prefab == null || enemy == null)
                {
                    CopyVanillaFace(enemy, player);
                    return;
                }

                var modelInstance = UnityEngine.Object.Instantiate(prefab);
                enemy.SetCharacterModel(modelInstance);
                CopyVanillaFace(enemy, player);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 复制玩家原版模型失败，至少复制脸型: {ex}");
                CopyVanillaFace(enemy, player);
            }
        }

        /// <summary>
        /// 传给 <c>ModelHandler.Initialize</c> 的目标类型 ID：**本模组专有的 <c>extension:</c> 类型**。
        ///
        /// <para>为什么不用 DCM 的 <c>built-in:AICharacter_*</c>（"所有 AI 角色"）：那条路会让 DCM 的
        /// **模型优先级表**按"所有 AI"给这只敌人解析模型，而我们要的是"只复制玩家的模型"。
        /// 专属的 <c>extension:</c> ID 让这只敌人自成一档，不落到任何玩家配置上。</para>
        ///
        /// <para>合法形式取自 DCM 源码：<c>ModelTargetType.ExtensionPrefix = "extension:"</c>，
        /// 而 <c>IsValid</c> 只检查前缀（<c>DuckovCustomModel.Core/Data/ModelTargetType.cs:8,44-46</c>）
        /// ——**不需要向 DCM 注册**这个类型也能用。</para>
        /// </summary>
        private const string MimicTargetTypeId = "extension:EliteEnemies_MimicTear";

        /// <summary>
        /// **一律**隐藏仿生体的装备外观——只针对这一只敌人，且**不碰玩家的任何配置**。
        ///
        /// <para>范围与取法照抄 DCM 隐藏装备时用的那一套（这样两边"看不见的装备"是同一批东西）：
        /// 五个槽位 armor / helmat / backpack / faceMask / headset（<c>ModelHandler.cs:167-171</c>），
        /// 槽位里的 agent 用 <c>slot.Content.ActiveAgent</c>（它自己的
        /// <c>GetSlotActiveAgent</c>，<c>ModelHandler.cs:906-910</c>）。</para>
        ///
        /// <para><b>但只关渲染器：不动 agent、也不写 DCM 的配置。</b>
        /// ① DCM 的 <c>LateUpdate</c> 在"不需要隐藏"时会**主动重新启用 agent**
        /// （<c>ModelHandler.cs:174-180</c>）——我们若动 agent 就是每帧互相打架，而它**从不碰渲染器**；
        /// ② <c>HideEquipmentConfig</c> 是**玩家的**配置对象，写它可能被 DCM 保存进
        /// <c>HideEquipmentConfig.json</c>，而这个词条的效果应当是**临时的、只对这一只敌人**。
        /// 装备照样提供属性（属性在 <c>Item</c> 上，不在 agent 上），只是看不见。</para>
        ///
        /// <para>每帧重扫（只做几个引用比较）：换装时 agent 会变；记录过的渲染器若被谁重新启用也会被按回去。</para>
        /// </summary>
        private void ApplyEquipmentHiding(CharacterMainControl enemy)
        {
            var eq = enemy != null ? enemy.EquipmentController : null;
            if (eq == null) return;

            CheckSlot(eq.armorSlot, 0);
            CheckSlot(eq.helmatSlot, 1);
            CheckSlot(eq.backpackSlot, 2);
            CheckSlot(eq.faceMaskSlot, 3);
            CheckSlot(eq.headsetSlot, 4);

            for (int i = 0; i < _hiddenRenderers.Count; i++)
            {
                var r = _hiddenRenderers[i];
                if (r != null && r.enabled) r.enabled = false;
            }
        }

        private void CheckSlot(Slot slot, int index)
        {
            ItemAgent agent = slot != null && slot.Content != null ? slot.Content.ActiveAgent : null;
            if (agent == _slotAgents[index]) return;   // 该槽位没换装

            _slotAgents[index] = agent;
            if (agent == null) return;

            foreach (var r in agent.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                _hiddenRenderers.Add(r);
            }
        }

        /// <summary>清理时还原被关掉的渲染器（与 <see cref="ApplyEquipmentHiding"/> 配对）。</summary>
        private void RestoreEquipmentVisuals()
        {
            for (int i = 0; i < _hiddenRenderers.Count; i++)
            {
                var r = _hiddenRenderers[i];
                if (r != null) r.enabled = true;
            }
            _hiddenRenderers.Clear();

            for (int i = 0; i < _slotAgents.Length; i++) _slotAgents[i] = null;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime) => ApplyEquipmentHiding(character);

        private static void InitializeReflection()
        {
            if (_isReflectionInitialized) return;
            _isReflectionInitialized = true;

            // 【为什么这里的反射必须保留】属于 03 篇 §2.3 的 A2（无法编译期引用的可选依赖）：
            // DuckovCustomModel 是**可选**的第三方模组，其程序集不在本工程引用列表里，
            // 硬引用会让没装它的玩家连 EliteEnemies 都加载不了。
            //
            // 下面每个成员都核对过（2026-09-18 对照 DCM 仓库 main 分支的
            // DuckovCustomModel/MonoBehaviours/ModelHandler.cs 与 Core/Data/*.cs）：
            //   private ModelBundleInfo? _currentModelBundleInfo      ← 只有这一个是 private
            //   public  ModelInfo? CurrentModelInfo { get; private set; }
            //   public  void Initialize(CharacterMainControl characterMainControl, string targetTypeId)
            //   public  void InitializeCustomModel(ModelBundleInfo modelBundleInfo, ModelInfo modelInfo)
            // 为什么不能只用公开 API（DCM 有 ModelManager.FindModelByID 与 ModelInfo.ModelID）：
            // 见 CopyPlayerModel 的注释——ModelBundleInfo 没有公开访问口，而按 ID 重新解析
            // 可能拿到与玩家实际使用不同的实例（DCM 内部有 CreateFilteredCopy）。
            //
            // ⚠ Initialize 必须写成带 Type[] 的形式：ModelHandler 另有一个已标
            // [Obsolete] 的 Initialize(CharacterMainControl, ModelTarget) 重载，
            // 只传方法名会抛 AmbiguousMatchException。
            try
            {
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _infoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");

                if (_modelHandlerType == null || _bundleType == null || _infoType == null)
                {
                    if (!IsDuckovCustomModelLoaded())
                    {
                        // 没装 DCM——这是 A2 的**正常形态**，静默回落到原版脸型，不报错
                        return;
                    }

                    Debug.LogError($"{LogTag} 检测到已加载 DuckovCustomModel，但解析它的类型失败" +
                                   $"（ModelHandler={_modelHandlerType != null} / ModelBundleInfo={_bundleType != null} / " +
                                   $"ModelInfo={_infoType != null}）——DCM 大概改过程序集名或命名空间。" +
                                   "仿身泪滴的模型复制将失效，只复制原版脸型。");
                    return;
                }

                _bundleInfoField = _modelHandlerType.GetField("_currentModelBundleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                _modelInfoProperty = _modelHandlerType.GetProperty("CurrentModelInfo", BindingFlags.Public | BindingFlags.Instance);
                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), typeof(string) });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _infoType });

                if (_bundleInfoField == null || _modelInfoProperty == null || _initMethod == null || _loadMethod == null)
                {
                    Debug.LogError($"{LogTag} DuckovCustomModel 的 ModelHandler 成员与预期不符" +
                                   $"（_currentModelBundleInfo={_bundleInfoField != null} / CurrentModelInfo={_modelInfoProperty != null} / " +
                                   $"Initialize={_initMethod != null} / InitializeCustomModel={_loadMethod != null}）——" +
                                   "多半是 DCM 升级改了成员名（它 v1.10 做过一次大规模 API 更名，官方还发了 " +
                                   "docs/OBSOLETE_APIS_v1.10.0.md）。仿身泪滴的模型复制将失效，" +
                                   "请对照 DCM 仓库的 ModelHandler.cs 复核本文件。");
                    return;
                }

                _hasCustomModelMod = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 解析 DuckovCustomModel 的反射信息失败，仿身泪滴只复制原版脸型: {ex}");
            }
        }

        /// <summary>进程里有没有加载 DuckovCustomModel（用来区分"没装"与"装了但对不上"）。</summary>
        private static bool IsDuckovCustomModelLoaded()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string name = assemblies[i].GetName().Name;
                if (name != null && name.StartsWith("DuckovCustomModel", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void CopyVanillaFace(CharacterMainControl enemy, CharacterMainControl player)
        {
            if (enemy?.characterModel?.CustomFace == null || player?.characterModel?.CustomFace == null) return;
            enemy.characterModel.SetFaceFromData(player.characterModel.CustomFace.ConvertToSaveData());
        }


        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            Modify(enemy, StatKeys.ViewDistance, 1.5f, true);
            Modify(enemy, StatKeys.ViewAngle, 1.3f, true);
            Modify(enemy, StatKeys.HearingAbility, 1.5f, true);
            Modify(enemy, StatKeys.TurnSpeed, 1.5f, true);
            Modify(enemy, StatKeys.AimTurnSpeed, 1.5f, true);
            
            Modify(enemy, StatKeys.GunScatterMultiplier, 0.8f, true);
            
            ModifyAI(enemy, AIFields.ShootCanMove, true);
            ModifyAI(enemy, AIFields.CanDash, true);
        }
        
        // 改为只随机保留一件物品，其他全部销毁
        private void ClearAllDrops(CharacterMainControl c)
        {
            if (c?.CharacterItem == null) return;
            try
            {
                // 用循环 + HashSet 去重，替掉 `Where/Select/Distinct/ToList` 那串 LINQ：
                // 每次模仿死亡都要跑一遍，而那串链会分配 4 个中间集合与若干闭包。
                var allItems = new List<Item>();
                var seen = new HashSet<Item>();

                var inventory = c.CharacterItem.Inventory;
                if (inventory != null)
                {
                    foreach (var it in inventory)
                    {
                        if (it != null && seen.Add(it)) allItems.Add(it);
                    }
                }

                var slots = c.CharacterItem.Slots;
                if (slots != null)
                {
                    foreach (var slot in slots)
                    {
                        Item content = slot != null ? slot.Content : null;
                        if (content != null && seen.Add(content)) allItems.Add(content);
                    }
                }

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
                    
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 掉落逻辑处理异常: " + e.Message);
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            RestoreEquipmentVisuals();   // 还原被隐藏的装备（与 ApplyEquipmentHiding 配对）
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