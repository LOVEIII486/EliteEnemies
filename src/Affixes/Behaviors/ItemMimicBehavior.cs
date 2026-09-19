using System;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using NodeCanvas.Framework;
using EliteEnemies.DebugTools;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【物品拟态】—— **测试词条**（key = <c>ItemMimic</c>，名字是工作名）。
    ///
    /// <para>与【拟态】<see cref="MimicBehavior"/> 是同一个思路的两个变体，差别只在**伪装体**：
    /// 拟态伪装成一只补给箱，本词条伪装成**躺在地上的一件物品**。箱子是玩家「有理由去开」的东西，
    /// 打久了就认得出来；物品是**路过时有动机主动去捡**的东西，触发点从「开箱」变成「贪心去捡」。</para>
    ///
    /// <para><b>触发</b>：玩家按 E 拾取它、或对它造成伤害时，敌人现形并突袭。
    /// 伪装期间敌人隐藏模型、压制 AI、压住血条，并**每帧对齐到物品的位置**。</para>
    ///
    /// <para>⚠ <b>本行为不阻止玩家拾取——玩家会真的拿到那件物品。</b>
    /// 这不是偷懒，是读源码后的结论：<c>InteractableBase.StartInteract</c> 里
    /// <c>OnInteractStartEvent</c>（<c>:293</c>）与真正拾取的 <c>OnInteractStart</c>（<c>:297</c>）
    /// 之间**没有任何重判**，而 Unity 的 <c>Object.Destroy</c> 延迟到帧末、之后 <c>item == null</c>
    /// 不会立刻为真（游戏自己就留了证据：<c>ItemTreeExtensions.DestroyTree</c> 之外还有一个
    /// 单独的 <c>DestroyTreeImmediate</c>）。硬拦的后果更糟——<c>:64</c> 的
    /// <c>PickupItem</c> 仍会成功，走 <c>ItemStatsSystem</c> 的
    /// <c>ReleaseActiveAgent → Detach → SendToPlayerCharacterInventory</c> 把一个**已排进销毁队列**
    /// 的物品塞进玩家背包，帧末 GameObject 被销毁 ⇒ 背包里留下点不开的空条目。
    /// ⇒ 索性把「捡到它」当作**咬钩的报酬**（现拟态也在送：开诱饵箱会拿到 10 件诱饵物品）。</para>
    ///
    /// <para>⚠ <b>若日后要求「物品必须消失」</b>：唯一干净的口子是 Harmony prefix 打在
    /// <c>InteractableBase.StartInteract</c>（<c>public bool</c>，<c>:267</c>）上返回 <c>false</c>——
    /// 那是**唯一能在 <c>:293</c> 之前拦下**的位置。**不要**去打 <c>InteractablePickup.IsInteractable</c>：
    /// 那会让 <c>:281</c> 的 <c>CheckInteractable()</c> 失败、连 <c>:293</c> 都走不到，
    /// E 键就完全触发不了伏击了。</para>
    ///
    /// <para>本行为与 <see cref="MimicBehavior"/> 有大量**同源的坑**（血条与 FOW 抢 flag、
    /// 预设整批覆写 AI 字段、协程在这条链路上不恢复……），那些处理是**照搬**过来的——
    /// 每一处的理由都写在原文件里，这里只留简短指引，**改动前请先读 <c>MimicBehavior.cs</c> 的对应注释**。</para>
    /// </summary>
    public class ItemMimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "ItemMimic";
        private const string LogTag = "[EliteEnemies.ItemMimic]";

        /// <summary>
        /// 伪装物的候选物品池（玩家看见就有动机去捡的小件）。
        ///
        /// <para>⚠ 这几个 ID 有没有 **3D 世界模型**，代码里判不了——<c>..\Docs\ItemDatabase原版.xlsx</c>
        /// 只有 ID/名称/数值/标签，**没有模型列**。运行时判据是
        /// <c>ItemAssetsCollection.GetPrefab(id).ItemGraphic != null &amp;&amp; !prefab.useSpriteForPickup</c>
        /// （游戏侧 <c>InteractablePickup.CreateGraphic</c> 就是这么选的）。没有 3D 模型的物品会退化成
        /// **2D 精灵立牌**——实机看到纸片样式就说明这个 ID 要换掉。</para>
        /// </summary>
        private static readonly int[] DisguiseItemIds = { 963, 993, 137, 326 };

        private Item _item;
        private DuckovItemAgent _agent;
        private InteractablePickup _pickup;

        /// <summary>
        /// 交互回调的**具名委托**，退订时必须用同一个实例。
        /// <para>⚠ 不能像 <c>MimicBehavior</c> 那样在 <c>AddListener</c> 里直接写 lambda：
        /// 那个写法**退订不掉**（方法组每次转换都产生新的委托对象）。
        /// 拟态不需要退订（它没有会残留到玩家身上的东西），本行为需要。</para>
        /// </summary>
        private UnityEngine.Events.UnityAction<CharacterMainControl, InteractableBase> _interactHandler;

        private AICharacterController _aiController;
        private CharacterSoundMaker _soundMaker;
        private GraphOwner _brain;

        private List<Renderer> _cachedRenderers;

        /// <summary>角色身上的碰撞体缓存，见 <see cref="ReignoreItemCollision"/>。</summary>
        private Collider[] _cachedCharacterColliders;

        /// <summary>下一次重扫碰撞体列表的时刻（<see cref="Time.time"/> 口径）。</summary>
        private float _nextColliderRescanTime;

        /// <summary>碰撞体列表的重扫冷却（秒）。与 <c>MimicBehavior.ColliderRescanCooldown</c> 同一取舍。</summary>
        private const float ColliderRescanCooldown = 0.5f;

        private bool _hasTriggered = false;
        private bool _isTriggering = false;

        /// <summary>位置同步的死区（平方）。物品没动就不写，稳态下每帧只是一次比较。</summary>
        private const float SyncThresholdSqr = 0.001f;

        /// <summary>拾取到伏击之间的延迟（秒，**真实时间**，见 <see cref="OnPlayerInteracted"/>）。</summary>
        private const float AmbushDelay = 1.2f;

        /// <summary>待触发的伏击（到点后由 <see cref="UpdatePendingAmbush"/> 触发）。</summary>
        private CharacterMainControl _pendingTarget;
        private float _ambushDueTime;

        private float _cachedSightDist, _cachedHearing, _cachedSightAngle, _cachedTraceDist;
        private bool _cachedCanTalk;
        private bool _isSensorySuppressed = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isTriggering = false;

            // 精英自身的引用一律从框架上下文取（每个敌人只解析一次）。同 MimicBehavior.cs:74-78。
            _aiController = Ctx?.Ai;
            _soundMaker = Ctx?.SoundMaker;

            // ⚠ GraphOwner 是 NodeCanvas（第三方）的类型，反编译树里没有它，拿不到比 GetComponent
            //    更权威的取法；这两次查找保留（只在初始化跑一次，不在热路径）。
            if (_aiController != null)
            {
                _brain = _aiController.GetComponent<GraphOwner>();
                if (_brain == null) _brain = _aiController.GetComponentInParent<GraphOwner>();
            }

            InitRendererCache(character);

            SpawnDisguiseItem(character);

            SetDisguiseState(character, true);

            ForceHideVisuals();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null) return;

            UpdatePendingAmbush(character);

            // ⚠ **伏击可能就在上面那句里生效了**（它会把 `_hasTriggered` 置真并揭示敌人）。
            // 此时若继续往下走，同一帧就会把敌人**重新藏回去、AI 重新压制回去**
            // ⇒ 表现是"玩家明明捡了，敌人却不现身、也不攻击"。
            // 开枪那条路没这个问题：`OnDamaged` 在 `OnUpdate` 外面触发，下一帧一进门就被最上面那句挡住。
            if (_hasTriggered) return;

            character.Hide();
            ForceHideVisuals();

            // ⚠ **每帧重挂**"物品 ↔ 角色互不碰撞"。Unity 在碰撞体被**重新启用**时会清掉
            //    `Physics.IgnoreCollision` 的对，而角色的碰撞体会被反复启停（FOW 显隐等）。
            //    这里还有个本行为特有的作用：物品的 collider 是**非 trigger** 的（游戏侧
            //    `InteractableBase.Awake` 把它归到 "Interactable" 层），角色自己的胶囊体
            //    不该被自己的伪装物顶住。详见 ReignoreItemCollision 的注释。
            ReignoreItemCollision(character);

            // 压制血条——**"闪一下"的根治点**，见 MimicBehavior.SuppressHealthBar 的注释。
            SuppressHealthBar(character);

            // 持续压制 AI —— 为什么必须"每帧"而不是"压一次"，见 MimicBehavior.EnsureAISuppressed。
            EnsureAISuppressed(character);

            // 位置同步：角色始终站在物品上，玩家才能直接射它提前击杀。
            SyncPositionToItem(character);
        }

        /// <summary>
        /// 把角色对齐到伪装物（带死区：物品没动就不写，稳态下每帧只是一次比较）。
        ///
        /// <para><b>⚠ 必须走 <c>character.SetPosition()</c>，不要直接写 <c>transform.position</c>。</b>
        /// 后者绕过贴地约束与 ECM2 的同步，是原先抖动/穿模的来源——
        /// 完整理由见 <c>MimicBehavior.SyncPositionToBox</c> 的注释。</para>
        ///
        /// <para>⚠ <b>这里与拟态有一处关键不同：垂直偏移为 0。</b>拟态那边
        /// <c>_followOffset = Vector3.up * 0.15f</c> 是为了让角色"站在箱子上"——箱子有体积。
        /// 物品是**平的**，角色没法站在它上面，所以对齐目标就是物品自身的轴心。</para>
        /// </summary>
        private void SyncPositionToItem(CharacterMainControl character)
        {
            if (_agent == null) return;

            Vector3 targetPos = _agent.transform.position;
            if (Vector3.SqrMagnitude(character.transform.position - targetPos) <= SyncThresholdSqr) return;

            character.SetPosition(targetPos);
            character.transform.rotation = _agent.transform.rotation;
        }

        /// <summary>被攻击时触发埋伏（与拟态同一条路，见 <c>MimicBehavior.OnDamaged</c>）。</summary>
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasTriggered) return;

            CharacterMainControl attacker = damageInfo.fromCharacter;
            TriggerAmbush(character, attacker);
        }

        /// <summary>
        /// 把一件物品丢在角色脚下当作伪装物。
        ///
        /// <para>走的是游戏自己的掉落路径 <c>ItemExtensions.Drop</c>（<c>ItemExtensions.cs:89-121</c>），
        /// 它一次把六件事做完：地面视觉（<c>InteractablePickup.CreateGraphic</c>）、交互标记、
        /// 交互提示名、层级（<c>InteractableBase.Awake</c> 归到 "Interactable" 层）、
        /// 场景搬运（<c>:107-110</c>）、落地朝向。**不要自己复刻其中任何一件**——
        /// 本工程在"自造生成物"上反复栽过（箱子漂移、Obscurer）。</para>
        ///
        /// <para>⚠ <b><c>createRigidbody: false</c> 是刻意的。</b>传 <c>true</c> 会走
        /// <c>InteractablePickup.Throw()</c> 给它一个初速度——那件物品会**从敌人脚下飞出去**，
        /// 而伪装物必须待在原地。游戏自己的地面物品点 <c>LootSpawner</c> 用的也是
        /// <c>false</c>（<c>LootSpawner.cs:174</c>），语义完全一致：**放在地上，不是抛出去**。</para>
        ///
        /// <para>⚠ <b>不需要再补一次 <c>MultiSceneCore.MoveToActiveWithScene</c>。</b>拟态那边要补，
        /// 是因为它直接 <c>Instantiate</c> 预制体、绕过了官方建箱路径；<c>Item.Drop</c> 自己就带这一步
        /// （<c>ItemExtensions.cs:107-110</c>）。</para>
        /// </summary>
        private void SpawnDisguiseItem(CharacterMainControl character)
        {
            if (character == null) return;

            int itemId = DisguiseItemIds[UnityEngine.Random.Range(0, DisguiseItemIds.Length)];

            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                Debug.LogError($"{LogTag} 伪装物 itemID={itemId} 实例化失败，这只敌人将只隐藏不伪装");
                return;
            }

            DuckovItemAgent agent = item.Drop(character.transform.position, false, Vector3.forward, 360f);
            if (agent == null)
            {
                // 物品没落成：留着它也是一份悬空数据，直接收掉。
                Debug.LogError($"{LogTag} 伪装物 itemID={itemId} 掉落失败（agent 为 null），这只敌人将只隐藏不伪装");
                if (!item.IsBeingDestroyed) item.DestroyTree();
                return;
            }

            _item = item;
            _agent = agent;
            _pickup = agent.GetComponent<InteractablePickup>();

            ReignoreItemCollision(character);

            // 绑定交互：玩家按 E 捡它的那一瞬间就是伏击点。
            //
            // ⚠ 拾取是**一帧内完成**的（`InteractablePickup.OnInteractStart` 里同步调
            //    `PickupItem` 然后 `StopInteract`，没有 interactTime 阶段），而且
            //    `OnInteractStartEvent` 早于它——所以我们不需要"进度条到点"之类的处理。
            if (_pickup != null)
            {
                _interactHandler = OnPlayerInteracted;
                _pickup.OnInteractStartEvent.AddListener(_interactHandler);

                if (DebugSwitch.Enabled)
                {
                    Debug.Log($"{LogTag} 诊断：伪装物已生成并绑定交互（{character.name}，物品={itemId}）");
                }
            }
            else
            {
                // 这一条若出现，"按 E 不触发伏击"就有了直接答案：agent 上根本没有 InteractablePickup。
                // （只有回退路径 `GameplayDataSettings.Prefabs.PickupAgentPrefab` 才保证带它，
                //   见 ItemExtensions.cs:29-41；物品自带 "Pickup" agent 时不一定带。）
                Debug.LogError($"{LogTag} 伪装物 itemID={itemId} 的 agent 上没有 InteractablePickup，" +
                               "**交互监听挂不上** ⇒ 按 E 永远不会触发伏击（受伤那条路不受影响）");
            }
        }

        /// <summary>
        /// 让伪装物与角色身上的每个碰撞体互不碰撞（形状与理由照搬 <c>MimicBehavior.ReignoreBoxCollision</c>）。
        ///
        /// <para>⚠ <b>重挂必须每帧做</b>：Unity 在碰撞体被**重新启用**时会清掉
        /// <c>Physics.IgnoreCollision</c> 的**对**，而角色的碰撞体会被反复启停。
        /// 只挂一次的后果就是拟态那边实测到的"箱子漂移"——这里换成物品同样成立
        /// （物品虽是非运动学的静态 collider，推不走，但角色的胶囊体会被自己的伪装物**顶住**）。</para>
        ///
        /// <para>取列表不必每帧：缓存 + 0.5 秒冷却重扫，与 <c>MimicBehavior</c> 同一范式。
        /// 缓存不会漏掉"碰撞体被反复启停"——Unity 清掉的是对，不是组件，引用始终有效；
        /// 唯一可能漏的是**新增**的碰撞体（换模型），故留重扫窗口。</para>
        /// </summary>
        private void ReignoreItemCollision(CharacterMainControl character)
        {
            Collider itemCollider = GetItemCollider();
            if (itemCollider == null || character == null) return;

            if (_cachedCharacterColliders == null || Time.time >= _nextColliderRescanTime)
            {
                _cachedCharacterColliders = character.GetComponentsInChildren<Collider>(true);
                _nextColliderRescanTime = Time.time + ColliderRescanCooldown;
            }

            var colliders = _cachedCharacterColliders;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) Physics.IgnoreCollision(itemCollider, colliders[i], true);
            }
        }

        /// <summary>
        /// 伪装物的碰撞体：优先取 <see cref="InteractableBase.interactCollider"/>（游戏自己认的那一个），
        /// 没有再退到 agent 上的任意 Collider。
        /// </summary>
        private Collider GetItemCollider()
        {
            if (_pickup != null && _pickup.interactCollider != null) return _pickup.interactCollider;
            if (_agent != null) return _agent.GetComponent<Collider>();
            return null;
        }

        private void TriggerAmbush(CharacterMainControl character, CharacterMainControl initialTarget = null)
        {
            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 诊断：进入 TriggerAmbush（_hasTriggered={_hasTriggered} " +
                          $"character={(character != null)} target={(initialTarget != null)}）");
            }

            if (_hasTriggered) return;
            _hasTriggered = true;

            ClearDisguiseItem();

            SetDisguiseState(character, false);
            ForceShowVisuals();

            // ⚠ 顺序有讲究：**必须先放回血条闸门，再 Show()**——
            // `Show()` 里第一句就是 `health?.RequestHealthBar()`（`CharacterMainControl.cs:2557-2559`），
            // 而那正是我们要它把血条**建出来**的那一次请求；闸门还压着的话这次请求会被挡掉。
            RestoreHealthBar(character);
            character.Show();

            if (initialTarget != null && _aiController != null)
            {
                FaceTarget(character, initialTarget);
                _aiController.SetTarget(initialTarget.transform);
                _aiController.searchedEnemy = initialTarget.mainDamageReceiver;
                _aiController.alert = true;
            }
        }

        /// <summary>
        /// 收掉还在地上的伪装物。
        ///
        /// <para>⚠ <b>玩家已经把它捡走时绝不能销毁</b>——<c>Item.Detach()</c> 会把它
        /// **从玩家背包里拽出来**（<c>Item.cs:797-801</c> → <c>InInventory?.RemoveItem(this)</c>）。
        /// 判据用 <c>InInventory == null</c>（<c>Item.cs:390</c>，public）：在地面的物品不属于任何背包。</para>
        ///
        /// <para>清理范式是游戏自己的：<c>Detach()</c> 后 <c>DestroyTree()</c>
        /// （<c>ItemTreeExtensions.cs:117-131</c>）。销毁 Item 会连带销毁 agent——
        /// <c>Item.OnDestroy</c>（<c>Item.cs:1118-1124</c>）里会 <c>Detach()</c> +
        /// <c>agentUtilities.ReleaseActiveAgent()</c>，后者销毁的就是 agent 那个 GameObject。
        /// **不要反过来只销毁 agent**：那会留下一个无渲染、无交互的 Item 数据物体。</para>
        ///
        /// <para>本方法必须**可重复调用**（<see cref="OnCleanup"/> 可能被走到两次）。</para>
        /// </summary>
        private void ClearDisguiseItem()
        {
            if (_pickup != null && _interactHandler != null)
            {
                _pickup.OnInteractStartEvent.RemoveListener(_interactHandler);
            }

            if (_item != null && !_item.IsBeingDestroyed && _item.InInventory == null)
            {
                _item.Detach();
                _item.DestroyTree();
            }

            _item = null;
            _agent = null;
            _pickup = null;
        }

        private void SuppressAIHard(AICharacterController ai)
        {
            // ⚠ 先读后写：本方法现在**每帧**都会被 EnsureAISuppressed 调到，
            //    稳态下应当是"几次比较、零写入"。
            if (ai.sightDistance != 0f) ai.sightDistance = 0f;
            if (ai.sightAngle != 0f) ai.sightAngle = 0f;
            if (ai.hearingAbility != 0f) ai.hearingAbility = 0f;
            if (ai.forceTracePlayerDistance != 0f) ai.forceTracePlayerDistance = 0f;

            if (ai.searchedEnemy != null || ai.aimTarget != null)
            {
                ai.SetTarget(null);
                ai.searchedEnemy = null;
                ai.alert = false;
                ai.StopMove();
            }
        }

        /// <summary>
        /// 伪装期间的 AI 压制。**必须每帧做**——三条理由（引用可能晚到 / 游戏会把字段覆写回去 /
        /// 行为树可能晚启动或被重新启动）逐条写在 <c>MimicBehavior.EnsureAISuppressed</c> 的注释里，
        /// 这里完全同源，改动前请先读那一段。
        /// </summary>
        private void EnsureAISuppressed(CharacterMainControl character)
        {
            // ① 惰性重取引用（初始化时可能还没有）
            if (_aiController == null)
            {
                _aiController = character.aiCharacterController;
                if (_aiController == null) return;
            }

            if (_brain == null)
            {
                _brain = _aiController.GetComponent<GraphOwner>();
                if (_brain == null) _brain = _aiController.GetComponentInParent<GraphOwner>();
            }

            if (_soundMaker == null) _soundMaker = Ctx?.SoundMaker;

            // ② 首次补齐"一次性"压制（缓存原值 + 收枪 + 闭嘴），见 SetSensorySuppression
            if (!_isSensorySuppressed) SetSensorySuppression(_aiController, true);

            // ③ 每帧重压（会被预设覆写的那几个）
            SuppressAIHard(_aiController);
            if (_aiController.canTalk) _aiController.canTalk = false;
            if (_soundMaker != null && _soundMaker.enabled) _soundMaker.enabled = false;

            // ④ 大脑：晚启动、或被重新启动，都补一次暂停。
            if (_brain != null && _brain.isRunning && !_brain.isPaused) _brain.PauseBehaviour();
        }

        private void SetDisguiseState(CharacterMainControl character, bool isDisguised)
        {
            if (_aiController == null) return;

            if (isDisguised)
            {
                // 暂停大脑逻辑
                if (_brain != null && _brain.isRunning) _brain.PauseBehaviour();
                SetSensorySuppression(_aiController, true);
                if (_soundMaker != null) _soundMaker.enabled = false;

                // 取消当前寻路，防止它在被强刷坐标时尝试回正位置导致抖动。
                // 游戏不用 NavMeshAgent（AI 走 A*），标准调用是 StopMove()
                // （`AICharacterController.cs:744`）——理由见 MimicBehavior.cs:435-438。
                _aiController.StopMove();
            }
            else
            {
                SetSensorySuppression(_aiController, false);
                if (_brain != null && _brain.isPaused) _brain.StartBehaviour();
                if (_soundMaker != null) _soundMaker.enabled = true;
                // 不需要"恢复寻路"：上面把大脑恢复了，行为树会自己重新决策并再次寻路。
            }
        }

        private void SetSensorySuppression(AICharacterController ai, bool shouldSuppress)
        {
            if (ai == null) return;
            if (shouldSuppress)
            {
                if (_isSensorySuppressed) return;
                _cachedSightDist = ai.sightDistance;
                _cachedHearing = ai.hearingAbility;
                _cachedSightAngle = ai.sightAngle;
                _cachedTraceDist = ai.forceTracePlayerDistance;
                _cachedCanTalk = ai.canTalk;

                SuppressAIHard(ai);
                ai.PutBackWeapon();

                // ⚠ 伪装期间**闭嘴**。`canTalk` 是游戏自己的 AI 台词闸门
                // （预设里赋值：`CharacterRandomPreset.cs:382`），`PopText` / `PostSound` /
                // `TryToReloadIfEmpty` 三个 AI 任务都会查它。其中 `TryToReloadIfEmpty` 的
                // 「换弹」提示只受两道闸：`canTalk` 与 `!Health.Hidden`，而后者是 FOW 显隐系统
                // 会抢的**同一个** flag ⇒ 光靠 hidden 挡不住，必须把这道闸也关上。
                // 完整推演见 MimicBehavior.cs:465-474。
                ai.canTalk = false;
                _isSensorySuppressed = true;
            }
            else
            {
                if (!_isSensorySuppressed) return;
                ai.sightDistance = _cachedSightDist;
                ai.sightAngle = _cachedSightAngle;
                ai.hearingAbility = _cachedHearing;
                ai.forceTracePlayerDistance = _cachedTraceDist;
                ai.canTalk = _cachedCanTalk;
                _isSensorySuppressed = false;
            }
        }

        #region 基础辅助逻辑

        /// <summary>
        /// 伪装期间**压住血条**——"快速转身时血条闪一下"的根治点。
        ///
        /// <para><c>showHealthBar</c> 是 <c>Health.RequestHealthBar()</c> 的闸门
        /// （<c>Health.cs:458-463</c>）⇒ 把它压住就能挡掉 FOW 那一帧的请求，
        /// **不需要跟 FOW 抢 <c>hidden</c>**。完整推演（四个步骤）见
        /// <c>MimicBehavior.SuppressHealthBar</c> 的注释。</para>
        ///
        /// <para>每帧压是必要的：我们自己的强制显示补丁在 <c>Health.Start</c> 写它一次，
        /// 那个时机**晚于**本行为的初始化。先读后写，稳态下只是一次 bool 读。</para>
        /// </summary>
        private static void SuppressHealthBar(CharacterMainControl character)
        {
            Health health = character.Health;
            if (health != null && health.showHealthBar) health.showHealthBar = false;
        }

        /// <summary>
        /// 放回血条闸门。**必须在 <c>character.Show()</c> 之前调**——
        /// <c>Show()</c> 自己那次 <c>RequestHealthBar()</c> 正是我们要它建出血条的机会。
        /// </summary>
        private static void RestoreHealthBar(CharacterMainControl character)
        {
            Health health = character != null ? character.Health : null;
            if (health != null) health.showHealthBar = true;
        }

        private void InitRendererCache(CharacterMainControl character)
        {
            if (character.characterModel == null) return;

            // renderers 是私有字段（TeamSoda.Duckov.Core/CharacterModel.cs:59），
            // 已由 Publicizer 在编译期公开，直接访问
            _cachedRenderers = character.characterModel.renderers;
        }

        /// <summary>
        /// 把模型的全部 Renderer 切到目标开关状态。
        ///
        /// <para>⚠ 每帧都会被调到（模型可能被别的系统重新启用，所以要持续压制），
        /// 因此先读后写：已经在目标状态的就不写回。</para>
        /// </summary>
        private void SetRenderersEnabled(bool enabled)
        {
            if (_cachedRenderers == null) return;

            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                var r = _cachedRenderers[i];
                if (r != null && r.enabled != enabled) r.enabled = enabled;
            }
        }

        private void ForceShowVisuals() => SetRenderersEnabled(true);

        private void ForceHideVisuals() => SetRenderersEnabled(false);

        /// <summary>
        /// 玩家按下 E 的那一瞬间（**早于**物品真正归属玩家，见类型注释里那段推演）。
        ///
        /// <para>这里只记一个到点时间戳，真正的揭示交给 <see cref="UpdatePendingAmbush"/>。
        /// ⚠ **刻意不用协程**：拟态实测过——协程体确实执行了、宿主 active 且 enabled、
        /// 没有任何异常，但它**再也不恢复**。延迟只有 1.2 秒，而 <c>OnUpdate</c> 是
        /// **确定在跑**的。<see cref="Time.realtimeSinceStartup"/> 是因为按 E 时游戏可能被暂停，
        /// 用 <c>Time.time</c> 会像 <c>WaitForSeconds</c> 一样被冻住。</para>
        /// </summary>
        private void OnPlayerInteracted(CharacterMainControl player, InteractableBase _)
        {
            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 诊断：玩家拾取了伪装物（_hasTriggered={_hasTriggered} " +
                          $"_isTriggering={_isTriggering}）");
            }

            if (_hasTriggered || _isTriggering) return;
            _isTriggering = true;

            if (player.interactAction != null && player.interactAction.Running)
                player.interactAction.StopAction();

            _pendingTarget = player;
            _ambushDueTime = Time.realtimeSinceStartup + AmbushDelay;
        }

        /// <summary>延迟到点就触发（在 <see cref="OnUpdate"/> 里每帧查一次）。</summary>
        private void UpdatePendingAmbush(CharacterMainControl character)
        {
            if (_pendingTarget == null) return;
            if (Time.realtimeSinceStartup < _ambushDueTime) return;

            CharacterMainControl target = _pendingTarget;
            _pendingTarget = null;
            TriggerAmbush(character, target);
        }

        private void FaceTarget(CharacterMainControl character, CharacterMainControl target)
        {
            if (character == null || target == null) return;

            Vector3 direction = (target.transform.position - character.transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                character.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        /// <summary>
        /// ⚠ <b>清理必须幂等</b>：本方法会被走到两次——<see cref="OnEliteDeath"/> 里顺带调一次，
        /// 组件销毁时框架还会再调一次（<c>EliteBehaviorComponent.OnDestroy</c>）。
        /// 幂等的写法是：不做任何创建/实例化；退订可重复调用；销毁用
        /// <see cref="ClearDisguiseItem"/> 里的三重守卫（引用非空 + <c>IsBeingDestroyed</c> +
        /// <c>InInventory == null</c>）；引用与状态旗无条件重置，于是第二次进入时每一步都是空操作。
        /// </summary>
        public override void OnCleanup(CharacterMainControl character)
        {
            SetDisguiseState(character, false);
            ForceShowVisuals();
            RestoreHealthBar(character);
            if (character != null) character.Show();

            ClearDisguiseItem();

            _hasTriggered = false;
            _isTriggering = false;
            _isSensorySuppressed = false;

            // 与其它行为一致：清理时把引用放掉（实例本就会被丢弃，这里只是不留悬空引用）
            _pendingTarget = null;
            _interactHandler = null;
            _aiController = null;
            _soundMaker = null;
            _brain = null;
            _cachedRenderers = null;
            // 缓存里存的是**上一个角色**的碰撞体，不清的话万一实例被复用，会拿旧角色去挂 IgnoreCollision。
            _cachedCharacterColliders = null;
        }

        public void OnAttack(CharacterMainControl c, DamageInfo d)
        {
        }

        public override void OnEliteDeath(CharacterMainControl c, DamageInfo d) => OnCleanup(c);

        #endregion
    }
}
