using System;
using System.Collections;
using System.Collections.Generic;
using EliteEnemies.Modifiers;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 精英行为组件
    /// </summary>
    public class EliteBehaviorComponent : MonoBehaviour
    {
        private CharacterMainControl _character;

        /// <summary>受伤事件的两个来源。**两个都订阅**——为什么缺一不可见 <see cref="RegisterCombatEvents"/>。</summary>
        private DamageReceiver _damageReceiver;
        private Health _health;

        /// <summary>
        /// 「这一遍伤害已经由 <c>DamageReceiver</c> 那侧派发过了」。
        ///
        /// <para>正常命中时 <c>DamageReceiver.Hurt</c> 会**先触发自己的事件、再紧接着同步转发**
        /// 给 <c>Health.Hurt</c>（<c>DamageReceiver.cs:94-96</c>），于是同一次伤害经两个事件各来一遍。
        /// 靠这个标志让紧随其后的那遍跳过，保证 <c>OnDamaged</c> 只派发一次。</para>
        ///
        /// <para>⚠️ <b>不能用"记住上次的 DamageInfo 再比引用"来去重</b>——<c>DamageInfo</c> 是
        /// <b>值类型</b>，每次装箱都是新对象，<c>ReferenceEquals</c> 恒为 false。</para>
        ///
        /// <para>程序化结算（联机模组那条路）只触发 <c>Health</c> 那侧，此标志为 false ⇒ 正常派发。</para>
        /// </summary>
        private bool _handledByReceiver;
        private List<IAffixBehavior> _behaviors = new List<IAffixBehavior>();
        private List<IUpdateableAffixBehavior> _updateableBehaviors = new List<IUpdateableAffixBehavior>();
        private List<ICombatAffixBehavior> _combatBehaviors = new List<ICombatAffixBehavior>();
        private bool _isInitialized = false;

        private UnityAction<DamageInfo> _receiverHurtHandler;
        private UnityAction<DamageInfo> _healthHurtHandler;

        /// <summary>每个敌人只解析一次的常用引用，见 <see cref="AffixContext"/>。</summary>
        private AffixContext _context;

        /// <summary>
        /// 行为经 <see cref="AffixBehaviorBase.StartManagedCoroutine"/> 起的协程的句柄。
        ///
        /// <para>宿主是敌人自己，所以敌人被销毁/停用时 Unity 本来就会中止这些协程；
        /// 登记它们的意义是**让框架知道有这些协程**——统一停（见 <see cref="OnDestroy"/>），
        /// 而不是让每个行为自己找宿主（`ModBehaviour.Instance` / `attacker` / 敌人自身）。
        /// 「协程被中止但状态没还原」是本工程栽过的一类问题（PhaseSwap 卡死）。</para>
        /// </summary>
        private readonly List<Coroutine> _managedCoroutines = new List<Coroutine>();

        /// <summary>供 <see cref="AffixBehaviorBase"/> 使用：起一个由框架托管的协程。</summary>
        internal Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;

            Coroutine handle = StartCoroutine(routine);
            _managedCoroutines.Add(handle);
            return handle;
        }

        /// <summary>供 <see cref="AffixBehaviorBase"/> 使用：停掉一个托管协程。</summary>
        internal void StopManagedCoroutine(Coroutine handle)
        {
            if (handle == null) return;

            StopCoroutine(handle);
            _managedCoroutines.Remove(handle);
        }

        public void Initialize(CharacterMainControl character, List<string> affixes)
        {
            if (_isInitialized) return;
            _character = character;
            if (affixes == null || affixes.Count == 0) return;

            // 为每个词缀创建独立实例
            foreach (var affixName in affixes)
            {
                IAffixBehavior behavior = AffixBehaviorManager.CreateBehaviorInstance(affixName);
                if (behavior != null)
                {
                    _behaviors.Add(behavior);

                    if (behavior is IUpdateableAffixBehavior updateable)
                        _updateableBehaviors.Add(updateable);

                    if (behavior is ICombatAffixBehavior combat)
                        _combatBehaviors.Add(combat);
                }
            }

            // 一次性解析常用引用，再交给每个行为（见 AffixContext 的类注释）。
            // 必须在调用 OnEliteInitialized **之前**：行为在那里就要用。
            _context = new AffixContext(this, character);
            foreach (var behavior in _behaviors)
            {
                if (behavior is AffixBehaviorBase baseBehavior) baseBehavior.BindContext(_context);
            }

            // 调用初始化
            foreach (var behavior in _behaviors)
            {
                try
                {
                    behavior.OnEliteInitialized(_character);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(IAffixBehavior.OnEliteInitialized), ex);
                }
            }

            // 绑定战斗事件
            RegisterCombatEvents();
            _isInitialized = true;
        }

        /// <summary>
        /// 注册战斗事件监听
        /// </summary>
        private void RegisterCombatEvents()
        {
            if (_combatBehaviors.Count == 0) return;

            // 1. 绑定受伤事件——**两个都要绑**，理由见下。
            //
            // 游戏里这是两个不同组件上的同名 UnityEvent，触发时机与覆盖面都不同：
            //
            //   DamageReceiver.Hurt(dmg)                       // DamageReceiver.cs:76
            //   {   dmg.toDamageReceiver = this;
            //       OnHurtEvent?.Invoke(dmg);                  // ← ① 扣血**之前**，仅"被打中"才走
            //       health.Hurt(dmg); }                        //    再转发给下面
            //
            //   Health.Hurt(dmg)                               // Health.cs:308
            //   {   …扣血…;
            //       OnDeadEvent?.Invoke(dmg);
            //       OnHurtEvent?.Invoke(dmg); }                // ← ② 扣血**之后**，两条路都会走这
            //
            // **只绑 ①**：正常命中没问题，但**程序化结算走不到**它——
            //   联机模组在主机上结算客机上报的伤害正是直接调 `health.Hurt()`
            //   （`AISyncService.ApplyDamageToController`），于是 `OnDamaged` 永不触发，
            //   **报复之类的词条在联机下静默失效**（已实测确认）。
            //
            // **只绑 ②**：覆盖面够了，但 `OnDamaged` 会从"扣血前"变成"扣血后"——
            //   而**扣血前这个时机是有用的**：`UndyingBehavior` 的"预判通道"正是靠它
            //   赶在 `Health.Hurt` 之前补血并给上无敌（`Health.Hurt` 开头就是
            //   `if (invincible) return false;`），那一击才被完全挡下。
            //   `SplitBehavior` 也明写两条通道"缺一不可"。**挪到 ② 会破坏单机的不死。**
            //
            // ⇒ 两个都绑，靠**事件顺序标志**保证同一次伤害只派发一次：
            //   正常命中时 `DamageReceiver.Hurt` 触发完自己的事件后会**紧接着同步**调用
            //   `health.Hurt`，所以 `Health` 那一遍必定紧跟在 `DamageReceiver` 那一遍之后。
            //   收到前者就置 `_handledByReceiver`，后者据此跳过并清标志。
            //   （⚠ 不能用"比较 DamageInfo"去重：它是**值类型**，装箱后 `ReferenceEquals` 恒为 false。）
            _damageReceiver = _character.mainDamageReceiver;
            _health = _character.Health;

            if (_damageReceiver == null && _health == null)
            {
                Debug.LogWarning($"[EliteBehaviorComponent] {_character.name} 既没有 DamageReceiver 也没有 Health！");
            }
            else
            {
                _receiverHurtHandler = OnDamageReceiverHurt;
                _healthHurtHandler = OnHealthHurt;
                _damageReceiver?.OnHurtEvent.AddListener(_receiverHurtHandler);
                _health?.OnHurtEvent.AddListener(_healthHurtHandler);
            }

            // 2. 绑定攻击事件
            // 监听敌人的射击事件
            _character.OnShootEvent += OnShootHandlerWrapper;

            // 监听近战攻击事件
            _character.OnAttackEvent += OnMeleeAttackHandlerWrapper;

            //Debug.Log($"[EliteBehaviorComponent] {_character.name} 已绑定战斗事件 ({_combatBehaviors.Count} 个战斗行为)");
        }

        /// <summary>
        /// 正常命中路径：**扣血之前**由 <c>DamageReceiver</c> 触发。
        /// 派发后置起标志，让紧跟着的那遍 <c>Health</c> 事件跳过。
        /// </summary>
        private void OnDamageReceiverHurt(DamageInfo damageInfo)
        {
            _handledByReceiver = true;
            DispatchOnDamaged(damageInfo);
        }

        /// <summary>
        /// 覆盖面更广的那条：**扣血之后**由 <c>Health</c> 触发，
        /// 包括**不经过 DamageReceiver 的程序化结算**（联机模组给主机结算客机伤害即此路）。
        /// 若这一遍已由 <c>DamageReceiver</c> 派发过就跳过。
        /// </summary>
        private void OnHealthHurt(DamageInfo damageInfo)
        {
            if (_handledByReceiver)
            {
                _handledByReceiver = false;   // 消费掉，只跳过紧随其后的这一遍
                return;
            }

            DispatchOnDamaged(damageInfo);
        }

        private void DispatchOnDamaged(DamageInfo damageInfo)
        {
            if (!_isInitialized || _character == null) return;

            foreach (var behavior in _combatBehaviors)
            {
                try
                {
                    behavior.OnDamaged(_character, damageInfo);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(ICombatAffixBehavior.OnDamaged), ex);
                }
            }
        }

        /// <summary>
        /// 射击事件处理器
        /// </summary>
        private void OnShootHandlerWrapper(DuckovItemAgent agent)
        {
            if (!_isInitialized || _character == null) return;

            // 创建伤害信息（射击事件没有直接的DamageInfo，需要构造）
            DamageInfo damageInfo = new DamageInfo(_character);

            foreach (var behavior in _combatBehaviors)
            {
                try
                {
                    behavior.OnAttack(_character, damageInfo);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(ICombatAffixBehavior.OnAttack), ex);
                }
            }
        }

        /// <summary>
        /// 近战攻击事件处理器
        /// </summary>
        private void OnMeleeAttackHandlerWrapper(DuckovItemAgent agent)
        {
            if (!_isInitialized || _character == null) return;

            // 创建伤害信息
            DamageInfo damageInfo = new DamageInfo(_character);

            foreach (var behavior in _combatBehaviors)
            {
                try
                {
                    behavior.OnAttack(_character, damageInfo);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(ICombatAffixBehavior.OnAttack), ex);
                }
            }
        }

        /// <summary>
        /// 触发命中玩家事件（从 Harmony Patch 调用）
        /// </summary>
        /// <param name="attacker">发动攻击的那个敌人（就是本组件所属的角色）。</param>
        /// <param name="victim">挨打的那个玩家角色。**给玩家上 debuff 要用它**——
        /// 联机下挨打的可能是别的玩家（见 <c>EliteBuffs.ApplyToPlayer</c>）。</param>
        public void TriggerHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (!_isInitialized || _character == null) return;

            //Debug.Log($"[EliteBehaviorComponent] TriggerHitPlayer 调用，攻击者: {attacker?.name}, 战斗词缀数量: {_combatBehaviors.Count}");

            foreach (var behavior in _combatBehaviors)
            {
                try
                {
                    behavior.OnHitPlayer(attacker, victim, damageInfo);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(ICombatAffixBehavior.OnHitPlayer), ex);
                }
            }
        }

        private void Update()
        {
            if (!_isInitialized || _character == null || _updateableBehaviors.Count == 0) return;

            float deltaTime = Time.deltaTime;
            foreach (var behavior in _updateableBehaviors)
            {
                try
                {
                    behavior.OnUpdate(_character, deltaTime);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(IUpdateableAffixBehavior.OnUpdate), ex);
                }
            }
        }

        public void OnDeath(DamageInfo damageInfo)
        {
            if (!_isInitialized) return;
            foreach (var behavior in _behaviors)
            {
                try
                {
                    behavior.OnEliteDeath(_character, damageInfo);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(IAffixBehavior.OnEliteDeath), ex);
                }
            }
        }

        /// <summary>
        /// 记录一个行为回调抛出的异常。**只记录，不重抛**——隔离的意义就是让一个词条的异常
        /// 不打断同一轮里的其余词条，也不穿进游戏的事件链（`OnHurtEvent` 是 UnityEvent、
        /// `OnShootEvent`/`OnAttackEvent` 是委托，异常会顺着它们扩散）。
        ///
        /// <para>隔离范围只到"每个行为每次回调"：初始化与清理原本就有 try/catch，此前**热路径没有**，
        /// 两者不对称。不隔离的代价是有实证的——`OnEliteDeath` 一旦被中断，
        /// 后续词条的清理不会执行，残留的修改器会留在角色身上。</para>
        /// </summary>
        private void LogBehaviorError(IAffixBehavior behavior, string callback, Exception ex)
        {
            // 读名字本身也可能抛（AffixName 是行为的实例属性）。这里必须兜住：
            // 否则报告错误的代码会把原始异常吃掉，恰好是最需要日志的时候。
            string name;
            try
            {
                name = behavior.AffixName;
            }
            catch
            {
                name = behavior.GetType().Name;
            }

            Debug.LogError($"[EliteBehaviorComponent] 词条 '{name}' 的 {callback} 抛异常" +
                           $"（已隔离，同轮其余词条不受影响）: {ex}");
        }

        private void OnDestroy()
        {
            if (!_isInitialized) return;

            UnregisterCombatEvents();

            // 先停托管协程、再走各自的清理：否则清理跑完协程还可能再跑一帧并改状态。
            for (int i = 0; i < _managedCoroutines.Count; i++)
            {
                Coroutine handle = _managedCoroutines[i];
                if (handle != null) StopCoroutine(handle);
            }
            _managedCoroutines.Clear();

            foreach (var behavior in _behaviors)
            {
                try
                {
                    behavior.OnCleanup(_character);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, nameof(IAffixBehavior.OnCleanup), ex);
                }

                // 保险丝：无论 OnCleanup 是否成功，都把"以这个词条名为来源登记过的修改"再撤一遍。
                //
                // 依据：行为对 CharacterModifiers 的**全部**写入都以 AffixName 作来源标识——
                // 基类助手如此，6 处直接调 CharacterModifiers.* 的也如此
                //（见 docs\词条模块审查与设计.md §3.1）。原先撤销完全靠"作者记得调
                // ClearBaseModifiers"，而异常一打断清理链，残留就会留在角色身上——
                // StripElite 撤销精英化时角色还活着，那是最明显的一条路径。
                try
                {
                    if (_character != null) CharacterModifiers.ClearAll(_character, behavior.AffixName);
                }
                catch (Exception ex)
                {
                    LogBehaviorError(behavior, "ClearAll（清理保险丝）", ex);
                }
            }

            _behaviors.Clear();
            _updateableBehaviors.Clear();
            _combatBehaviors.Clear();
            _isInitialized = false;
        }
        
        /// <summary>
        /// 解绑战斗事件
        /// </summary>
        private void UnregisterCombatEvents()
        {
            // 解绑受伤事件
            // 两个来源都要退订；对象可能已被销毁，逐个判空。
            if (_damageReceiver != null && _receiverHurtHandler != null)
                _damageReceiver.OnHurtEvent.RemoveListener(_receiverHurtHandler);
            if (_health != null && _healthHurtHandler != null)
                _health.OnHurtEvent.RemoveListener(_healthHurtHandler);

            _handledByReceiver = false;

            // 解绑攻击事件
            if (_character != null)
            {
                _character.OnShootEvent -= OnShootHandlerWrapper;
                _character.OnAttackEvent -= OnMeleeAttackHandlerWrapper;
            }

            //Debug.Log($"[EliteBehaviorComponent] {_character?.name} 已解绑战斗事件");
        }

    }
}