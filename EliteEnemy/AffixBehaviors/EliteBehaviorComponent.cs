using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.AffixBehaviors
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
        private UnityAction<DamageInfo> _shootHandler;
        
        private Dictionary<string, object> _customData = new Dictionary<string, object>();

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

            // 调用初始化
            foreach (var behavior in _behaviors)
            {
                try { behavior.OnEliteInitialized(_character); }
                catch (Exception ex) { Debug.LogError($"[EliteBehaviorComponent] Init error: {ex}"); }
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
            // 监听敌人的射击事件（适用于远程攻击）
            _shootHandler = OnShootHandler;
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
                    Debug.LogError($"[EliteBehaviorComponent] OnDamaged error: {ex}"); 
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
                    Debug.LogError($"[EliteBehaviorComponent] OnAttack (Shoot) error: {ex}"); 
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
                    Debug.LogError($"[EliteBehaviorComponent] OnAttack (Melee) error: {ex}"); 
                }
            }
        }

        /// <summary>
        /// 射击事件处理器（从UnityEvent调用）
        /// </summary>
        private void OnShootHandler(DamageInfo damageInfo)
        {
            if (!_isInitialized || _character == null) return;

            foreach (var behavior in _combatBehaviors)
            {
                try 
                { 
                    behavior.OnAttack(_character, damageInfo); 
                }
                catch (Exception ex) 
                { 
                    Debug.LogError($"[EliteBehaviorComponent] OnAttack error: {ex}"); 
                }
            }
        }
        
        /// <summary>
        /// 触发命中玩家事件（从 Harmony Patch 调用）
        /// </summary>
        public void TriggerHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (!_isInitialized || _character == null) return;

            Debug.Log($"[EliteBehaviorComponent] TriggerHitPlayer 调用，攻击者: {attacker?.name}, 战斗词缀数量: {_combatBehaviors.Count}");

            foreach (var behavior in _combatBehaviors)
            {
                try 
                {
                    Debug.Log($"[EliteBehaviorComponent] 调用词缀 OnHitPlayer: {behavior.AffixName}");
                    behavior.OnHitPlayer(attacker, damageInfo);
                }
                catch (Exception ex) 
                { 
                    Debug.LogError($"[EliteBehaviorComponent] OnHitPlayer error [{behavior.AffixName}]: {ex}"); 
                }
            }
        }
        
        private void Update()
        {
            if (!_isInitialized || _character == null || _updateableBehaviors.Count == 0) return;

            float deltaTime = Time.deltaTime;
            foreach (var behavior in _updateableBehaviors)
            {
                behavior.OnUpdate(_character, deltaTime);
            }
        }

        public void OnDeath(DamageInfo damageInfo)
        {
            if (!_isInitialized) return;
            foreach (var behavior in _behaviors)
            {
                behavior.OnEliteDeath(_character, damageInfo);
            }
        }

        private void OnDestroy()
        {
            if (!_isInitialized) return;

            UnregisterCombatEvents();

            foreach (var behavior in _behaviors)
            {
                try { behavior.OnCleanup(_character); }
                catch (Exception ex) { Debug.LogError($"[EliteBehaviorComponent] OnCleanup error: {ex}"); }
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

        /// <summary>
        /// 获取自定义数据
        /// </summary>
        public T GetCustomData<T>(string key, T defaultValue = default)
        {
            if (_customData.TryGetValue(key, out object value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        /// <summary>
        /// 设置自定义数据
        /// </summary>
        public void SetCustomData(string key, object value)
        {
            _customData[key] = value;
        }

        /// <summary>
        /// 清除自定义数据
        /// </summary>
        public void ClearCustomData(string key)
        {
            _customData.Remove(key);
        }
    }
}