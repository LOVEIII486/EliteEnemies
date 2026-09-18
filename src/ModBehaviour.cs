using System;
using System.Collections.Generic;
using Duckov.Modding;
using EliteEnemies.DebugTools;
using EliteEnemies.Affixes;
using EliteEnemies.Buffs;
using EliteEnemies.Combos;
using EliteEnemies.Core;
using EliteEnemies.Loot;
using EliteEnemies.Infrastructure;
using EliteEnemies.Localization;
using EliteEnemies.ModSettingsApi;
using EliteEnemies.Stats;
using EliteEnemies.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EliteEnemies
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static ModBehaviour Instance { get; private set; }
        public static LootItemHelper LootHelper => Instance?._lootItemHelper;
        
        private const string LogTag = "[EliteEnemies]";

        /// <summary>Buff 模块的注册者标识。传进去而不是让模块自己写死，见 InitializeBuffFramework。</summary>
        private const string BuffModuleOwner = "EliteEnemies";
        /// <summary>
        /// 调试工具开关。**唯一实现在 <see cref="DebugTools.DebugSwitch"/>**——
        /// 它必须在 <c>DebugTools</c> 命名空间里，因为有些工具被生产代码直接调用、
        /// 需要**自门控**；开关放在 ModBehaviour 里时它们够不着。
        ///
        /// <para>⚠ 它**不管**本局统计（<c>Stats.SessionStats</c>）：那个是常开的，
        /// 理由见 <see cref="DebugTools.DebugSwitch"/> 的类注释。</para>
        /// </summary>
        private static bool EnableDebugTool => DebugTools.DebugSwitch.Enabled;
        
        private GameObject _lootHelperObject;
        private LootItemHelper _lootItemHelper;
        private GameObject _eggSpawnHelperObject;
        
        private GameObject _debugToolObject;
        
        private bool _isPatched = false;
        // 是否已拿到 ModSetting 的**真实配置**（而非默认值）。
        // 之所以不是简单的「已初始化」布尔量：配置可能先以默认值应用过一次，
        // 等 ModSetting 晚激活后再升级为玩家的真实配置——那一步需要能再进来一次。
        private bool _settingsHaveModSetting = false;
        private bool _sceneHooksInitialized = false;

        // ════════════════════════════════════════════════════════════════════════
        //  子系统表
        // ════════════════════════════════════════════════════════════════════════
        //
        // 历史形态是「OnEnable 里手工调 N 个 InitializeXxx、OnDisable 里手工调 N 个
        // CleanupXxx」——两份清单靠人眼保持同步。实测到的三个后果：
        //
        //   · 数量对不上：10 个 Initialize 对 8 个 Cleanup；
        //   · AffixBehaviorManager.ClearAll() 明明存在却**从无调用点**，于是停用后再启用
        //     会对 40 个词条各刷一条「词条名 'X' 被重复登记」的 LogWarning；
        //   · 顺序没人决定过，只是碰巧与 OnEnable 同序。更隐蔽的是
        //     LocalizationManager.OnSetLanguage 的**退订**散在 OnDisable 里，
        //     而它的订阅藏在 InitializeSettings 内部，两者隔着几十行。
        //
        // 做成表之后：配对由结构保证（每项必须同时给出 Start 与 Stop）、顺序一眼可见、
        // 停机按**逆序**（镜像 teardown，对将来引入依赖的子系统才是对的）。
        // 现有这些 Stop 彼此独立（退订事件 / 反注册 / Clear 字典 / Destroy 对象），
        // 所以逆序对本轮是行为等价的。

        /// <summary>启动时机。两阶段的边界是 <c>info</c> 是否可用，见 AGENT.md §3.5。</summary>
        private enum Phase
        {
            /// <summary>OnEnable 阶段——不依赖 <c>info</c>。</summary>
            Early,

            /// <summary>OnAfterSetup 阶段——**必须**晚于 <see cref="Early"/>：本地化要读 <c>info.path</c>。</summary>
            AfterSetup,
        }

        private sealed class Subsystem
        {
            public readonly string Name;

            /// <summary>启动。**必须幂等**——停用后再启用会再跑一次。</summary>
            public readonly Action Start;

            /// <summary>
            /// 停机。必须与 <see cref="Start"/> 对称；确实无事可做时写空实现**并注明理由**，
            /// 不要留 null —— 那会把「忘了写」与「确实不需要」混成同一种形态，而这一整张表
            /// 存在的意义就是让这两者不再能被混淆。
            /// </summary>
            public readonly Action Stop;

            public readonly Phase Phase;

            /// <summary>只在 <see cref="EnableDebugTool"/> 打开时启停。</summary>
            public readonly bool DebugOnly;

            public Subsystem(string name, Phase phase, Action start, Action stop, bool debugOnly = false)
            {
                Name = name;
                Phase = phase;
                Start = start;
                Stop = stop;
                DebugOnly = debugOnly;
            }
        }

        // 惰性构建，而**不是**字段初始值设定项：C# 不允许在字段初始值设定项里调用实例方法
        // （CS0236），而表里存的是指向实例方法的委托。
        // 之所以也不新增一个 Awake 去构建它：那会引入一条"必须在 OnEnable 之前跑"的隐含时序
        // 要求——正是这张表要消灭的那类东西。
        private List<Subsystem> _subsystems;

        private List<Subsystem> Subsystems => _subsystems ?? (_subsystems = BuildSubsystems());

        private List<Subsystem> BuildSubsystems() => new List<Subsystem>
        {
            // 顺序 = 启动顺序；停机按逆序。
            new Subsystem("ModSetting 激活监听", Phase.Early, SubscribeModActivation, UnsubscribeModActivation),
            // 它此前被塞在 InitializeSettings 的 `if (modSettingAvailable)` 分支里——
            // 于是**没装 ModSetting 的玩家换语言时，我们什么都不刷新**（既包括本模组的
            // 本地化，也包括 Buff 名称/描述往游戏本地化器的注入）。跟设置无关，单独成条。
            new Subsystem("语言变化监听", Phase.Early, SubscribeLanguageChange, UnsubscribeLanguageChange),
            new Subsystem("场景事件钩子", Phase.Early, InitializeSceneHooks, CleanupSceneHooks),
            // ⚠ **不是 debugOnly**：本局统计的用途是"提高玩家出错时那份日志的可分析率"，
            //    它必须出现在正式版里。它对玩家无感（只有场景卸载时打一条，无热键无 UI），
            //    见 SessionStats 的类注释。
            new Subsystem("本局统计", Phase.Early, SessionStats.BeginSession, SessionStats.EndSession),
            new Subsystem("Harmony 补丁", Phase.Early, InitializeHarmonyPatches, CleanupHarmonyPatches),
            new Subsystem("词条行为注册", Phase.Early, InitializeAffixBehaviors, CleanupAffixBehaviors),
            new Subsystem("Buff 框架", Phase.Early, InitializeBuffFramework, CleanupBuffFramework),
            new Subsystem("掉落工具", Phase.Early, InitializeLootHelper, CleanupLootHelper),
            new Subsystem("Egg 生成器", Phase.Early, InitializeEggSpawnHelper, CleanupEggSpawnHelper),
            new Subsystem("调试工具", Phase.Early, InitializeDebugTools, CleanupDebugTools, debugOnly: true),
            new Subsystem("本地化", Phase.AfterSetup, InitializeLocalization, CleanupLocalization),
            new Subsystem("设置", Phase.AfterSetup, InitializeSettings, CleanupSettings),
        };

        private void StartSubsystems(Phase phase)
        {
            foreach (var subsystem in Subsystems)
            {
                if (subsystem.Phase != phase) continue;
                if (subsystem.DebugOnly && !EnableDebugTool) continue;

                // 逐条隔离——与 StopAllSubsystems 对称，但**代价方向相反**：
                // 停机时一条失败只影响它自己的收尾；启动时一条失败会从 OnEnable/OnAfterSetup
                // 冒出去，表现为**整个模组加载失败**，且排在它后面的子系统全都不启动
                // （玩家侧看到的是"模组装了但完全没效果"，日志里只有一条看似无关的异常）。
                // 多数 Initialize* 内部已自带 try/catch，这一层是它们漏掉时的最后兜底。
                try
                {
                    subsystem.Start();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogTag} 启动子系统「{subsystem.Name}」时出错: {ex}");
                }
            }
        }

        private void StopAllSubsystems()
        {
            for (int i = Subsystems.Count - 1; i >= 0; i--)
            {
                var subsystem = Subsystems[i];
                if (subsystem.DebugOnly && !EnableDebugTool) continue;

                // 逐条隔离：一条失败不能阻断其余（同 04 篇 §2「卸载路径的第一原则」）。
                try
                {
                    subsystem.Stop();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogTag} 停用子系统「{subsystem.Name}」时出错: {ex}");
                }
            }
        }

        private void SubscribeModActivation() => ModManager.OnModActivated += OnModActivated;

        private void UnsubscribeModActivation() => ModManager.OnModActivated -= OnModActivated;

        private void SubscribeLanguageChange()
            => SodaCraft.Localizations.LocalizationManager.OnSetLanguage += OnLanguageChanged;

        private void UnsubscribeLanguageChange()
            => SodaCraft.Localizations.LocalizationManager.OnSetLanguage -= OnLanguageChanged;

        /// <summary>
        /// 词条行为注册的停机。
        ///
        /// <para>它此前**没有**对应的清理——而 <c>AffixBehaviorManager.ClearAll()</c> 一直存在、
        /// 从无调用点。后果是：停用后再启用会重新扫描程序集，对 40 个词条各刷一条
        /// 「词条名 'X' 被重复登记」的 LogWarning（<c>RegisterBehavior</c> 对重复登记会告警）。
        /// 清空后重新扫描即恢复幂等。</para>
        /// </summary>
        private void CleanupAffixBehaviors()
        {
            AffixBehaviorManager.ClearAll();
            Debug.Log($"{LogTag}  词条行为注册已清空");
        }

        /// <summary>
        /// 设置的停机。它此前是写在 <c>OnDisable</c> 中间的两行——
        /// 一行字段重置、一行 <c>OnSetLanguage</c> 退订——那实质上就是清理，
        /// 只是没有名字、也不在配对清单上。
        /// </summary>
        private void CleanupSettings()
        {
            // _settingsHaveModSetting 是 InitializeSettings 的提前返回条件；
            // 不重置它会导致「停用再启用后设置不再初始化」。
            //
            // （OnSetLanguage 的退订原先也在这里，现已移入「语言变化监听」子系统——
            //   它跟设置没有关系，见那条目上的注释。）
            _settingsHaveModSetting = false;
        }

        private void OnEnable()
        {
            // 0Harmony 的取材策略见 Infrastructure/HarmonyResolver：
            // 优先复用进程里已加载的那份（玩家通常订阅了 Harmony 前置模组），
            // 没有再退回模组自带的 libs\0Harmony.dll。因此这里不再需要关心它从哪来。
            Instance = this;

            // 启动顺序 = _subsystems 的列表顺序（理由见那张表的注释）。
            // 需要 info 的子系统（本地化、设置）在 OnAfterSetup 里启动。
            StartSubsystems(Phase.Early);
        }

        private void OnDisable()
        {
            // 停机按 _subsystems 的**逆序**——镜像 teardown（理由见那张表的注释）。
            StopAllSubsystems();

            // ⚠ 必须置空。`Instance?.X()` 里的 `?.` 是 C# 的**引用**判空，**不走 Unity 的假空判定**，
            // 所以停用后它会拿到一个"已销毁的 MonoBehaviour 包装"并继续调用，抛
            // MissingReferenceException。`EggSpawnHelper` 一直是这么做的（`EggSpawnHelper.cs:115`），
            // 这里补齐，免得同一个工程教两套做法。
            //
            // 写清楚现状，别把它当成"正在发生的 bug"：现存三处 `Instance?.StartCoroutine(...)`
            // （鸡哥/守卫者/鸭王）都在词条的 `OnEliteInitialized` 里，而它只在精英**诞生**时调用——
            // 停用时补丁已撤销，新精英不会再诞生，所以那条路今天走不到。置空是**提前拆掉隐患**：
            // 谁以后把 `Instance?.X()` 写到停用后仍会跑的路径（每帧回调、受击、场景事件）上，
            // 那一行就会炸。
            if (Instance == this) Instance = null;
        }

        protected override void OnAfterSetup()
        {
            // 第二阶段：到这里 info 才可用（AGENT.md §3.5）。
            StartSubsystems(Phase.AfterSetup);

            // 本地化是 Phase.AfterSetup 里的第一个，到这里才就绪。
            // 推送本身由 LocalizationManager.Initialize 完成后自动做（全量推），
            // 这里只做**各模块自己引用的键**的校验——**不能**放在 Phase.Early 里：
            // 那时本地化还没初始化，什么都查不到。
            EliteBuffRegistry.ValidateLocalization();
        }

        private void InitializeSceneHooks()
        {
            if (_sceneHooksInitialized) return;
            _sceneHooksInitialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Debug.Log($"{LogTag} 场景事件钩子已初始化");
        }

        private void InitializeHarmonyPatches()
        {
            if (_isPatched) return;

            // Harmony 实例的持有与解析策略都在 Infrastructure 下：
            // HarmonyBootstrap 负责持有，HarmonyResolver 负责决定用哪一份 0Harmony
            // （优先复用进程里已加载的，避免与 Harmony 前置模组各持一份）。
            HarmonyBootstrap.PatchAll();
            _isPatched = HarmonyBootstrap.IsPatched;
        }

        private void InitializeAffixBehaviors()
        {
            AffixBehaviorRegistration.RegisterAllBehaviors();
            Debug.Log($"{LogTag}  词缀行为已注册");

            // combo 池 ↔ 词条池的自检挂在这里：这是**唯一**两个池都已就绪、
            // 又不会制造反向依赖的位置。放进 `AffixBehaviorRegistration` 会让
            // `Affixes → Combos` 与既有的 `Combos → Affixes` 成环（依赖矩阵见
            // `docs\词条模块审查与设计.md` §8）。
            EliteComboRegistry.ValidatePool();
        }

        private void InitializeBuffFramework()
        {
            // owner 由调用方传入，而不是写死在 Buff 模块里——那个模块将来要整体搬走，
            // 不该带任何本项目特有的字符串（见 docs\Buff模块审查与设计.md §8）。
            EliteBuffRegistry.RegisterAll(BuffModuleOwner, typeof(EliteBuffRegistry).Assembly);
            Debug.Log($"{LogTag}  Buff框架已初始化");
        }

        private void InitializeLocalization()
        {
            // Initialize 内部会加载 CSV **并把全部文本推给游戏本地化器**
            // （含 EliteLoot_* 那三个掉落来源键——它们本来就是 CSV 键，
            //   早先 EliteLootSystem 里另有一处逐键推送，已随本次收敛删除）。
            LocalizationManager.Initialize(info.path);
            Debug.Log($"{LogTag}  本地化系统已初始化");
        }

        private void InitializeLootHelper()
        {
            if (_lootHelperObject != null) return;

            _lootHelperObject = new GameObject("EliteEnemies_LootHelper");
            _lootItemHelper = _lootHelperObject.AddComponent<LootItemHelper>();
            DontDestroyOnLoad(_lootHelperObject);
            Debug.Log($"{LogTag}  掉落工具已初始化");
        }
        private void InitializeEggSpawnHelper()
        {
            if (_eggSpawnHelperObject != null) return;

            _eggSpawnHelperObject = new GameObject("EliteEnemies_EggSpawnHelper");
            _eggSpawnHelperObject.AddComponent<EggSpawnHelper>();
            DontDestroyOnLoad(_eggSpawnHelperObject);
            Debug.Log($"{LogTag}  EggSpawnHelper 已初始化");
        }
        
        private void InitializeDebugTools()
        {
            if (_debugToolObject != null) return;

            _debugToolObject = new GameObject("EliteEnemies_DebugTools");

            // 掉落导出工具 (F10)：把 LootItemHelper 的物品缓存导成 CSV
            _debugToolObject.AddComponent<EliteEnemies.DebugTools.LootCacheDumper>();

            // 预设清单导出工具 (F9)：导出 资源名 → 本地化键名 的映射
            var logger = _debugToolObject.AddComponent<EliteEnemies.DebugTools.PresetKeyLogger>();
            logger.dumpKey = KeyCode.F9;

            // 工坊标签修正工具 (F8)：游戏上传器每次上传都会把标签覆盖成 ["Mod"]，
            // 这个工具把分类标签写回去。**每次发版后都要按一次**。
            _debugToolObject.AddComponent<EliteEnemies.DebugTools.WorkshopTagFixer>();

            DontDestroyOnLoad(_debugToolObject);
            Debug.Log($"{LogTag} 调试工具已初始化: F8 (工坊标签), F9 (预设清单), F10 (掉落信息)");
        }

        /// <summary>
        /// 应用配置，并在 ModSetting 可用时注册设置界面。
        ///
        /// <para><b>关键：配置的应用与 ModSetting 是否可用无关。</b>
        /// 原先的写法是 <c>if (!ModSettingAPI.Init(info)) { LogError; return; }</c>——
        /// 一旦 ModSetting 缺失，后面的 <c>GameConfig.Init()</c> 与
        /// <c>EliteEnemyCore.UpdateConfig(...)</c> 会被**一并跳过**，
        /// 于是模组不是「不能改配置」，而是**配置从未被应用**。
        /// 现在无论 ModSetting 在不在，都先应用配置（缺失时 <c>GameConfig.Init()</c>
        /// 自己会走 <c>LoadDefaults()</c>）。</para>
        ///
        /// <para><b>可重入，用于「ModSetting 后激活」的场景：</b>
        /// ModSetting 在它自己的 <c>OnAfterSetup</c> 里才加载存档（<c>Saver.Load()</c>），
        /// 所以若它比本模组晚激活，这里第一次读到的会是默认值。本模组订阅了
        /// <c>ModManager.OnModActivated</c>，ModSetting 激活时会再次调进来，
        /// 那时才拿得到玩家的真实配置——所以提前返回的条件是
        /// <c>_settingsHaveModSetting</c>（已拿到真实配置），而不是「已初始化过」。</para>
        /// </summary>
        private void InitializeSettings()
        {
            // 已经拿到 ModSetting 的真实配置，不必再跑
            if (_settingsHaveModSetting) return;

            bool modSettingAvailable = ModSettingAPI.Init(info);

            // 无条件应用配置：ModSetting 可用则读已保存的值，否则用默认值。
            Settings.GameConfig.Init();
            EliteEnemyCore.UpdateConfig(Settings.GameConfig.GetConfig());

            if (modSettingAvailable)
            {
                // 只有设置界面真正依赖 ModSetting
                Settings.SettingsUIRegistration.RegisterUI();
                _settingsHaveModSetting = true;
                Debug.Log($"{LogTag}  设置系统已初始化（含 ModSetting 设置界面）");
            }

            // 诊断并提示。无论成功或失败都调一次——成功时它会顺带核对版本，
            // 失败时它会区分「没订阅 / 订阅了没启用 / 已停用 / 接口不兼容」并告知玩家。
            Settings.ModSettingDiagnostics.Diagnose(modSettingAvailable);
        }
        
        private void CleanupSceneHooks()
        {
            if (!_sceneHooksInitialized) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneHooksInitialized = false;
            Debug.Log($"{LogTag}  场景事件钩子已清理");
        }

        private void CleanupHarmonyPatches()
        {
            if (!_isPatched) return;

            HarmonyBootstrap.UnpatchAll();
            _isPatched = false;
        }

        private void CleanupBuffFramework()
        {
            EliteBuffRegistry.UnregisterAll(BuffModuleOwner);
            BulletDeflectionTracker.Instance.Clear();
            Debug.Log($"{LogTag}  Buff框架已清理");
        }

        private void CleanupLootHelper()
        {
            if (_lootHelperObject == null) return;

            Destroy(_lootHelperObject);
            _lootHelperObject = null;
            _lootItemHelper = null;
            Debug.Log($"{LogTag}  掉落工具已清理");
        }
        private void CleanupEggSpawnHelper()
        {
            if (_eggSpawnHelperObject == null) return;

            Destroy(_eggSpawnHelperObject);
            _eggSpawnHelperObject = null;
            Debug.Log($"{LogTag}  EggSpawnHelper 已清理");
        }
        
        
        private void CleanupDebugTools()
        {
            if (_debugToolObject == null) return;
            Destroy(_debugToolObject);
            _debugToolObject = null;
            Debug.Log($"{LogTag} 调试工具已清理");
        }

        private void CleanupLocalization()
        {
            LocalizationManager.Cleanup();
            Debug.Log($"{LogTag}  本地化系统已清理");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SessionStats.OnSceneLoaded(scene.name);
            EliteLootSystem.ClearCache();

            Debug.Log($"{LogTag}  场景已加载: {scene.name}");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // ⚠ 本局统计**不随场景清零**（它是"本局累计"——调掉率要大样本，
            //    单场景十几只精英说明不了问题），所以这里只有输出、没有 Reset。
            //    清零在子系统 Start/Stop，见 SessionStats.BeginSession。
            SessionStats.DumpSummary();

            EliteLootSystem.ClearCache();

            // 与上面那条同类：清的都是「跨场景会残留、且在新场景里已经无意义」的状态。
            // 它必须在这里清，而不是靠别处——理由见 ClearIgnoredPresets 的注释
            // （Unity 会在非叠加式加载场景时自动回收临时预设，回收后实例 ID 会被复用）。
            EliteEnemyCore.ClearIgnoredPresets();

            // 同上：按角色记账的静态字典（只增不减），场景边界清空
            EliteGarbledLabel.Clear();

            // 副本账本：与上面几条同类，都是"这一局攒下了什么"的收尾汇总。
            // 它自检「创建 = 各条释放路径之和 + 跟踪中」，不平就报错（调试版才有输出）。
            EggSpawnHelper.Instance?.DumpPresetLedger($"场景卸载: {scene.name}");

            // 同上：按角色对象记账的集合，角色被直接销毁（没走 RemoveBuff）时会留下死引用
            BulletDeflectionTracker.Instance.Clear();
        }

        private void OnModActivated(ModInfo modInfo, Duckov.Modding.ModBehaviour behaviour)
        {
            if (modInfo.name != "ModSetting") return;
            InitializeSettings();
        }

        private void OnLanguageChanged(SystemLanguage lang)
        {
            // Refresh 内部会重新加载 CSV **并重推**给游戏本地化器
            // （游戏的 override 不会随语言自动更新，见 LocalizationManager.PushToGame）。
            LocalizationManager.Refresh();

            // ModSetting 的**唯一特殊处理**：它的控件文案是注册时传进去的**纯文本**，
            // 它既不解析 key、换语言时也不替模组重取，所以只能清掉重建。
            // 其余一切都走"注册进游戏本地化 → 从游戏读"这条统一路径，不需要刷新。
            if (_settingsHaveModSetting) Settings.SettingsUIRegistration.Reregister();
        }
    }
}