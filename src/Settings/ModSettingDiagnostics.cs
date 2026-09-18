using System;
using System.Linq;
using System.Reflection;
using Duckov;            // StrongNotification（namespace Duckov，不是全局命名空间）
using Duckov.Modding;
using UnityEngine;

namespace EliteEnemies.Settings
{
    /// <summary>
    /// ModSetting 集成状态的诊断与提示。
    ///
    /// <para><b>为什么单独建这个类，而不改 <c>ModSettingsApi/ModSettingAPI.cs</c>：</b>
    /// 那个文件是 <b>ModSetting 作者提供的接口文件</b>（官方 README 的「方式一：拷入
    /// ModSettingAPI.cs」），拷贝进来是为了让我们能编译。它**不属于本模组**，
    /// 修改它会让日后与上游同步变得困难。所以本模组对它的定位是「只读的第三方 API」，
    /// 所有容错逻辑都放在这里。</para>
    ///
    /// <para><b>为什么这里用反射：</b>属规范 A2 类例外——对 ModSetting 硬引用会让
    /// <b>没订阅它的玩家模组加载失败</b>。这里是同一个理由下的只读诊断，
    /// 只在启动期跑一次，不影响热路径。</para>
    ///
    /// <para><b>要解决的三个实际问题</b>（均由实测确认，不是假想）：
    /// <list type="number">
    /// <item>没订阅 ModSetting 时，原先 <c>ModBehaviour.InitializeSettings</c> 直接
    /// <c>return</c>，导致 <c>GameConfig.Init()</c> 与 <c>UpdateConfig</c> 全被跳过——
    /// 模组不是「不能改配置」，而是**配置从未被应用**。现已改为无条件应用（见 ModBehaviour）。</item>
    /// <item>订阅了但没启用时，玩家看不出原因。</item>
    /// <item>版本不一致时没有任何提示——<c>ModSettingAPI.VersionAvailable()</c> 是死代码
    /// （它要求 <c>VERSION</c> 字段是 <c>float</c>，而 ModSetting 实际是 <c>System.Version</c>，
    /// 条件恒假；且调用处丢弃了返回值）。本类重新实现这个检查。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class ModSettingDiagnostics
    {
        private const string LogTag = "[EliteEnemies.Settings]";

        /// <summary>ModSetting 的模组名与入口类型全名（与 <c>ModSettingAPI</c> 中的常量一致）。</summary>
        private const string ModSettingName = "ModSetting";
        private const string ModSettingEntryType = "ModSetting.ModBehaviour";

        /// <summary>
        /// 本模组所对接的 ModSetting **主次版本**。
        ///
        /// <para>⚠ 这个常量必须与 <c>ModSettingsApi/ModSettingAPI.cs</c> 里的 <c>VERSION</c>
        /// 保持一致（那份是 `0.5.0`）。<b>无法直接读取它</b>——它是那个文件里的 private 常量，
        /// 而该文件不可修改。所以这里是「同一事实的第二份副本」，改上游时记得同步。</para>
        ///
        /// <para>只比较主次版本：补丁号差异（0.5.0 / 0.5.1）不构成不兼容，不该打扰玩家。</para>
        /// </summary>
        private static readonly Version ExpectedApiVersion = new Version(0, 5);

        private static bool _reported;

        /// <summary>
        /// 诊断当前 ModSetting 集成状态，并把结论记录到日志。
        /// 只在第一次调用时输出（避免 ModSetting 延迟激活时重复刷屏）。
        /// </summary>
        /// <param name="initSucceeded">上游 <c>ModSettingAPI.Init</c> 的返回值——我们不改变它，只解释它。</param>
        internal static void Diagnose(bool initSucceeded)
        {
            if (_reported) return;
            _reported = true;

            Type entryType = FindEntryType();

            if (initSucceeded)
            {
                Debug.Log($"{LogTag} ModSetting 集成正常（{DescribeVersion(entryType)}）");
                WarnIfVersionMismatch(entryType);
                return;
            }

            if (entryType != null)
            {
                // 程序集已加载，但 Init 仍然失败。可能是方法签名变了，也可能是启用状态异常。
                if (!IsEnabled(entryType))
                {
                    Report(ModSettingState.LoadedButDisabled, entryType);
                }
                else
                {
                    Report(ModSettingState.ApiMismatch, entryType);
                }
                return;
            }

            // 程序集没加载。用游戏自己的模组清单区分「没订阅」与「订阅了但没启用」——
            // ModManager.modInfos 是**扫描到的全部模组**（不论是否激活），
            // GetCurrentActiveModList 是已激活的。两者一比就知道。
            bool discovered = IsModDiscovered();
            Report(discovered ? ModSettingState.InstalledNotLoaded : ModSettingState.NotSubscribed, null);
        }

        private static void Report(ModSettingState state, Type entryType)
        {
            switch (state)
            {
                case ModSettingState.NotSubscribed:
                    Debug.LogWarning(
                        $"{LogTag} 未检测到 ModSetting（前置配置模组）。\n" +
                        "  影响：设置界面不可用，无法在游戏内调整本模组的配置项。\n" +
                        "  不影响：精英敌人系统本身照常运行，将使用内置默认配置。\n" +
                        "  处理：如需调整配置，请订阅并启用 ModSetting 后重启游戏。");
                    Notify("未检测到 ModSetting",
                        "精英敌人模组将以默认配置运行\n订阅并启用 ModSetting 后可调整配置");
                    break;

                case ModSettingState.InstalledNotLoaded:
                    Debug.LogWarning(
                        $"{LogTag} 检测到 ModSetting 已安装，但**未被激活**。\n" +
                        "  常见原因：在模组列表中未勾选它，或它加载失败。\n" +
                        "  影响：设置界面不可用；本模组以默认配置运行。\n" +
                        "  处理：在游戏内模组列表里启用 ModSetting，然后重启游戏。");
                    Notify("ModSetting 未启用",
                        "已安装但未激活\n请在模组列表中启用后重启");
                    break;

                case ModSettingState.LoadedButDisabled:
                    Debug.LogWarning(
                        $"{LogTag} ModSetting 已加载，但处于**停用状态**。\n" +
                        "  影响：设置界面不可用；本模组以默认配置运行。\n" +
                        "  处理：在游戏内重新启用 ModSetting。");
                    Notify("ModSetting 已停用",
                        "精英敌人模组将以默认配置运行");
                    break;

                case ModSettingState.ApiMismatch:
                    Debug.LogWarning(
                        $"{LogTag} ModSetting 已加载并启用，但接口对接失败。\n" +
                        $"  当前 ModSetting 版本：{DescribeVersion(entryType)}；" +
                        $"本模组对接的版本：{ExpectedApiVersion}。\n" +
                        "  常见原因：ModSetting 更新后改名或删除了本模组依赖的方法。\n" +
                        "  影响：设置界面不可用；本模组以默认配置运行，其余功能正常。\n" +
                        "  处理：等待本模组更新，或在评论区反馈。");
                    Notify("ModSetting 接口不兼容",
                        $"当前版本 {DescribeVersion(entryType)}，与本模组对接的 {ExpectedApiVersion} 不一致\n设置界面不可用，其余功能正常");
                    break;
            }
        }

        /// <summary>版本不一致只警告，不阻断——「能用就继续用」。</summary>
        private static void WarnIfVersionMismatch(Type entryType)
        {
            Version actual = ReadVersion(entryType);
            if (actual == null) return;

            if (actual.Major != ExpectedApiVersion.Major || actual.Minor != ExpectedApiVersion.Minor)
            {
                Debug.LogWarning(
                    $"{LogTag} ModSetting 版本与本模组对接的版本不一致：\n" +
                    $"  实际 {actual}；本模组对接 {ExpectedApiVersion}。\n" +
                    "  设置功能仍会尝试使用；若出现异常请反馈。");
            }
        }

        private static string DescribeVersion(Type entryType)
        {
            Version v = ReadVersion(entryType);
            return v == null ? "版本未知" : v.ToString();
        }

        /// <summary>
        /// 读取 ModSetting 的 <c>VERSION</c> 字段（<c>System.Version</c>）。
        ///
        /// <para>📌 注意 ModSetting 有**两个**版本字段，别读错：
        /// <c>Version</c>（<c>float</c>，0.5f）与 <c>VERSION</c>（<c>System.Version</c>，0.5.1）。
        /// 上游那份 <c>ModSettingAPI.VersionAvailable()</c> 读的是 <c>VERSION</c>
        /// 却要求类型为 <c>float</c>——条件恒假，所以它从未生效。本方法读对的那个。</para>
        /// </summary>
        private static Version ReadVersion(Type entryType)
        {
            try
            {
                FieldInfo field = entryType.GetField("VERSION", BindingFlags.Public | BindingFlags.Static);
                return field?.GetValue(null) as Version;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 读取 ModSetting 版本失败：{e.Message}");
                return null;
            }
        }

        /// <summary>ModSetting 的 <c>Enable</c> 属性：其 OnEnable 置 true、OnDisable 置 false。</summary>
        private static bool IsEnabled(Type entryType)
        {
            try
            {
                PropertyInfo property = entryType.GetProperty("Enable", BindingFlags.Public | BindingFlags.Static);
                object value = property?.GetValue(null);
                return value is bool enabled && enabled;
            }
            catch
            {
                // 读不到就当作「不确定」，交给调用方按更保守的那条路径处理。
                return true;
            }
        }

        private static Type FindEntryType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                Type type = assembly.GetType(ModSettingEntryType);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// ModSetting 是否被游戏**扫描到**（不论是否激活）。
        /// 用游戏自己的清单，而不是去猜工坊目录——避免硬编码工坊 ID。
        /// </summary>
        private static bool IsModDiscovered()
        {
            try
            {
                return ModManager.modInfos != null
                    && ModManager.modInfos.Any(m => m.name == ModSettingName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 查询模组清单失败：{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 玩家可见提示。ModSetting 缺失时我们连自己的设置界面都没有，
        /// 所以借游戏自己的通知系统——<c>StrongNotification.Push</c> 只是往静态队列里塞，
        /// 不要求调用时场景里已有实例。
        /// </summary>
        private static void Notify(string mainText, string subText)
        {
            try
            {
                StrongNotification.Push(mainText, subText);
            }
            catch (Exception e)
            {
                // 提示失败不能反过来影响模组运行——它只是锦上添花。
                Debug.LogWarning($"{LogTag} 通知推送失败（不影响模组运行）：{e.Message}");
            }
        }
    }

    internal enum ModSettingState
    {
        /// <summary>程序集未加载，且游戏没扫描到 —— 玩家没订阅。</summary>
        NotSubscribed,

        /// <summary>游戏扫描到了 ModSetting，但它没被激活 —— 已订阅但未启用。</summary>
        InstalledNotLoaded,

        /// <summary>程序集已加载，但 ModSetting 自身处于停用状态。</summary>
        LoadedButDisabled,

        /// <summary>程序集已加载并启用，但接口对接失败（版本/方法不匹配）。</summary>
        ApiMismatch,
    }
}
