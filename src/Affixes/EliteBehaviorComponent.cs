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
        private DamageReceiver _damageReceiver;
        private List<IAffixBehavior> _behaviors = new List<IAffixBehavior>();
        private List<IUpdateableAffixBehavior> _updateableBehaviors = new List<IUpdateableAffixBehavior>();
        private List<ICombatAffixBehavior> _combatBehaviors = new List<ICombatAffixBehavior>();
        private bool _isInitialized = false;

        private UnityAction<DamageInfo> _hurtHandler;

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

            // 1. 绑定受伤事件
            _damageReceiver = _character.mainDamageReceiver;
            if (_damageReceiver != null)
            {
                _hurtHandler = OnHurtHandler;
                _damageReceiver.OnHurtEvent.AddListener(_hurtHandler);
            }
            else
            {
                Debug.LogWarning($"[EliteBehaviorComponent] {_character.name} 没有 DamageReceiver 组件！");
            }

            // 2. 绑定攻击事件
            // 监听敌人的射击事件
            _character.OnShootEvent += OnShootHandlerWrapper;

            // 监听近战攻击事件
            _character.OnAttackEvent += OnMeleeAttackHandlerWrapper;

            //Debug.Log($"[EliteBehaviorComponent] {_character.name} 已绑定战斗事件 ({_combatBehaviors.Count} 个战斗行为)");
        }

        /// <summary>
        /// 受伤事件处理器
        /// </summary>
        private void OnHurtHandler(DamageInfo damageInfo)
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
        public void TriggerHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (!_isInitialized || _character == null) return;

            //Debug.Log($"[EliteBehaviorComponent] TriggerHitPlayer 调用，攻击者: {attacker?.name}, 战斗词缀数量: {_combatBehaviors.Count}");

            foreach (var behavior in _combatBehaviors)
            {
                try
                {
                    behavior.OnHitPlayer(attacker, damageInfo);
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
            if (_damageReceiver != null && _hurtHandler != null)
            {
                _damageReceiver.OnHurtEvent.RemoveListener(_hurtHandler);
            }

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