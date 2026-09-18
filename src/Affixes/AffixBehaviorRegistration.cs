using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条行为的自动登记 + 词条名与词条池的一一对应校验。
    ///
    /// 历史：这里曾是一份**手写的 40 行** RegisterBehavior&lt;T&gt;() 清单。
    /// 新增一个行为类却忘了在这里加一行 → 该词条静默不生效，没有任何提示。
    /// 现在改为扫描本程序集自动登记，并在启动后核对「登记的词条名」与「EliteAffixes.Pool 的键」
    /// 是否一一对应，把漏登记/拼错名变成启动日志里看得见的一条。
    /// </summary>
    public static class AffixBehaviorRegistration
    {
        private const string LogTag = "[EliteEnemies.AffixBehavior]";

        private static bool _correspondenceChecked;

        public static void RegisterAllBehaviors()
        {
            DiscoverAndRegister(typeof(AffixBehaviorRegistration).Assembly);

            Debug.Log($"[EliteEnemies.AffixBehavior] 注册完成，共 {AffixBehaviorManager.Count} 个词缀行为类型");

            // 同步校验即可。
            //
            // 这里**曾经**必须推迟一帧：那时 EliteAffixes.Pool 是在自己的静态字段初始化器里
            // 调用 GetText 取词的，所以读 Pool 就等于把它的静态初始化提前到本地化就绪之前，
            // 把全部词条名永久冻结在兜底文本上。
            //
            // 现在 Pool 存的是**键**（LocalizedText），初始化器里不再取译文，读取不产生任何
            // 时机依赖——推迟的理由已经消失。见 src/Localization/LocalizedText.cs。
            ValidateAffixCorrespondence();
        }

        /// <summary>
        /// 扫描程序集，把「实现 IAffixBehavior 且可实例化」的类型登记进 AffixBehaviorManager。
        ///
        /// <b>筛选条件（四条全满足才登记）：</b>
        /// <list type="number">
        /// <item>是 class 且非 abstract —— 排除接口自身与抽象基类 AffixBehaviorBase</item>
        /// <item>实现了 IAffixBehavior（IUpdateable/ICombat 都继承自它，故一并覆盖）</item>
        /// <item>不是开放泛型（<c>ContainsGenericParameters</c>）—— 开放泛型没有可绑定的构造器</item>
        /// <item>类型可见（public 或 public 嵌套）且有无参公开构造器 —— 否则构造不出来</item>
        /// </list>
        ///
        /// 第 1、2 条不满足的类型（接口、抽象基类、无关类型）静默跳过，这是预期内的排除；
        /// 满足 1、2 却不满足 3、4 的类型说明「长得像行为类却登记不上」，逐条打**错误**日志——
        /// 漏登记永远不该是静默的。构造器非 public（如 protected/private）同样归入此类：
        /// 它多半是刻意的非行为类，但宁可报出来让人确认。
        /// </summary>
        private static void DiscoverAndRegister(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // GetTypes 只要有一个类型加载失败就整体抛出。把能拿到的继续处理，
                // 但必须报错——被丢掉的那些类型就是静默漏登记。
                Debug.LogError($"{LogTag} 扫描程序集时部分类型加载失败，将跳过它们: {ex}");
                types = ex.Types;
            }

            foreach (Type type in types)
            {
                if (type == null || !type.IsClass || type.IsAbstract) continue;
                if (!typeof(IAffixBehavior).IsAssignableFrom(type)) continue;

                bool visible = type.IsPublic || type.IsNestedPublic;
                if (type.ContainsGenericParameters || !visible || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"{LogTag} {type.FullName} 实现了 IAffixBehavior，但不是可实例化的具体类型" +
                                   $"（可见={visible} / 无参公开构造器={(type.GetConstructor(Type.EmptyTypes) != null)}），" +
                                   "无法自动登记——该词条会静默不生效");
                    continue;
                }

                Func<IAffixBehavior> factory = CompileFactory(type);
                if (factory == null) continue;

                string affixName;
                try
                {
                    // 建一个探针实例只为读 AffixName（AffixName 是实例属性，无法从 Type 上取）。
                    // 旧实现 RegisterBehavior<T>() 同样 new 了一个实例，这里行为一致。
                    // 该实例随即被丢弃，不参与任何生命周期。
                    affixName = factory().AffixName;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogTag} 构造 {type.FullName} 以读取 AffixName 失败，已跳过: {ex}");
                    continue;
                }

                if (string.IsNullOrEmpty(affixName))
                {
                    Debug.LogError($"{LogTag} {type.FullName} 的 AffixName 为空，无法登记——该词条会静默不生效");
                    continue;
                }

                AffixBehaviorManager.RegisterBehavior(affixName, factory);
            }
        }

        /// <summary>
        /// 把「构造一个行为实例」编译成委托，**每个类型只在这里做一次**。
        ///
        /// 旧实现每次 CreateBehaviorInstance 都走 <c>Activator.CreateInstance(type)</c>，
        /// 即每次创建实例都付一遍反射开销；编译出的委托直接调构造器，注册表里存的也是它，
        /// 运行期不再有反射。
        ///
        /// 这里用到 Expression，是因为「从运行期拿到的 Type 创建实例」在 C# 里没有非反射写法
        /// （<c>new T()</c> 对泛型参数同样编译成 Activator 调用）。它发生在启动期一次，
        /// 且**不依赖任何字符串成员名**，因此不属于 03 篇 §2.1 D1 那一类「改名即静默失效」的反射——
        /// 构造器消失会让这里编译委托失败并打错误日志，而不是悄悄少一个词条。
        /// </summary>
        private static Func<IAffixBehavior> CompileFactory(Type type)
        {
            try
            {
                ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null) return null;

                return Expression.Lambda<Func<IAffixBehavior>>(
                    Expression.Convert(Expression.New(ctor), typeof(IAffixBehavior))).Compile();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 为 {type.FullName} 生成构造委托失败，该词条会静默不生效: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 校验「登记了的词条名」与「EliteAffixes.Pool 的键」是否一一对应，结果写进启动日志。
        ///
        /// 两个方向都会报：
        /// <list type="bullet">
        /// <item>登记了行为但词条池里没有同名条目 —— <b>错误</b>。词条是从 EliteAffixes.Pool.Keys
        /// 里挑出来的（EliteEnemyCore.cs:330），这个名字永远不会被选中，该行为类等于死代码。
        /// 多半是 AffixName 拼错，或词条数据被删了。</item>
        /// <item>词条池里有条目但没有行为类 —— <b>警告</b>。这**不一定是错**：纯数值/纯掉落词条
        /// 本来就不需要行为类。列出来只为让人确认「是有意为之，还是漏写了行为类」。</item>
        /// </list>
        ///
        /// 读取 <c>EliteAffixes.Pool</c> 不产生时机依赖：它存的是键，不取译文。
        /// （这里原先有一条「必须等本地化就绪才能读」的约束，随词条表改存 LocalizedText 而消失。）
        /// </summary>
        public static void ValidateAffixCorrespondence()
        {
            if (_correspondenceChecked) return;
            _correspondenceChecked = true;

            var poolKeys = new HashSet<string>(EliteAffixes.Pool.Keys, StringComparer.Ordinal);
            var behaviorNames = new List<string>(AffixBehaviorManager.GetAllAffixNames());

            var orphanBehaviors = new List<string>();
            foreach (string name in behaviorNames)
            {
                if (!poolKeys.Contains(name)) orphanBehaviors.Add(name);
            }

            var namesWithBehavior = new HashSet<string>(behaviorNames, StringComparer.Ordinal);
            var behaviorlessAffixes = new List<string>();
            foreach (string key in EliteAffixes.Pool.Keys)
            {
                if (!namesWithBehavior.Contains(key)) behaviorlessAffixes.Add(key);
            }

            if (orphanBehaviors.Count > 0)
            {
                Debug.LogError($"{LogTag} 以下 {orphanBehaviors.Count} 个词条行为已登记，但词条池(EliteAffixes.Pool)里没有同名条目，" +
                               $"永远不会被选中（检查 AffixName 是否拼错）: {JoinSorted(orphanBehaviors)}");
            }

            if (behaviorlessAffixes.Count > 0)
            {
                Debug.LogWarning($"{LogTag} 以下 {behaviorlessAffixes.Count} 个词条在词条池里存在，但没有对应的行为类" +
                                 $"（纯数值/掉落词条属正常，其余请确认是否漏了行为类）: {JoinSorted(behaviorlessAffixes)}");
            }

            if (orphanBehaviors.Count == 0 && behaviorlessAffixes.Count == 0)
            {
                Debug.Log($"{LogTag} 词条与行为类一一对应校验通过（{behaviorNames.Count} 个行为 / {poolKeys.Count} 个词条）");
            }
        }

        /// <summary>排序后拼成一行——Pool 与反射的顺序都不保证，排序只为日志可读、可比对。</summary>
        private static string JoinSorted(List<string> names)
        {
            names.Sort(StringComparer.Ordinal);
            return string.Join(", ", names);
        }
    }
}
