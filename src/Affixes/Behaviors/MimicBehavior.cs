using System;
﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using NodeCanvas.Framework;
using EliteEnemies.DebugTools;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【拟态】
    /// </summary>
    public class MimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Mimic";
        private const string LogTag = "[EliteEnemies.Mimic]";

        private InteractableLootbox _trapBox;
        private AICharacterController _aiController;
        private CharacterSoundMaker _soundMaker;
        private GraphOwner _brain;

        private List<Renderer> _cachedRenderers;

        /// <summary>角色身上的碰撞体缓存，见 <see cref="ReignoreBoxCollision"/>。</summary>
        private Collider[] _cachedCharacterColliders;

        /// <summary>下一次重扫碰撞体列表的时刻（<see cref="Time.time"/> 口径）。</summary>
        private float _nextColliderRescanTime;

        /// <summary>碰撞体列表的重扫冷却（秒）。与 `EliteGlowController.RescanCooldown` 同一取舍。</summary>
        private const float ColliderRescanCooldown = 0.5f;

        private bool _hasTriggered = false;
        private bool _isTriggering = false;

        private const int BaitItemID = 445;
        private const int BaitItemCount = 10;

        /// <summary>角色相对诱饵箱的偏移（略高于箱心，让角色站在箱子上）。</summary>
        private readonly Vector3 _followOffset = Vector3.up * 0.15f;

        /// <summary>位置同步的死区（平方）。箱子没动就不写，稳态下每帧只是一次比较。</summary>
        private const float SyncThresholdSqr = 0.001f;

        /// <summary>开箱到伏击之间的延迟（秒，**真实时间**，见 <see cref="OnPlayerOpenedBox"/>）。</summary>
        private const float AmbushDelay = 1.2f;

        /// <summary>待触发的伏击（到点后由 <see cref="UpdatePendingAmbush"/> 触发）。</summary>
        private CharacterMainControl _pendingTarget;
        private float _ambushDueTime;

        /// <summary>诱饵箱的碰撞体。持有它才能**反复**重挂"与角色互不碰撞"，见 <see cref="ReignoreBoxCollision"/>。</summary>
        private Collider _trapBoxCollider;

        /// <summary>
        /// 生成诱饵箱时把角色临时抬多高（米）。目的是让箱子**不生成在角色胶囊体内部**
        /// ——否则交互提示可能解析到角色身上，玩家打不开箱子。
        /// </summary>
        private const float BoxSpawnLift = 5f;

        private float _cachedSightDist, _cachedHearing, _cachedSightAngle, _cachedTraceDist;
        private bool _cachedCanTalk;
        private bool _isSensorySuppressed = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isTriggering = false;

            // 精英自身的引用一律从框架上下文取（每个敌人只解析一次）。
            // 原先这里用 GetComponentInChildren + GetComponentInParent 两次**层级遍历**找一个
            // 公开字段就能拿到的控制器（character.aiCharacterController，CharacterMainControl.cs:68）。
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

            SpawnTrapBox(character);

            SetMimicState(character, true);

            ForceHideVisuals();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null) return;

            // 先看待触发的伏击（开箱那条路用它替代了协程，见 OnPlayerOpenedBox）。
            UpdatePendingAmbush(character);

            // ⚠ **伏击可能就在上面那句里生效了**（它会把 `_hasTriggered` 置真并揭示敌人）。
            // 此时若继续往下走，同一帧就会把敌人**重新藏回去、AI 重新压制回去**
            // ⇒ 表现是"伏击明明触发了，敌人却不现身、也不攻击"。
            // 开枪那条路没这个问题：`OnDamaged` 在 `OnUpdate` 外面触发，
            // 下一帧一进门就被最上面那句守卫挡住。
            if (_hasTriggered) return;

            character.Hide();
            ForceHideVisuals();

            // ⚠ **每帧重挂**"箱子 ↔ 角色互不碰撞"。Unity 在碰撞体被**重新启用**时会清掉
            // `Physics.IgnoreCollision` 的对，而角色的碰撞体会被反复启停（FOW 显隐等）
            // ⇒ 只挂一次的话，箱子迟早被角色的胶囊体顶走（实机症状：**箱子漂移**）。
            // 十几对原生调用/帧，相对这条链路上的其它工作可以忽略。
            ReignoreBoxCollision(character);

            // 压制血条——**"闪一下"的根治点**，见 SuppressHealthBar 的注释。
            SuppressHealthBar(character);

            // 持续压制 AI —— 见 EnsureAISuppressed 的注释（为什么必须"每帧"而不是"压一次"）
            EnsureAISuppressed(character);

            // 位置同步：**必须跟着箱子走**——玩家可能在开箱前把箱子推走，
            // 不同步的话敌人就会现身在另一处；而且敌人必须始终站在箱子上，
            // 玩家才能直接射它提前击杀。
            SyncPositionToBox(character);
        }

        /// <summary>
        /// 把角色对齐到诱饵箱（带死区：箱子没动就不写，稳态下每帧只是一次比较）。
        ///
        /// <para><b>⚠ 必须走 <c>character.SetPosition()</c>，不要直接写 <c>transform.position</c>。</b>
        /// 后者绕过了两件事，而那正是原先抖动/穿模的来源：</para>
        /// <list type="number">
        /// <item><c>SetPosition</c> → <c>movementControl.ForceSetPosition</c>
        /// （<c>CharacterMainControl.cs:2699-2703</c> → <c>Movement.cs:243-248</c>）里做了三件事：
        /// <c>PauseGroundConstraint(1f)</c>（挂起贴地约束，否则地面会把角色拽回去）、
        /// ECM2 的 <c>SetPosition</c>（**同步 CharacterController**）、<c>velocity = 0</c>（清残留速度）；</item>
        /// <item>它还会发 <c>OnSetPositionEvent</c>，让订阅者（如 <c>PetAI</c>）知道角色被挪过。</item>
        /// </list>
        ///
        /// <para>旋转仍直接写 <c>transform.rotation</c>——本文件自己的 <see cref="FaceTarget"/>
        /// 也是这么做的，且旋转不参与那个控制器互抢。</para>
        /// </summary>
        private void SyncPositionToBox(CharacterMainControl character)
        {
            if (_trapBox == null) return;

            Vector3 targetPos = _trapBox.transform.position + _followOffset;
            if (Vector3.SqrMagnitude(character.transform.position - targetPos) <= SyncThresholdSqr) return;

            character.SetPosition(targetPos);
            character.transform.rotation = _trapBox.transform.rotation;
        }

        /// <summary>
        /// 被攻击时触发埋伏
        /// </summary>
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasTriggered) return;
    
            // 被打激活时，如果是被其他单位攻击，将攻击者设为突袭目标
            CharacterMainControl attacker = damageInfo.fromCharacter;
            TriggerAmbush(character, attacker);
        }

        /// <summary>
        /// 在角色**脚下**放一只诱饵箱，并让它与角色**互不碰撞**。
        ///
        /// <para><b>整体流程与作者原版一致</b>（先把角色挪开 ⇒ 在腾出的地面位置生成箱子 ⇒
        /// 每帧把角色对齐回箱子上），**改的只是"怎么挪角色"**：原版三处都直接写
        /// <c>character.transform.position</c>，而角色带着 <c>CharacterController</c>（ECM2），
        /// 两者互抢 ⇒ 抖动/穿模。现在一律走 <c>character.SetPosition()</c>
        /// （<c>CharacterMainControl.cs:2699-2703</c>）。</para>
        /// <list type="number">
        /// <item><b>挪开角色</b>：<c>SetPosition(原地 + up×<see cref="BoxSpawnLift"/>)</c>。
        /// 这一步**不能省**——箱子若生成在角色胶囊体内部，交互提示可能解析到**角色**身上，
        /// 玩家就打不开箱子（＝伏击永远不会触发）。</item>
        /// <item><b>互不碰撞</b>：<c>Physics.IgnoreCollision</c>——游戏自己就是这么处理
        /// "生成物不该和生成者打架"的（<c>Grenade.cs:271-284</c>、<c>SpawnEgg.cs:40</c>）。
        /// ⚠ 这个 ignore 是**长期**的、不是 Grenade 那种 0.5 秒：诱饵箱本就是盖在这只敌人
        /// 身上的伪装，两者永远不该碰撞，否则非运动学的箱子会被角色的胶囊体顶走、两者分家。</item>
        /// </list>
        ///
        /// <para>⚠ 子弹**不会**被箱子挡住：弹道射线只打 <c>hitLayers</c>
        /// （<c>Projectile.cs:143</c> = damageReceiver ∪ wall ∪ ground ∪ blockBullet），
        /// 而箱子的交互碰撞体在 "Interactable" 层、不在其中
        /// ⇒ 玩家打箱子照样打到角色的伤害接收器。</para>
        ///
        /// <para>⚠ <b>本注释原先写的是「弹道只考虑 <c>damageReceiverLayerMask</c>（<c>Projectile.cs:361</c>）」，
        /// 两处都不对</b>：<c>:361</c> 是判断"命中的是不是伤害接收器"的**分支处**，真正的射线掩码在
        /// <c>:143</c>，而且**含 ground**。对箱子结论不变（箱子的碰撞体不在其中任何一个掩码里），
        /// 但**贴地的生成物**会踩在这上面——见 <see cref="ItemMimicBehavior"/>：把伪装物放平在地上，
        /// 对着它开枪就可能被地面先吃掉弹道。</para>
        /// </summary>
        private void SpawnTrapBox(CharacterMainControl character)
        {
            if (character == null || character.deadLootBoxPrefab == null) return;

            // 与官方死亡箱同一个出发点：**角色原地的地面位置**。
            //
            // ⚠ 但必须**先把角色挪开**（原版是"抬高 5 米"，这里保留同样的语义）。
            // 原因：箱子若生成在角色的胶囊体内部，交互提示的解析可能落到**角色**身上而不是箱子，
            // 玩家就打不开它了。挪角色改走安全 API（见 SyncPositionToBox），
            // **不再直接写 `transform.position`**；紧接着每帧的位置同步会把它拉回箱子正上方。
            Vector3 originalFloorPos = character.transform.position;
            character.SetPosition(originalFloorPos + Vector3.up * BoxSpawnLift);

            _trapBox = UnityEngine.Object.Instantiate(character.deadLootBoxPrefab, originalFloorPos,
                character.transform.rotation);

            if (_trapBox == null) return;

            // 让箱子落到地面：非运动学 + 重力 + 连续检测（防止高速穿过地面）。
            Rigidbody boxRb = _trapBox.GetComponent<Rigidbody>();
            if (boxRb != null)
            {
                boxRb.isKinematic = false;
                boxRb.useGravity = true;
                boxRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // 给一点初始自旋（原实现有，我在上一轮改物理时误删了——它不是可有可无的：
                // 箱子"从敌人身上掉出来"落地时转两下才像真的）
                boxRb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;

                boxRb.WakeUp();
            }

            // 箱子不是触发器等，才能被地面接住
            Collider boxCollider = _trapBox.interactCollider != null
                ? _trapBox.interactCollider
                : _trapBox.GetComponent<Collider>();
            if (boxCollider != null) boxCollider.isTrigger = false;

            _trapBoxCollider = boxCollider;
            ReignoreBoxCollision(character);

            // ⚠ 官方的建箱路径（`InteractableLootbox.CreateFromItem`）会先调它私有的
            // `CreateLocalInventory()`（`:328-332`）**新建一个 Inventory**；我们绕过那条路、
            // 直接 Instantiate 预制体，靠的是预制体自带的那个。
            // 已核：`Instantiate` 会把预制体内部的引用重映射到克隆体上 ⇒ 每只箱子各有一份，
            // **不存在共享**。但那条路依赖预制体确实带了 Inventory，所以这里判空而不是直接解引用。
            var boxInventory = _trapBox.Inventory;
            if (boxInventory == null)
            {
                Debug.LogError($"{LogTag} 诱饵箱的预制体没有 Inventory 组件，诱饵物品放不进去" +
                               "（官方在 CreateFromItem 里会补一个）");
                return;
            }

            boxInventory.SetCapacity(BaitItemCount + 4);
            for (int i = 0; i < BaitItemCount; i++)
            {
                Item newItem = ItemAssetsCollection.InstantiateSync(BaitItemID);
                if (newItem != null) _trapBox.Inventory.AddItem(newItem);
            }

            // 绑定交互
            if (_trapBox.GetComponent<InteractableBase>() is var interactable && interactable != null)
            {
                interactable.OnInteractStartEvent.AddListener((player, _) => OnPlayerOpenedBox(player, character));

                if (DebugSwitch.Enabled)
                {
                    Debug.Log($"{LogTag} 诊断：诱饵箱已生成并绑定交互（{character.name}，箱子={_trapBox.name}）");
                }
            }
            else if (DebugSwitch.Enabled)
            {
                // 这一条若出现，"开箱不触发伏击"就有了直接答案：箱子根本不是 InteractableBase。
                Debug.LogError($"{LogTag} 诱饵箱上没有 InteractableBase，**交互监听挂不上** " +
                               "⇒ 开箱永远不会触发伏击");
            }

            // 移至当前活动场景（官方生成死亡箱走的也是这一步，见 InteractableLootbox.cs:349）
            try
            {
                Duckov.Scenes.MultiSceneCore.MoveToActiveWithScene(_trapBox.gameObject,
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
            catch (Exception ex)
            {
                // 原先这里是空 catch：跨场景搬箱子失败会让陷阱箱留在别的场景里
                // （玩家根本走不到那个箱子），但日志里什么都不留。报出来。
                Debug.LogError($"{LogTag} 陷阱箱移到当前场景失败，箱子可能不在玩家可达的场景: {ex}");
            }
        }

        /// <summary>
        /// 让诱饵箱与角色身上的每个碰撞体互不碰撞。
        ///
        /// <para>⚠ <b>重挂这一步必须每帧做</b>（见 <see cref="OnUpdate"/>）：Unity 在碰撞体被
        /// **重新启用**时会清掉 `Physics.IgnoreCollision` 的对，而角色的碰撞体会被反复启停。
        /// 只挂一次的后果就是实机看到的**箱子漂移**——箱子被角色的胶囊体顶走。</para>
        ///
        /// <para>但**取列表**不必每帧：见方法体内的缓存说明。代价是"角色之后换了模型"
        /// 这种情况最多晚一个重扫窗口才被挂上，而不是下一帧。</para>
        /// </summary>
        private void ReignoreBoxCollision(CharacterMainControl character)
        {
            if (_trapBoxCollider == null || character == null) return;

            // ⚠ 每帧 `GetComponentsInChildren` 会**每帧**整棵层级遍历 + 新建一个数组——
            //    对一只可能蹲几分钟不动的诱饵箱来说纯属浪费（原先这里就是每一帧都在付这笔）。
            //    改为缓存 + 冷却重扫，与 `EliteGlowController.RefreshRenderers` 同一范式。
            //
            // 缓存**不会**让"碰撞体被反复启停"漏挂：Unity 清掉的是 `Physics.IgnoreCollision`
            // 的**对**，不是组件本身——启停不产生新对象，缓存里的引用始终有效，
            // 所以下面那个每帧循环照旧生效，箱子漂移那条防线原样不动。
            // 缓存唯一可能漏的是**新增**的碰撞体（换模型），故留一个 0.5 秒的重扫窗口。
            if (_cachedCharacterColliders == null || Time.time >= _nextColliderRescanTime)
            {
                _cachedCharacterColliders = character.GetComponentsInChildren<Collider>(true);
                _nextColliderRescanTime = Time.time + ColliderRescanCooldown;
            }

            var colliders = _cachedCharacterColliders;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) Physics.IgnoreCollision(_trapBoxCollider, colliders[i], true);
            }
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
        /// 伪装期间的 AI 压制。**必须每帧做**——这里每一条都是踩过的坑：
        ///
        /// <list type="number">
        /// <item><b>引用可能晚到。</b>我们的初始化不保证早于 AI 控制器与行为树就位
        /// （分裂体这种"战斗中即时生成"的路径最易踩中）⇒ 初始化那一刻 <c>Ctx.Ai</c> 若还是 null，
        /// 整套压制（暂停大脑 / 收枪 / 闭嘴）**一次都不会发生**，而且此后永远补不上。
        /// 所以这里允许**惰性重取**（<c>character.aiCharacterController</c> 是公开字段，
        /// <c>CharacterMainControl.cs:68</c>）。</item>
        /// <item><b>游戏会把它们覆写回去。</b>预设应用时**整批**写 AI 字段
        /// （<c>CharacterRandomPreset.cs:355-390</c>：<c>sightDistance</c> / <c>hearingAbility</c> /
        /// <c>sightAngle</c> / <c>forceTracePlayerDistance</c> / <c>canTalk</c> …），
        /// 而那个时机**可能落在我们的初始化之后** ⇒ "压一次"会被它抹掉。
        /// 这与血条闸门（<see cref="SuppressHealthBar"/>）是同一类问题、同一套解法：
        /// **持续压，而不是压一次**。</item>
        /// <item><b>行为树可能晚于我们启动</b>，也可能被别的系统 <c>StartBehaviour()</c> 回来。</item>
        /// </list>
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
            //    先判 isRunning 再暂停——不去碰"还没跑起来"的图，免得留下 isPaused 的怪状态。
            if (_brain != null && _brain.isRunning && !_brain.isPaused) _brain.PauseBehaviour();
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

            if (_trapBox != null)
            {
                UnityEngine.Object.Destroy(_trapBox.gameObject);
                _trapBoxCollider = null;
                _trapBox = null;
            }
    
            SetMimicState(character, false);
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

        private void SetMimicState(CharacterMainControl character, bool isMimic)
        {
            if (_aiController == null) return;

            if (isMimic)
            {
                // 暂停大脑逻辑
                if (_brain != null && _brain.isRunning) _brain.PauseBehaviour();
                SetSensorySuppression(_aiController, true);
                if (_soundMaker != null) _soundMaker.enabled = false;

                // 取消当前寻路，防止它在被强刷坐标时尝试回正位置导致抖动。
                // 原先这里是 NavMeshAgent.isStopped = true——游戏不用 NavMeshAgent（AI 走 A*），
                // 那行**一直没生效**。现在用 AI 层的标准调用：StopMove()（AICharacterController.cs:744）。
                // 因为上面已经把大脑 Pause 了，不会有新的寻路被发起 ⇒ 不需要显式的"恢复"。
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

                // ⚠ 伪装期间**闭嘴**。这不只是"少一句台词"的音效问题——`canTalk` 是游戏自己的
                //    AI 台词闸门（预设里赋值：`CharacterRandomPreset.cs:382`），
                //    `PopText` / `PostSound` / `TryToReloadIfEmpty` 三个 AI 任务都会查它
                //    （`AICharacterController.cs:136`）。
                //    其中 `TryToReloadIfEmpty` 的「换弹」提示只受两道闸：`canTalk` 与
                //    `!Health.Hidden`；而后者是 FOW 显隐系统会抢的**同一个** flag
                //    （`DuckovHider.OnReveal()` → `Show()` 会把 hidden 清掉，
                //    本文件 SuppressHealthBar 的注释记录过同一场抢旗）
                //    ⇒ **光靠 hidden 挡不住，必须把这道闸也关上**。
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
        /// 伪装期间**压住血条**——这是"快速转身时血条闪一下"的根治点。
        ///
        /// <para><b>闪一下是怎么发生的</b>（全部有源码出处）：</para>
        /// <list type="number">
        /// <item>拟态每帧 <c>Hide()</c> ⇒ <c>hidden = true</c>
        /// （<c>CharacterMainControl.cs:2547-2556</c>）；</item>
        /// <item>而 <c>HealthBar.LateUpdate</c> 开头就是
        /// <c>if (… || target.Hidden) { Release(); return; }</c>（<c>HealthBar.cs:126-140</c>）
        /// ⇒ 本该看不到；</item>
        /// <item><b>但游戏的 FOW 显隐系统</b>（<c>DuckovHider</c>）在角色进入玩家视野时会调
        /// <c>CharacterMainControl.Show()</c>，而 <c>Show()</c> 里第一句就是
        /// <c>health?.RequestHealthBar()</c>（<c>:2557-2559</c>）——**它自己会请求血条**；</item>
        /// <item>于是"玩家快速转身看向拟态"的那一帧，两个系统抢同一个 <c>hidden</c>，
        /// FOW 恰好跑在后面 ⇒ 血条被建出来、显示一帧，下一帧又被 <c>Release()</c>
        /// ⇒ 玩家看到**闪一下**。</item>
        /// </list>
        ///
        /// <para><c>showHealthBar</c> 是 <c>Health.RequestHealthBar()</c> 的闸门
        /// （<c>Health.cs:458-463</c>）⇒ 把它压住就能挡掉那一帧的请求，
        /// **不需要跟 FOW 抢 `hidden`**。</para>
        ///
        /// <para>每帧压是必要的：我们自己的强制显示补丁在 <c>Health.Start</c> 写它一次
        /// （那时机**晚于**本行为的初始化，见 <c>Patches模块代码审查.md</c> §5-10）。
        /// 先读后写，稳态下只是一次 bool 读。</para>
        /// </summary>
        private static void SuppressHealthBar(CharacterMainControl character)
        {
            Health health = character.Health;
            if (health != null && health.showHealthBar) health.showHealthBar = false;
        }

        /// <summary>
        /// 放回血条闸门。**必须在 <c>character.Show()</c> 之前调**——见上一条注释第 ③ 步：
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
        /// <para>⚠ 这两个方法**每帧**都会被 `OnUpdate` / `OnCleanup` 调到（模型可能被别的系统
        /// 重新启用，所以要持续压制），因此先读后写：已经在目标状态的就不写回——
        /// 省掉每帧对每个 Renderer 的一次原生调用（`Renderer.enabled` 的读是廉价的，写不是）。</para>
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

        private void OnPlayerOpenedBox(CharacterMainControl player, CharacterMainControl owner)
        {
            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 诊断：玩家打开了诱饵箱（_hasTriggered={_hasTriggered} " +
                          $"_isTriggering={_isTriggering}）");
            }

            if (_hasTriggered || _isTriggering) return;
            _isTriggering = true;
    
            if (player.interactAction != null && player.interactAction.Running) 
                player.interactAction.StopAction();
        
            // 延迟突袭：把玩家作为初始目标，延迟到点后在 `OnUpdate` 里触发。
            //
            // ⚠ **刻意不用协程**：原先走 `StartManagedCoroutine`，而实机日志显示——
            // 协程体**确实执行了**（"协程已启动"打出来了）、宿主 **active 且 enabled**、
            // **没有任何异常**，但它**再也不恢复**。排查成本已远超收益。
            // 延迟只有 1.2 秒，而 `OnUpdate` 是**确定在跑**的（敌人全程保持隐身就是证据：
            // `Hide()` 每帧都在跑）。改成"记一个到点时间戳、在 `OnUpdate` 里比较"，
            // **整类协程问题直接消失**。
            //
            // ⚠ 时间用 `Time.realtimeSinceStartup`（真实时间）：开箱时游戏可能被暂停
            // （日志里就能看到 `PauseMenu`），用 `Time.time` 会像 `WaitForSeconds` 一样被冻住。
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

        // （原 `DelayedAmbushRoutine` 已删除：延迟改由 `OnUpdate` + 时间戳实现，
        //   见 `OnPlayerOpenedBox` 与 `UpdatePendingAmbush` 的注释。）
        
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
        
        public override void OnCleanup(CharacterMainControl character)
        {
            SetMimicState(character, false);
            ForceShowVisuals();
            RestoreHealthBar(character);
            if (character != null) character.Show();

            _hasTriggered = false;
            _isTriggering = false;
            _isSensorySuppressed = false;

            if (_trapBox != null) UnityEngine.Object.Destroy(_trapBox.gameObject);

            // 与其它行为一致：清理时把引用放掉（实例本就会被丢弃，这里只是不留悬空引用）
            _trapBox = null;
            _trapBoxCollider = null;
            _pendingTarget = null;
            _aiController = null;
            _soundMaker = null;
            _brain = null;
            _cachedRenderers = null;
            // 与 _cachedRenderers 同理：缓存里存的是**上一个角色**的碰撞体，
            // 不清的话万一实例被复用，会拿旧角色的碰撞体去挂 IgnoreCollision。
            _cachedCharacterColliders = null;
        }

        public void OnAttack(CharacterMainControl c, DamageInfo d)
        {
        }



        public override void OnEliteDeath(CharacterMainControl c, DamageInfo d) => OnCleanup(c);

        #endregion
    }
}