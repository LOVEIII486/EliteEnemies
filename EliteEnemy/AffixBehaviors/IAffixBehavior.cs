using System;
using System.Collections.Generic;
using UnityEngine;
using EliteEnemies.EliteEnemy.AttributeModifier;

namespace EliteEnemies.EliteEnemy.AffixBehaviors
{
    /// <summary>
    /// 词缀行为接口 - 所有复杂词缀必须实现
    /// </summary>
    public interface IAffixBehavior
    {
        string AffixName { get; }
        void OnEliteInitialized(CharacterMainControl character);
        void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo);
        void OnCleanup(CharacterMainControl character);
    }

    /// <summary>
    /// 词缀行为基类 - 纯工具类，不包含隐性自动化逻辑
    /// </summary>
    public abstract class AffixBehaviorBase : IAffixBehavior
    {
        public abstract string AffixName { get; }

        // --- 默认实现全部留空，允许子类随意覆盖 ---
        public virtual void OnEliteInitialized(CharacterMainControl character) { }
        public virtual void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public virtual void OnCleanup(CharacterMainControl character) { }
        public virtual void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }

        // --- 受保护的助手方法：仅供开发者手动调用 ---

        /// <summary>
        /// 手动清理该词缀对目标造成的所有 StatModifier 和 AIField 修改
        /// </summary>
        protected void ClearBaseModifiers(CharacterMainControl character)
        {
            if (character != null)
            {
                AttributeModifier.AttributeModifier.ClearAll(character, this.AffixName);
            }
        }

        /// <summary>
        /// 便捷方法：带入 AffixName 的修改方法
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers
        /// </summary>
        protected void Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier = true)
        {
            AttributeModifier.AttributeModifier.Modify(character, attributeName, value, isMultiplier, this.AffixName);
        }

        /// <summary>
        /// 便捷方法：应用精英怪三维增强
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers
        /// </summary>
        protected void ApplyPowerup(CharacterMainControl character, float hpMul, float dmgMul, float spdMul)
        {
            AttributeModifier.AttributeModifier.Quick.ApplyElitePowerup(character, hpMul, dmgMul, spdMul, this.AffixName);
        }
    }

    // --- 接口定义保持不变，确保兼容性 ---
    public interface IUpdateableAffixBehavior : IAffixBehavior
    {
        void OnUpdate(CharacterMainControl character, float deltaTime);
    }

    public interface ICombatAffixBehavior : IAffixBehavior
    {
        void OnAttack(CharacterMainControl character, DamageInfo damageInfo);
        void OnDamaged(CharacterMainControl character, DamageInfo damageInfo);
        void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo);
    }

    // --- 管理器保持不变 ---
    public static class AffixBehaviorManager
    {
        private static readonly Dictionary<string, Type> BehaviorTypes = new Dictionary<string, Type>();

        public static void RegisterBehavior<T>() where T : IAffixBehavior, new()
        {
            T tempInstance = new T();
            string affixName = tempInstance.AffixName;
            if (string.IsNullOrEmpty(affixName)) return;
            BehaviorTypes[affixName] = typeof(T);
        }

        public static IAffixBehavior CreateBehaviorInstance(string affixName)
        {
            if (BehaviorTypes.TryGetValue(affixName, out Type type))
            {
                return (IAffixBehavior)Activator.CreateInstance(type);
            }
            return null;
        }

        public static bool IsRegistered(string affixName) => BehaviorTypes.ContainsKey(affixName);
        public static IEnumerable<string> GetAllAffixNames() => BehaviorTypes.Keys;
        public static void ClearAll() => BehaviorTypes.Clear();
        public static int Count => BehaviorTypes.Count;
    }

    public static class AffixBehaviorUtils { }
}