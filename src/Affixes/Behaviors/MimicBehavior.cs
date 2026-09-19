using System;
using System.Collections.Generic;
using EliteEnemies.Core;
using UnityEngine;
using ItemStatsSystem;
using NodeCanvas.Framework;
using EliteEnemies.DebugTools;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【拟态】—— 敌人隐藏自身，伪装成**一件场景里的东西**，等玩家上钩时现形突袭。
    ///
    /// <para><b>两种形态，生成时 50/50 随机取一种：</b></para>
    /// <list type="bullet">
    /// <item><b>补给箱</b>（原有）：拿角色自己的 <c>deadLootBoxPrefab</c> 造一只装着诱饵物品的箱子，
    /// 玩家**按 E 开箱**或对它造成伤害时现形。角色站在箱子上。</item>
    /// <item><b>地上的物品</b>：拿一件高价值物品丢在脚下（走游戏自己的 <c>Item.Drop</c> 路径），
    /// 玩家**进入 <see cref="ItemTriggerDistance"/> 米**或对它造成伤害时现形。物品是平的，
    /// 角色与它同位。</item>
    /// </list>
    ///
    /// <para><b>为什么要两种形态</b>：箱子是玩家「有理由去开」的东西，但打久了就认得出来
    /// （孤零零一只箱子 = 拟态）。物品形态把触发点换成「走到跟前」，多一种变量。</para>
    ///
    /// <para>⚠ <b>箱子形态刻意保留「必须你去开」这条触发，不加距离触发</b>——那是它的博弈核心，
    /// 也是作者调过的既有行为。两种形态的触发差异是**有意**的，不是漏改。</para>
    ///
    /// <para><b>物品形态为什么是距离触发而不是「按 E 拾取」</b>（本项目踩过一次，别再改回去）：
    /// 拾取回调 <c>InteractableBase.OnInteractStartEvent</c>（<c>:293</c>）跑在真正拾取的
    /// <c>OnInteractStart</c>（<c>:297</c>）**之前**，中间没有任何重判。而揭示时要销毁伪装物
    /// （<see cref="ClearDisguiseItem"/>），<c>Object.Destroy</c> 又延迟到帧末
    /// （游戏自己留了证据：<c>ItemTreeExtensions.DestroyTree</c> 之外还有一个单独的
    /// <c>DestroyTreeImmediate</c>）⇒ 同一帧触发就会把一个**已排进销毁队列**的物品送进
    /// <c>PickupItem</c> → <c>ReleaseActiveAgent → Detach → SendToPlayerCharacterInventory</c>
    /// ⇒ 玩家背包里留下点不开的空条目。
    /// 曾经的对策是"延迟 1.2 秒跨过那一帧"，但实机表现是**玩家捡完走开几米敌人才现身**。
    /// 改成距离触发之后这条链整个不存在：现形时物品不在任何人背包里，直接销毁即可，
    /// **也顺带消掉了"每只物品拟态白送玩家一件物品"**。</para>
    ///
    /// <para>⚠ 若日后又想给物品形态加回拾取触发：**必须在揭示之前跨过那一帧**，不能在同一帧调
    /// <see cref="TriggerAmbush"/>。<see cref="ClearDisguiseItem"/> 里 <c>InInventory == null</c>
    /// 那道守卫只保护"已经被玩家拿走"的物品，**保护不了"正要去拿"的那一帧**。</para>
    ///
    /// <para>⚠ 箱子形态那两个偏移（<c>_boxFollowOffset</c> / <c>BoxSpawnLift</c>）是为**有体积的**箱子
    /// 设计的，物品形态一个都不能用：物品是平的，角色没法"站在"上面，抬高还会让它现形时浮空。</para>
    /// </summary>
    public class MimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Mimic";
        private const string LogTag = "[EliteEnemies.Mimic]";

        /// <summary>本只敌人的伪装形态，<see cref="OnEliteInitialized"/> 时随机定下、此后不变。</summary>
        private enum DisguiseForm
        {
            /// <summary>装诱饵物品的补给箱，开箱触发。角色站在箱子上。</summary>
            SupplyBox,

            /// <summary>躺在地上的一件物品，靠近触发。</summary>
            GroundItem
        }

        private DisguiseForm _form;

        /// <summary>两种形态各占一半。</summary>
        private const float GroundItemFormChance = 0.5f;

        // ═══════════════════════ 物品形态 ═══════════════════════

        /// <summary>
        /// 伪装物的候选物品池（高价值、玩家看见就会把脚步引过去的小件）。
        ///
        /// <para>⚠ 这几个 ID 有没有 **3D 世界模型**，代码里判不了——<c>..\Docs\ItemDatabase原版.xlsx</c>
        /// 只有 ID/名称/数值/标签，**没有模型列**（而且它本身不全：构建时会警告"掉落 ID 379 不在物品库里"，
        /// 而 379 在游戏里是存在的）。运行时判据是
        /// <c>ItemAssetsCollection.GetPrefab(id).ItemGraphic != null &amp;&amp; !prefab.useSpriteForPickup</c>
        /// （游戏侧 <c>InteractablePickup.CreateGraphic</c> 就是这么选的）。没有 3D 模型的物品会退化成
        /// **2D 精灵立牌**——实机看到纸片样式就说明这个 ID 要换掉。</para>
        ///
        /// <para>⚠ 829 曾被列进来，但它在物品库里查不到、邻居 827/828 是「神秘钥匙X/O」而它没有对应条目，
        /// 遂**未采用**——无效 ID 的失败方式是静默的：<c>InstantiateSync</c> 会走
        /// <c>InstantiateFallbackItem</c> 造一个既无图标也无模型的 <c>Item</c>，地上那件"伪装物"
        /// **完全隐形**，日志里什么都没有。</para>
        /// </summary>
        private static readonly int[] DisguiseItemIds =
        {
            827, 828,             // 神秘钥匙X / 神秘钥匙O
            801, 802, 803, 804,   // J-Lab门禁卡 黄 / 红 / 绿 / 蓝
            886, 887,             // J-Lab门禁卡 黑 / 紫
            388,                  // 0.2BTC
            1254, 1253            // 皇冠 / 纯金徽章
        };

        /// <summary>
        /// 物品形态：玩家进入这个距离（米）就现形。
        ///
        /// <para>这个数决定"玩家有多少时间意识到地上那件东西不对劲"，是物品形态**唯一的手感旋钮**
        /// （形状与调法照 <c>MusicianBehavior.TriggerDistance</c> 的先例：调它，别去加别的机制）。</para>
        ///
        /// <para><b>约束：必须明显大于交互距离。</b>交互扫描是玩家身前 0.2m、半径 0.3 的
        /// <c>OverlapSphere</c>（<c>CA_Interact.cs:51</c>）⇒ 玩家要贴到**约 0.5m** 内才有 E 提示。
        /// 触发距离若被压到那附近，"玩家还够得着物品"就重新成立，拾取那条有坑的路又变得可达
        /// （见类型注释）。2m 相比之下留了足够余量。</para>
        /// </summary>
        private const float ItemTriggerDistance = 2f;

        private Item _item;
        private DuckovItemAgent _itemAgent;
        private InteractablePickup _pickup;

        // ═══════════════════════ 补给箱形态 ═══════════════════════

        private InteractableLootbox _trapBox;

        /// <summary>诱饵箱的碰撞体。持有它才能**反复**重挂"与角色互不碰撞"，见 <see cref="ReignoreDisguiseCollision"/>。</summary>
        private Collider _trapBoxCollider;

        private const int BaitItemID = 445;
        private const int BaitItemCount = 10;

        /// <summary>角色相对箱子的偏移（略高于箱心，让角色站在箱子上）。**物品形态不用它。**</summary>
        private readonly Vector3 _boxFollowOffset = Vector3.up * 0.15f;

        /// <summary>开箱到伏击之间的延迟（秒，**真实时间**，见 <see cref="OnPlayerOpenedBox"/>）。</summary>
        private const float AmbushDelay = 1.2f;

        /// <summary>待触发的伏击（到点后由 <see cref="UpdatePendingAmbush"/> 触发）。</summary>
        private CharacterMainControl _pendingTarget;
        private float _ambushDueTime;

        /// <summary>
        /// 生成诱饵箱时把角色临时抬多高（米）。目的是让箱子**不生成在角色胶囊体内部**
        /// ——否则交互提示可能解析到角色身上，玩家打不开箱子。**物品形态不需要这一步。**
        /// </summary>
        private const float BoxSpawnLift = 5f;

        // ═══════════════════════ 两种形态共用 ═══════════════════════

        private AICharacterController _aiController;
        private CharacterSoundMaker _soundMaker;
        private GraphOwner _brain;

        private List<Renderer> _cachedRenderers;

        /// <summary>角色身上的碰撞体缓存，见 <see cref="ReignoreDisguiseCollision"/>。</summary>
        private Collider[] _cachedCharacterColliders;

        /// <summary>下一次重扫碰撞体列表的时刻（<see cref="Time.time"/> 口径）。</summary>
        private float _nextColliderRescanTime;

        /// <summary>碰撞体列表的重扫冷却（秒）。与 <c>EliteGlowController.RescanCooldown</c> 同一取舍。</summary>
        private const float ColliderRescanCooldown = 0.5f;

        private bool _hasTriggered = false;
        private bool _isTriggering = false;

        /// <summary>位置同步的死区（平方）。伪装物没动就不写，稳态下每帧只是一次比较。</summary>
        private const float SyncThresholdSqr = 0.001f;

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

            SpawnDisguise(character);

            SetMimicState(character, true);

            ForceHideVisuals();

            // 隐身本体这件事**也要过网**：联机模组不同步显隐，主机这边藏了、客机那边照样看得见
            // ⇒ 客机看到的是一只"站在原地不动的敌人"，伪装等于不存在（且比没有更糟：它把
            // 玩家的注意力引向本体，而伪装物反倒成了背景）。
            // 只在**进入/退出**伪装这两个边沿各报一次——隐身状态在两次之间是恒定的，
            // 而 `OnUpdate` 里那种"每帧 Hide()"是**本机**的维持动作，不需要每帧广播。
            RelayHidden(character, true);
        }

        /// <summary>把本体的显隐状态报给客机（走中立钩子；单机下没有接管方，代价是一次空调用）。</summary>
        private static void RelayHidden(CharacterMainControl character, bool hidden)
        {
            if (character == null) return;

            PlayerEffectRelay.RelayEliteVisual(character, character.transform.localScale, hidden);
        }

        /// <summary>
        /// 定下形态并生成对应的伪装物。
        ///
        /// <para>⚠️ <b>联机下只有"地上的物品"这一种形态</b>（<see cref="EliteEnemyCore.AreSpawnedPropsShared"/>
        /// 为假时）。理由是两种伪装物的**可见性命运不同**（已核联机模组源码）：</para>
        /// <list type="bullet">
        /// <item><b>补给箱</b>由我们直接 <c>Instantiate</c> 预制体（<see cref="SpawnTrapBox"/>），
        /// 而联机模组**只**在官方建箱路径 <c>InteractableLootbox.CreateFromItem</c> 的 Postfix 里
        /// 把尸箱注册进同步库（<c>Patch/Loot/DeadLootSpawnPatch.cs:26</c>）⇒ 客机**根本不存在**这只箱子。</item>
        /// <item><b>地上的物品</b>走游戏自己的 <c>ItemExtensions.Drop</c>，而联机模组**补丁了那条路**
        /// （<c>Patch/Item/LootInventoryPatch.cs:371</c>）⇒ 客机看得到诱饵。</item>
        /// </list>
        ///
        /// <para>而且箱子形态还差一条**结构上补不了**的东西：它的触发是"玩家去开箱"，
        /// 我们的钩子挂在<b>主机那只箱子</b>的交互事件上；客机开的是它本地那份 ⇒
        /// <b>主机不会知道</b>（联机模组只同步物品级动作，没有"某玩家打开了箱子"这条路）。
        /// 也就是说：箱子形态在联机下连"触发"都到不了。</para>
        ///
        /// <para><b>单机下这段一字未改</b>：<see cref="EliteEnemyCore.AreSpawnedPropsShared"/> 默认恒真，
        /// 取的仍是同一个 <c>Random.value</c> 与同一个 50% 判据。</para>
        /// </summary>
        private void SpawnDisguise(CharacterMainControl character)
        {
            _form = DisguiseForm.GroundItem;
            if (EliteEnemyCore.AreSpawnedPropsShared && UnityEngine.Random.value >= GroundItemFormChance)
                _form = DisguiseForm.SupplyBox;

            if (_form == DisguiseForm.SupplyBox) SpawnTrapBox(character);
            else SpawnDisguiseItem(character);

            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 诊断：本只敌人形态 = {_form}（{character.name}）");
            }
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null) return;

            // 只有箱子形态走"延迟伏击"这条路（开箱 → 等 1.2 秒）。
            if (_form == DisguiseForm.SupplyBox)
            {
                UpdatePendingAmbush(character);

                // ⚠ **伏击可能就在上面那句里生效了**（它会把 `_hasTriggered` 置真并揭示敌人）。
                // 此时若继续往下走，同一帧就会把敌人**重新藏回去、AI 重新压制回去**
                // ⇒ 表现是"伏击明明触发了，敌人却不现身、也不攻击"。
                // 开枪那条路没这个问题：`OnDamaged` 在 `OnUpdate` 外面触发，
                // 下一帧一进门就被最上面那句守卫挡住。
                if (_hasTriggered) return;
            }

            character.Hide();
            ForceHideVisuals();

            // ⚠ **每帧重挂**"伪装物 ↔ 角色互不碰撞"。Unity 在碰撞体被**重新启用**时会清掉
            // `Physics.IgnoreCollision` 的对，而角色的碰撞体会被反复启停（FOW 显隐等）
            // ⇒ 只挂一次的话，箱子迟早被角色的胶囊体顶走（实机症状：**箱子漂移**）。
            // 十几对原生调用/帧，相对这条链路上的其它工作可以忽略。
            ReignoreDisguiseCollision(character);

            // 压制血条——**"闪一下"的根治点**，见 SuppressHealthBar 的注释。
            SuppressHealthBar(character);

            // 持续压制 AI —— 见 EnsureAISuppressed 的注释（为什么必须"每帧"而不是"压一次"）
            EnsureAISuppressed(character);

            // 位置同步：**必须跟着伪装物走**——玩家可能在触发前把箱子推走，
            // 不同步的话敌人就会现身在另一处；而且敌人必须始终在那里，
            // 玩家才能直接射它提前击杀。
            SyncPositionToDisguise(character);

            // ⚠ 只有物品形态有距离触发，而且**刻意放在最后**：它是这里唯一会改变状态的一步（揭示）。
            //    放在最后 ⇒ 本帧该做的伪装维持工作都已经做完，也就不需要上面箱子那条
            //    "UpdatePendingAmbush 之后必须再查一次 _hasTriggered" 的补丁——
            //    那个坑（同帧把刚揭示的敌人又藏回去）在物品形态里从结构上就不存在。
            if (_form == DisguiseForm.GroundItem) CheckPlayerProximity(character);
        }

        /// <summary>
        /// 物品形态：玩家进入 <see cref="ItemTriggerDistance"/> 就触发伏击。
        ///
        /// <para>形状照搬 <c>MusicianBehavior.OnUpdate</c>——同一件事在本仓库
        /// 已有先例，别另起炉灶：距离用 <c>sqrMagnitude</c> 比较（省一次开方）。</para>
        ///
        /// <para>⚠ <b>玩家引用用 <see cref="EliteEnemyCore.FindNearestPlayer"/>，不是
        /// <c>CharacterMainControl.Main</c>。</b>后者是「本机玩家」——
        /// 联机下判定在主机上跑，客机玩家是另一个角色对象，写死 Main 会让本词条
        /// <b>只对主机玩家的靠近有反应</b>。（<c>Main</c> 在单机下没错，
        /// 所以这个 bug 只在联机暴露，详见 <c>Docs\Coop\05-integration-gotchas.md</c> §11。）</para>
        ///
        /// <para><b>为什么不做 <c>Time.timeScale &lt;= 0</c> 的守卫</b>（音乐家那边有）：
        /// 那个守卫是为"暂停时别继续吹奏"加的，而这里的判据是**纯位置比较**——
        /// 时间冻结时玩家位置不变，距离自然也不变，不存在"暂停期间误触发"这条路径。
        /// 加一个不会生效的分支只会是噪音。</para>
        ///
        /// <para>成本：每次 3 减 3 乘 1 比较。相比 <see cref="OnUpdate"/> 里每帧已经在做的
        /// （遍历全部 Renderer、遍历角色全部碰撞体逐个调原生 <c>Physics.IgnoreCollision</c>、
        /// <c>SetPosition</c>）可以忽略。</para>
        ///
        /// <para>⚠ <b>这里原本写的是"唯一要避开的是扫场景（<c>Physics.OverlapSphere</c> /
        /// <c>FindObjects*</c>），那种做法被 <c>check-affix-behaviors.sh</c> 的门 2 禁止"——
        /// 那句话把两件不同的事混成了一件，而且会把人挡在**正确**的做法外面。</b>
        /// 门 2 的正则（脚本第 60 行）只禁 <c>FindObjectsOfType|FindObjectsByType|FindFirstObject|
        /// FindAnyObject|Resources.(Find|Load)</c>：那一族是**托管层的全场景遍历**，每帧调等于自杀。
        /// 而 <c>Physics.OverlapSphereNonAlloc</c> 是**原生空间查询**（零 GC、只碰粗筛命中的那几个），
        /// 游戏自己就在用——全库 9 处调用，其中 7 处查的是角色
        /// （<c>ExplosionManager.cs:47</c>、<c>AIMainBrain.cs:149</c>、<c>AimTargetFinder.cs:36</c>、
        /// <c>ItemAgent_Gun.cs:968</c>、<c>ItemAgent_MeleeWeapon.cs:129</c>、<c>Projectile.cs:271</c>）。
        /// 要避开的是**每帧**调它，不是它本身；本方法走位置比较只是因为这里只关心**一个**已知目标。</para>
        /// </summary>
        private void CheckPlayerProximity(CharacterMainControl character)
        {
            // ⚠ 用**最近玩家**（本机 + 远端），不是 `CharacterMainControl.Main`。
            //   后者是「本机玩家」——联机下判定在主机上跑，客机玩家是另一个角色对象，
            //   写死 Main 会让这个词条**只对主机玩家的靠近有反应**。
            var player = EliteEnemyCore.FindNearestPlayer(character.transform.position, out float dist);
            if (player == null) return;

            float distSqr = dist * dist;
            if (distSqr > ItemTriggerDistance * ItemTriggerDistance) return;

            TriggerAmbush(character, player);
        }

        /// <summary>
        /// 把角色对齐到伪装物（带死区：伪装物没动就不写，稳态下每帧只是一次比较）。
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
        ///
        /// <para>⚠ <b>两种形态的垂直偏移不同，这不是笔误</b>：箱子有体积，角色要"站在箱子上"
        /// （<c>_boxFollowOffset</c>）；物品是**平的**，角色没法站在它上面，所以对齐目标就是物品自身的轴心。
        /// 另外 ⚠ 对 UnityEngine.Object 一律用 <c>!= null</c> 判空（走 Unity 重载的 fake-null），
        /// **不能写 <c>?.transform</c>**——那走的是 C# 的真 null 判断，已销毁的箱子会被判成非空然后抛异常。</para>
        /// </summary>
        private void SyncPositionToDisguise(CharacterMainControl character)
        {
            Vector3 targetPos;
            Quaternion targetRot;

            if (_form == DisguiseForm.SupplyBox)
            {
                if (_trapBox == null) return;
                targetPos = _trapBox.transform.position + _boxFollowOffset;
                targetRot = _trapBox.transform.rotation;
            }
            else
            {
                if (_itemAgent == null) return;
                targetPos = _itemAgent.transform.position;
                targetRot = _itemAgent.transform.rotation;
            }

            if (Vector3.SqrMagnitude(character.transform.position - targetPos) <= SyncThresholdSqr) return;

            character.SetPosition(targetPos);
            character.transform.rotation = targetRot;
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

        // ═══════════════════════════ 补给箱形态 ═══════════════════════════

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
        /// 但**贴地的生成物**会踩在这上面——见物品形态：把伪装物放平在地上，
        /// 对着它开枪就可能被地面先吃掉弹道。</para>
        /// </summary>
        private void SpawnTrapBox(CharacterMainControl character)
        {
            if (character == null || character.deadLootBoxPrefab == null) return;

            // 与官方死亡箱同一个出发点：**角色原地的地面位置**。
            //
            // ⚠ 但必须**先把角色挪开**（原版是"抬高 5 米"，这里保留同样的语义）。
            // 原因：箱子若生成在角色的胶囊体内部，交互提示的解析可能落到**角色**身上而不是箱子，
            // 玩家就打不开它了。挪角色改走安全 API（见 SyncPositionToDisguise），
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
            ReignoreDisguiseCollision(character);

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
        /// 延迟到点就触发（在 <see cref="OnUpdate"/> 里每帧查一次）。**只有箱子形态用它。**
        /// </summary>
        private void UpdatePendingAmbush(CharacterMainControl character)
        {
            if (_pendingTarget == null) return;
            if (Time.realtimeSinceStartup < _ambushDueTime) return;

            CharacterMainControl target = _pendingTarget;
            _pendingTarget = null;
            TriggerAmbush(character, target);
        }

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

        // ═══════════════════════════ 物品形态 ═══════════════════════════

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
        /// <para>⚠ <b>不需要再补一次 <c>MultiSceneCore.MoveToActiveWithScene</c>。</b>箱子形态要补，
        /// 是因为它直接 <c>Instantiate</c> 预制体、绕过了官方建箱路径；<c>Item.Drop</c> 自己就带这一步
        /// （<c>ItemExtensions.cs:107-110</c>）。</para>
        ///
        /// <para>⚠ <b>刻意不订阅 <c>OnInteractStartEvent</c></b>：触发是距离式的，
        /// <see cref="ItemTriggerDistance"/> 远早于交互距离，玩家够得着它之前就已经现形了。
        /// 理由见类型注释。</para>
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
            _itemAgent = agent;
            _pickup = agent.GetComponent<InteractablePickup>();

            ReignoreDisguiseCollision(character);

            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 诊断：伪装物已生成（{character.name}，物品={itemId}，" +
                          $"交互组件={(_pickup != null ? "有" : "无")}）");
            }
        }

        /// <summary>
        /// 收掉还在地上的伪装物品。
        ///
        /// <para>⚠ <b>玩家已经把它捡走时绝不能销毁</b>——<c>Item.Detach()</c> 会把它
        /// **从玩家背包里拽出来**（<c>Item.cs:797-801</c> → <c>InInventory?.RemoveItem(this)</c>）。
        /// 判据用 <c>InInventory == null</c>（<c>Item.cs:390</c>，public）：在地面的物品不属于任何背包。</para>
        ///
        /// <para>⚠ 范围触发下这条路**正常不会走到**（<see cref="ItemTriggerDistance"/> 远早于交互距离），
        /// 它防的是"玩家带着远程拾取类模组直接拿走"这类意外路径。守卫留着，但**别指望它
        /// 能挡住"同一帧内的拾取"**——那件事必须靠触发时机的顺序解决，见类型注释。</para>
        ///
        /// <para>清理范式是游戏自己的：<c>Detach()</c> 后 <c>DestroyTree()</c>
        /// （<c>ItemTreeExtensions.cs:117-131</c>）。销毁 Item 会连带销毁 agent——
        /// <c>Item.OnDestroy</c>（<c>Item.cs:1118-1124</c>）里会 <c>Detach()</c> +
        /// <c>agentUtilities.ReleaseActiveAgent()</c>，后者销毁的就是 agent 那个 GameObject。
        /// **不要反过来只销毁 agent**：那会留下一个无渲染、无交互的 Item 数据物体。</para>
        /// </summary>
        private void ClearDisguiseItem()
        {
            if (_item != null && !_item.IsBeingDestroyed && _item.InInventory == null)
            {
                // ⚠️ **联机下必须让"角色拾取"这一步真的发生**（作者 2026-09-19 实测：
                // 客机上的诱饵不消失、玩家还能把它捡走）。
                //
                //   联机模组的掉落物同步**只认拾取这一条路**：
                //   `CharacterMainControl.PickupItem` 的 Postfix 会调 `Server_HandleLocalPickup`
                //   → 从掉落注册表注销 + 广播 `ItemDespawnRpc`（`Patch/Item/LootInventoryPatch.cs:427`）。
                //   我们直接从地面上 `DestroyTree` 它**完全不知情**，客机那份就永远留在原地。
                //   （箱子形态是同一类问题，见类型注释——只是箱子连"存在"都同步不过去。）
                //
                //   这条路**单机下同样成立**（没有联机补丁时就是一次普通拾取），所以不做分支。
                //   拾起来之后**立刻销毁**，不让它变成拟态随身的战利品——
                //   本词条刻意避免的就是"每只物品拟态白送玩家一件物品"。
                var owner = Ctx?.Character;
                bool pickedUp = owner != null && owner.PickupItem(_item);

                if (!pickedUp)
                {
                    // 拾取失败（持有者已死 / 背包不可用）⇒ 回落到直接销毁：
                    // 单机下仍然正确；联机下客机那份会留在原处——**已知残留**，
                    // 但比"干脆不销毁"好：至少本机与主机侧是干净的。
                    _item.Detach();
                    _item.DestroyTree();
                }
                else if (!_item.IsBeingDestroyed)
                {
                    _item.DestroyTree();
                }
            }

            _item = null;
            _itemAgent = null;
            _pickup = null;
        }

        // ═══════════════════════════ 两种形态共用 ═══════════════════════════

        /// <summary>收掉当前形态的伪装物。</summary>
        private void ClearDisguise()
        {
            if (_form == DisguiseForm.SupplyBox)
            {
                if (_trapBox != null) UnityEngine.Object.Destroy(_trapBox.gameObject);
                _trapBoxCollider = null;
                _trapBox = null;
            }
            else
            {
                ClearDisguiseItem();
            }
        }

        /// <summary>当前形态伪装物的碰撞体（用于挂"与角色互不碰撞"）。</summary>
        private Collider GetDisguiseCollider()
        {
            if (_form == DisguiseForm.SupplyBox) return _trapBoxCollider;

            // 优先取游戏自己认的那一个交互碰撞体，没有再退到 agent 上的任意 Collider。
            if (_pickup != null && _pickup.interactCollider != null) return _pickup.interactCollider;
            if (_itemAgent != null) return _itemAgent.GetComponent<Collider>();
            return null;
        }

        /// <summary>
        /// 让伪装物与角色身上的每个碰撞体互不碰撞。
        ///
        /// <para>⚠ <b>重挂这一步必须每帧做</b>（见 <see cref="OnUpdate"/>）：Unity 在碰撞体被
        /// **重新启用**时会清掉 <c>Physics.IgnoreCollision</c> 的对，而角色的碰撞体会被反复启停。
        /// 只挂一次的后果就是实机看到的**箱子漂移**——箱子被角色的胶囊体顶走。
        /// 物品形态同理：物品是非运动学的静态 collider、推不走，但角色的胶囊体会被自己的伪装物**顶住**。</para>
        ///
        /// <para>但**取列表**不必每帧：见方法体内的缓存说明。代价是"角色之后换了模型"
        /// 这种情况最多晚一个重扫窗口才被挂上，而不是下一帧。</para>
        /// </summary>
        private void ReignoreDisguiseCollision(CharacterMainControl character)
        {
            Collider disguiseCollider = GetDisguiseCollider();
            if (disguiseCollider == null || character == null) return;

            // ⚠ 每帧 `GetComponentsInChildren` 会**每帧**整棵层级遍历 + 新建一个数组——
            //    对一只可能蹲几分钟不动的诱饵来说纯属浪费（原先这里就是每一帧都在付这笔）。
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
                if (colliders[i] != null) Physics.IgnoreCollision(disguiseCollider, colliders[i], true);
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

            ClearDisguise();

            SetMimicState(character, false);
            ForceShowVisuals();

            // ⚠ 顺序有讲究：**必须先放回血条闸门，再 Show()**——
            // `Show()` 里第一句就是 `health?.RequestHealthBar()`（`CharacterMainControl.cs:2557-2559`），
            // 而那正是我们要它把血条**建出来**的那一次请求；闸门还压着的话这次请求会被挡掉。
            RestoreHealthBar(character);
            character.Show();

            // 现形 = 本体的显隐从"藏"翻到"露" ⇒ 这是另一个边沿，必须报（否则客机那边
            // 一直停在"隐身"，玩家被一个看不见的敌人打）。
            RelayHidden(character, false);

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
        /// 幂等的写法是：不做任何创建/实例化；销毁用 <see cref="ClearDisguise"/> 里的守卫
        /// （箱子是引用非空，物品还多一道 <c>IsBeingDestroyed</c> + <c>InInventory == null</c>）；
        /// 引用与状态旗无条件重置，于是第二次进入时每一步都是空操作。
        /// </summary>
        public override void OnCleanup(CharacterMainControl character)
        {
            SetMimicState(character, false);
            ForceShowVisuals();
            RestoreHealthBar(character);
            if (character != null) character.Show();

            // 撤销精英化（`StripElite`）时可能还停在"伪装中"⇒ 同样要把"露出来"报出去，
            // 否则客机永远停在隐身。第二次进入时是重复值，客机侧幂等。
            RelayHidden(character, false);

            ClearDisguise();

            _hasTriggered = false;
            _isTriggering = false;
            _isSensorySuppressed = false;

            // 与其它行为一致：清理时把引用放掉（实例本就会被丢弃，这里只是不留悬空引用）
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
