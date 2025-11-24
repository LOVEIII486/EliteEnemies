using System.Collections.Generic;
using HarmonyLib;
using Duckov.Modding;
using EliteEnemies.BuffsSystem;
using EliteEnemies.BuffsSystem.Effects;
using EliteEnemies.DebugTool;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EliteEnemies
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static ModBehaviour Instance { get; private set; }
        public static LootItemHelper LootHelper => Instance?._lootItemHelper;
        
        private const string LogTag = "[EliteEnemies]";
        private const bool EnableDevSpawn = false;
        private const bool EnableDevLoot = false;
        private const bool EnableLootAlgorithmCheck = false;
        
        private Harmony _harmony;
        
        private GameObject _lootHelperObject;
        private LootItemHelper _lootItemHelper;
        private GameObject _eggSpawnHelperObject;
        
        private GameObject _spawnHelperObject;
        private GameObject _lootVerifierObject;
        
        private bool _isPatched = false;
        private bool _settingsInitialized = false;
        private bool _sceneHooksInitialized = false;

        private void OnEnable()
        {
            HarmonyLoad.Load0Harmony();
            Instance = this;

            InitializeSceneHooks();
            InitializeHarmonyPatches();
            InitializeAffixBehaviors();
            InitializeBuffFramework();
            InitializeLootHelper();
            InitializeEggSpawnHelper();
            
            
            if (EnableLootAlgorithmCheck)
            {
                InitializeLootVerifier();
            }
            if (EnableDevSpawn)
            {
                InitializeSpawnHelper();
            }
            ModManager.OnModActivated += OnModActivated;
        }

        private void OnDisable()
        {
            ModManager.OnModActivated -= OnModActivated;
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            
            CleanupSceneHooks();
            CleanupHarmonyPatches();
            CleanupLocalization();
            CleanupBuffFramework();
            CleanupLootHelper();
            CleanupEggSpawnHelper();
            
            CleanupLootVerifier();
            CleanupSpawnHelper();
            
            _settingsInitialized = false;
        }

        protected override void OnAfterSetup()
        {
            InitializeLocalization();
            InitializeSettings();
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
            
            if (_harmony == null)
            {
                _harmony = new Harmony("com.eliteenemies");
            }
            
            _harmony.PatchAll();
            _isPatched = true;
            Debug.Log($"{LogTag}  Harmony补丁已应用");
        }

        private void InitializeAffixBehaviors()
        {
            AffixBehaviors.AffixBehaviorRegistration.RegisterAllBehaviors();
            Debug.Log($"{LogTag}  词缀行为已注册");
        }

        private void InitializeBuffFramework()
        {
            EliteBuffRegistry.Instance.Initialize();
            Debug.Log($"{LogTag}  Buff框架已初始化");
        }

        private void InitializeLocalization()
        {
            LocalizationManager.Initialize(info.path);
            Debug.Log($"{LogTag}  本地化系统已初始化");
        }

        private void InitializeLootHelper()
        {
            if (_lootHelperObject != null) return;

            _lootHelperObject = new GameObject("EliteEnemies_LootHelper");
            _lootItemHelper = _lootHelperObject.AddComponent<LootItemHelper>();
            _lootItemHelper.debugMode = EnableDevLoot;
            DontDestroyOnLoad(_lootHelperObject);
            Debug.Log($"{LogTag}  掉落工具已初始化");
        }

        private void InitializeSpawnHelper()
        {
            if (_spawnHelperObject != null) return;

            _spawnHelperObject = new GameObject("EliteEnemies_SpawnHelper");
            var helper = _spawnHelperObject.AddComponent<SpawnHelper>();
            helper.enableSpawning = true;
            DontDestroyOnLoad(_spawnHelperObject);
            Debug.Log($"{LogTag}  生成调试工具已初始化（开发模式）");
        }

        private void InitializeEggSpawnHelper()
        {
            if (_eggSpawnHelperObject != null) return;

            _eggSpawnHelperObject = new GameObject("EliteEnemies_EggSpawnHelper");
            _eggSpawnHelperObject.AddComponent<EggSpawnHelper>();
            DontDestroyOnLoad(_eggSpawnHelperObject);
            Debug.Log($"{LogTag}  EggSpawnHelper 已初始化");
        }

        private void InitializeSettings()
        {
            if (_settingsInitialized) return;

            if (!ModSettingAPI.Init(info))
            {
                Debug.LogError($"{LogTag} ModSettingAPI 初始化失败，可能未安装 ModSetting 或版本不兼容");
                return;
            }

            Settings.GameConfig.Init();
            Settings.SettingsUIRegistration.RegisterUI();
            
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage += OnLanguageChanged;
            EliteEnemyCore.UpdateConfig(Settings.GameConfig.GetConfig());
            
            _settingsInitialized = true;
            Debug.Log($"{LogTag}  设置系统已初始化");
        }
        
        private void InitializeLootVerifier()
        {
            if (_lootVerifierObject != null) return;

            _lootVerifierObject = new GameObject("EliteEnemies_LootVerifier");
            _lootVerifierObject.AddComponent<LootAlgorithmVerifier>();
            DontDestroyOnLoad(_lootVerifierObject);
        
            Debug.Log($"{LogTag} 掉落算法验证器已就绪 (按 F11 触发)");
        }

        private void CleanupLootVerifier()
        {
            if (_lootVerifierObject == null) return;
            Destroy(_lootVerifierObject);
            _lootVerifierObject = null;
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
            if (!_isPatched || _harmony == null) return;

            _harmony.UnpatchAll(_harmony.Id);
            _isPatched = false;
            Debug.Log($"{LogTag}  Harmony补丁已移除");
        }

        private void CleanupBuffFramework()
        {
            EliteEnemies.BuffsSystem.EliteBuffRegistry.Instance.Clear();
            EliteEnemies.BuffsSystem.EliteBuffModifierManager.Instance.Clear();
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

        private void CleanupSpawnHelper()
        {
            if (_spawnHelperObject == null) return;

            Destroy(_spawnHelperObject);
            _spawnHelperObject = null;
            Debug.Log($"{LogTag}  生成调试工具已清理");
        }

        private void CleanupEggSpawnHelper()
        {
            if (_eggSpawnHelperObject == null) return;

            Destroy(_eggSpawnHelperObject);
            _eggSpawnHelperObject = null;
            Debug.Log($"{LogTag}  EggSpawnHelper 已清理");
        }

        private void CleanupLocalization()
        {
            LocalizationManager.Cleanup();
            Debug.Log($"{LogTag}  本地化系统已清理");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EliteEnemyTracker.Reset();
            EliteLootSystem.ClearCache();
            Debug.Log($"{LogTag}  场景已加载: {scene.name}");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            EliteEnemyTracker.DumpSummary($"场景卸载: {scene.name}");
            EliteEnemyTracker.Reset();
            EliteLootSystem.ClearCache();
        }

        private void OnModActivated(ModInfo modInfo, Duckov.Modding.ModBehaviour behaviour)
        {
            if (modInfo.name != "ModSetting") return;
            InitializeSettings();
        }

        private void OnLanguageChanged(SystemLanguage lang)
        {
            LocalizationManager.Refresh();
        }
    }
}