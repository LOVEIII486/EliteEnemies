using System.Collections.Generic;
using EliteEnemies.EliteEnemy.BuffsSystem.Effects;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.BuffsSystem
{
    /// <summary>
    /// 精英 Buff 注册中心
    /// 自动注册所有内置 Buff 效果
    /// </summary>
    public class EliteBuffRegistry
    {
        private const string LogTag = "[EliteEnemies.BuffRegistry]";
        
        private static EliteBuffRegistry _instance;
        public static EliteBuffRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EliteBuffRegistry();
                    // _instance.RegisterAllEffects(); // 自动注册和稍后手动注册二选一
                }
                return _instance;
            }
        }

        private Dictionary<string, IEliteBuffEffect> _effects = new Dictionary<string, IEliteBuffEffect>();
        private bool _isInitialized = false;
        
        public void Initialize()
        {
            if (_isInitialized) return;

            RegisterAllEffects();
            _isInitialized = true;
            Debug.Log($"{LogTag} 已初始化，注册了 {_effects.Count} 个 Buff 效果");
        }
        
        private void RegisterAllEffects()
        {
            RegisterEffect(new BlindnessEffect());
            RegisterEffect(new SlowEffect());
            RegisterEffect(new StunEffect());
            RegisterEffect(new DungEaterEffect());
            RegisterEffect(new DistortionEffect());
            RegisterEffect(new EMPEffect());
        }
        
        private void RegisterEffect(IEliteBuffEffect effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.BuffName))
            {
                Debug.LogWarning($"{LogTag} 无效的 Buff 效果");
                return;
            }

            if (_effects.ContainsKey(effect.BuffName))
            {
                Debug.LogWarning($"{LogTag} Buff 效果已存在: {effect.BuffName}");
                return;
            }

            _effects[effect.BuffName] = effect;
        }
        
        public IEliteBuffEffect GetEffect(string buffName)
        {
            _effects.TryGetValue(buffName, out var effect);
            return effect;
        }
        
        public bool IsRegistered(string buffName)
        {
            return _effects.ContainsKey(buffName);
        }
        
        public void Clear()
        {
            _effects.Clear();
            _isInitialized = false;
        }
    }
}