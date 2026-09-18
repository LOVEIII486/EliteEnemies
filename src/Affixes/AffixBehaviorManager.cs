using System;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条名 → 构造委托 的注册表。
    ///
    /// <para>表里存的是 <see cref="Func{IAffixBehavior}"/> 而**不是** <see cref="Type"/>：
    /// 构造器签名由委托类型在编译期钉死，实例化不再经过 <c>Activator</c>（反射）。
    /// 登记工作由 <see cref="AffixBehaviorRegistration"/> 扫描程序集自动完成——
    /// 不再有手写清单，「新增行为类忘了登记」这一类静默失效从根上消失。</para>
    /// </summary>
    public static class AffixBehaviorManager
    {
        private const string LogTag = "[EliteEnemies.AffixBehavior]";

        private static readonly Dictionary<string, Func<IAffixBehavior>> Factories =
            new Dictionary<string, Func<IAffixBehavior>>(StringComparer.Ordinal);

        /// <summary>
        /// 登记一个词条行为。affixName 为空或委托为 null 时忽略
        /// （与旧实现一致：拿不到名字的行为不登记）。
        /// 同一个词条名被登记两次时后者覆盖前者，并打警告——一个词条名只应对应一个行为类。
        /// </summary>
        public static void RegisterBehavior(string affixName, Func<IAffixBehavior> factory)
        {
            if (string.IsNullOrEmpty(affixName) || factory == null) return;

            if (Factories.ContainsKey(affixName))
            {
                Debug.LogWarning($"{LogTag} 词条名 '{affixName}' 被重复登记，后登记的覆盖前者");
            }

            Factories[affixName] = factory;
        }

        /// <summary>按词条名构造一个行为实例；该词条名未登记时返回 null（调用方需判空）。</summary>
        public static IAffixBehavior CreateBehaviorInstance(string affixName)
        {
            if (affixName != null && Factories.TryGetValue(affixName, out Func<IAffixBehavior> factory))
            {
                return factory();
            }
            return null;
        }

        /// <summary>
        /// 该词条名是否已登记。
        ///
        /// <para>⚠ 入参为 null 时返回 false，**不抛异常**——调用方（如精英生成流程）
        /// 常在判空之前先问一句，抛异常会打断生成。</para>
        /// </summary>
        public static bool IsRegistered(string affixName)
            => affixName != null && Factories.ContainsKey(affixName);

        public static IEnumerable<string> GetAllAffixNames() => Factories.Keys;
        public static void ClearAll() => Factories.Clear();
        public static int Count => Factories.Count;
    }
}
