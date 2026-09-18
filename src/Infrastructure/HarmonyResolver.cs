using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EliteEnemies.Infrastructure
{
    /// <summary>
    /// 0Harmony 的程序集解析策略。
    ///
    /// <para><b>要解决的问题</b>：绝大多数玩家会订阅一个单独的 Harmony 前置模组
    /// （创意工坊 3589088839「HarmonyLoadMod」，分发的是 0Harmony 2.4.1）。
    /// 如果本模组也无条件加载自己那一份 0Harmony，同一进程里就会出现**两份
    /// HarmonyLib**——因为两者版本不同，程序集身份不同，CLR 不会合并。
    /// 后果是两块互不可见的补丁注册表：别的模组 patch 了同一方法时，
    /// 双方都以为自己是唯一的补丁者，冲突无从察觉。</para>
    ///
    /// <para><b>策略</b>（三档，前者优先）：
    /// ① 复用进程里**已加载**的 0Harmony，不论版本；
    /// ② 找不到才加载模组自带的 <c>libs\0Harmony.dll</c>；
    /// ③ 都没有则放弃，让 CLR 报出它原本的错误。</para>
    ///
    /// <para><b>为什么自带的那份要放在 libs\ 子目录</b>：
    /// <c>AppDomain.AssemblyResolve</c> **只在常规解析失败后**才触发。
    /// 若 0Harmony.dll 与 EliteEnemies.dll 同目录，<c>Assembly.LoadFrom</c> 探测同目录
    /// 必然成功，本处理器永远不会被调用，"优先复用已加载的"也就无从实现。
    /// 放进子目录是为了让常规探测失败，从而把控制权交给这里。
    /// 代价是：本模组的部署布局中，0Harmony.dll 的位置与常见模组不同。</para>
    /// </summary>
    internal static class HarmonyResolver
    {
        private const string HarmonyAssemblyName = "0Harmony";
        private const string BundledRelativePath = @"libs\0Harmony.dll";

        private static bool _registered;
        private const string Unresolved = "尚未解析";
        private static string _source = Unresolved;

        /// <summary>
        /// 本次最终用了哪一份 0Harmony，供启动日志显示。
        ///
        /// <para>⚠ <b>不能只返回 <c>_source</c></b>：那个字段只在
        /// <see cref="OnAssemblyResolve"/> 里赋值，而**解析器只在常规解析失败时才被调用**。
        /// 前置模组分发的 0Harmony 版本与我们编译引用的一致时（这是常态，见 AGENT.md §3.3：
        /// 两份逐字节相同），CLR 直接从"已加载"列表里取走，**解析器根本没机会运行**
        /// ⇒ <c>_source</c> 永远停在初值，日志打出「尚未解析」——**恰恰在最该起作用的
        /// 场景下失效**。</para>
        ///
        /// <para>所以这里补一条兜底：解析器没被调用，**本身就是结论**——说明它已经加载好了。
        /// 此时去进程里查一下版本，把"到底用了哪一份"如实报出来。</para>
        /// </summary>
        internal static string Source
        {
            get
            {
                if (!string.Equals(_source, Unresolved, StringComparison.Ordinal)) return _source;

                Assembly loaded = FindLoaded();
                return loaded != null
                    ? $"复用已加载的 {loaded.GetName().Version}（版本完全匹配，未触发解析器）"
                    : "尚未用到 0Harmony（解析器未被调用，进程里也没有已加载的）";
            }
        }

        /// <summary>在进程里找已加载的 0Harmony；没有返回 null。</summary>
        private static Assembly FindLoaded()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                if (string.Equals(assembly.GetName().Name, HarmonyAssemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }
            return null;
        }

        /// <summary>
        /// 通过模块初始化器注册——这是本程序集里**最早**能运行的托管代码，
        /// 早于任何类型的静态构造函数与 ModBehaviour.Awake。
        /// </summary>
        [ModuleInitializer]
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // 解析失败的路径必须安静地返回 null：这里一旦抛异常，
            // CLR 会把它包装成难以理解的加载错误，掩盖真正的原因。
            try
            {
                var requested = new AssemblyName(args.Name);
                if (!string.Equals(requested.Name, HarmonyAssemblyName, StringComparison.Ordinal))
                {
                    return null;
                }

                // ① 复用已加载的。这是常态路径——玩家装了 Harmony 前置模组时走这里。
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic) continue;
                    if (!string.Equals(assembly.GetName().Name, HarmonyAssemblyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // 版本可能与本模组的编译引用不同（例如编译引用 2.3.3、前置模组加载的是 2.4.1）。
                    // AssemblyResolve 的返回值不受版本校验约束，正是靠这一点消除版本错配。
                    _source = $"复用已加载的 {assembly.GetName().Version}";
                    return assembly;
                }

                // ② 兜底：玩家没订阅前置模组时，用模组自带的那份。
                string bundled = Path.Combine(ModDirectory, BundledRelativePath);
                if (File.Exists(bundled))
                {
                    Assembly loaded = Assembly.LoadFrom(bundled);
                    _source = $"加载自带的 {loaded.GetName().Version}（未检测到已加载的 Harmony）";
                    return loaded;
                }

                // ③ 都没有。
                _source = "未找到任何 0Harmony（既无已加载的，也无自带的）";
                return null;
            }
            catch (Exception e)
            {
                _source = $"解析失败：{e.GetType().Name} {e.Message}";
                return null;
            }
        }

        /// <summary>本模组 DLL 所在目录。部署后即模组目录。</summary>
        private static string ModDirectory
        {
            get
            {
                string location = typeof(HarmonyResolver).Assembly.Location;
                return string.IsNullOrEmpty(location) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(location);
            }
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// netstandard2.1 的 BCL 里没有 <c>ModuleInitializerAttribute</c>（它是 .NET 5 才加入的），
    /// 这里自行声明一份。编译器只认类型全名，因此这个 polyfill 可以正常驱动
    /// <c>[ModuleInitializer]</c> 的代码生成。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
