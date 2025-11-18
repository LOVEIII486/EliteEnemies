using System;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀行为接口 - 所有复杂词缀必须实现
    /// </summary>
    public interface IAffixBehavior
    {
        /// <summary>
        /// 词缀名称
        /// </summary>
        string AffixName { get; }

        /// <summary>
        /// 在敌人初始化时调用
        /// </summary>
        void OnEliteInitialized(CharacterMainControl character);

        /// <summary>
        /// 在敌人死亡时调用
        /// </summary>
        void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo);

        /// <summary>
        /// 清理资源
        /// </summary>
        void OnCleanup(CharacterMainControl character);
    }

    /// <summary>
    /// 词缀行为基类
    /// </summary>
    public abstract class AffixBehaviorBase : IAffixBehavior
    {
        public abstract string AffixName { get; }

        public virtual void OnEliteInitialized(CharacterMainControl character)
        {
        }

        public virtual void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public virtual void OnCleanup(CharacterMainControl character)
        {
        }

        public virtual void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }
    }

    /// <summary>
    /// 支持 Update 的词缀行为接口
    /// </summary>
    public interface IUpdateableAffixBehavior : IAffixBehavior
    {
        /// <summary>
        /// 每帧更新
        /// </summary>
        void OnUpdate(CharacterMainControl character, float deltaTime);
    }

    /// <summary>
    /// 支持战斗事件的词缀行为接口
    /// </summary>
    public interface ICombatAffixBehavior : IAffixBehavior
    {
        /// <summary>
        /// 当精英攻击时触发
        /// </summary>
        void OnAttack(CharacterMainControl character, DamageInfo damageInfo);

        /// <summary>
        /// 当精英受伤时触发
        /// </summary>
        void OnDamaged(CharacterMainControl character, DamageInfo damageInfo);

        /// <summary>
        /// 当该精英敌人命中玩家时触发
        /// </summary>
        void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo);
    }

    /// <summary>
    /// 词缀行为管理器 - 负责注册和创建词缀行为实例
    /// </summary>
    public static class AffixBehaviorManager
    {
        // 存储词缀名称 -> 行为类型的映射
        private static readonly Dictionary<string, Type> BehaviorTypes
            = new Dictionary<string, Type>();

        /// <summary>
        /// 注册词缀行为类型
        /// </summary>
        public static void RegisterBehavior<T>() where T : IAffixBehavior, new()
        {
            // 创建临时实例来获取词缀名称
            T tempInstance = new T();
            string affixName = tempInstance.AffixName;

            if (string.IsNullOrEmpty(affixName))
            {
                Debug.LogWarning($"[AffixBehaviorManager] Invalid behavior registration: {typeof(T).Name}");
                return;
            }

            if (BehaviorTypes.ContainsKey(affixName))
            {
                Debug.LogWarning($"[AffixBehaviorManager] Behavior '{affixName}' already registered, replacing...");
            }

            BehaviorTypes[affixName] = typeof(T);
            //Debug.Log($"[AffixBehaviorManager] Registered behavior type: {affixName} ({typeof(T).Name})");
        }

        /// <summary>
        /// 为指定词缀创建新的行为实例
        /// </summary>
        public static IAffixBehavior CreateBehaviorInstance(string affixName)
        {
            if (!BehaviorTypes.TryGetValue(affixName, out Type behaviorType))
            {
                return null;
            }

            try
            {
                IAffixBehavior instance = (IAffixBehavior)Activator.CreateInstance(behaviorType);
                //Debug.Log($"[AffixBehaviorManager] Created new instance of {behaviorType.Name} for affix '{affixName}'");
                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[AffixBehaviorManager] Failed to create instance of {behaviorType.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查词缀是否已注册
        /// </summary>
        public static bool IsRegistered(string affixName)
        {
            return BehaviorTypes.ContainsKey(affixName);
        }

        /// <summary>
        /// 获取所有注册的词缀名称
        /// </summary>
        public static IEnumerable<string> GetAllAffixNames()
        {
            return BehaviorTypes.Keys;
        }

        /// <summary>
        /// 清空所有注册的行为
        /// </summary>
        public static void ClearAll()
        {
            BehaviorTypes.Clear();
            Debug.Log("[AffixBehaviorManager] All behavior types cleared");
        }

        /// <summary>
        /// 获取已注册的行为数量
        /// </summary>
        public static int Count => BehaviorTypes.Count;
    }

    public static class AffixBehaviorUtils
    {
        
    }
}