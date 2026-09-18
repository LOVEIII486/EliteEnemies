using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.Infrastructure
{
    /// <summary>
    /// Harmony 补丁的唯一持有者。
    ///
    /// <para><b>为什么单独建一个类，而不是直接在 ModBehaviour 里持有 Harmony 实例</b>：
    /// 游戏加载本模组的第一步是
    /// <c>Assembly.LoadFrom(dllPath).GetType("EliteEnemies.ModBehaviour")</c>。
    /// 如果 <c>ModBehaviour</c> 有一个 <c>Harmony</c> 类型的字段，那么**在类型加载阶段**
    /// 就要解析 0Harmony；而 <c>HarmonyResolver</c> 的解析器是靠
    /// <c>[ModuleInitializer]</c> 注册的，时机是否早于类型加载并不由我们控制。
    /// 把 Harmony 挪到这个类里之后，<c>ModBehaviour</c> 的类型签名中不再出现任何
    /// HarmonyLib 类型，它的加载也就不再需要 0Harmony——解析实际发生在
    /// <c>PatchAll()</c> 被调用的那一刻，那时解析器必定已经就绪。</para>
    ///
    /// <para><b>与前置模组的关系</b>：补丁打在**哪一个** HarmonyLib 实例上，
    /// 由 <see cref="HarmonyResolver"/> 决定（优先复用进程里已加载的那份）。
    /// 这里只负责持有与调用，不关心解析策略。</para>
    /// </summary>
    internal static class HarmonyBootstrap
    {
        private const string LogTag = "[EliteEnemies]";
        private const string HarmonyId = "com.eliteenemies";

        private static Harmony _harmony;

        /// <summary>补丁当前是否已应用。</summary>
        internal static bool IsPatched => _harmony != null;

        /// <summary>
        /// 创建 Harmony 实例并应用本程序集内全部 <c>[HarmonyPatch]</c>。
        /// 幂等：重复调用不会重复打补丁。
        /// </summary>
        internal static void PatchAll()
        {
            if (_harmony != null)
            {
                Debug.Log($"{LogTag}  Harmony 补丁已应用，跳过");
                return;
            }

            // 触碰 Harmony 类型的动作放在这里，越晚越好——
            // 此时 HarmonyResolver 已经通过 ModuleInitializer 完成注册。
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll();

            Debug.Log($"{LogTag}  Harmony 补丁已应用（0Harmony 来源：{HarmonyResolver.Source}）");
        }

        /// <summary>撤销本模组的全部补丁，并释放实例。幂等。</summary>
        internal static void UnpatchAll()
        {
            if (_harmony == null)
            {
                return;
            }

            _harmony.UnpatchAll(_harmony.Id);
            _harmony = null;

            Debug.Log($"{LogTag}  Harmony 补丁已移除");
        }
    }
}
