using System.Collections;
using System.Collections.Generic;
using Duckov.Utilities;
using UnityEngine;

namespace EliteEnemies.DebugTool
{
    /// <summary>
    /// 敌人生成助手
    /// 调试工具：按键在鼠标位置生成敌人
    /// </summary>
    public class SpawnHelper : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.SpawnHelper]";
        
        public static SpawnHelper Instance { get; private set; }
        
        public bool enableSpawning = false;
        public KeyCode spawnKey = KeyCode.F5;
        public float eggSpawnDelay = 0.001f;
        public string defaultPresetName = "Cname_Scav";
        public bool spawnAsElite = false;
        public List<string> eliteAffixes = new List<string>();
        public EnemyPresetConfig enemyConfig = new EnemyPresetConfig();
        
        private static Egg _eggPrefab;
        private Dictionary<string, CharacterRandomPreset> _presetCache = new Dictionary<string, CharacterRandomPreset>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InitializeEggPrefab();
            LoadAvailablePresets();
        }

        private void Update()
        {
            if (!enableSpawning) return;

            if (Input.GetKeyDown(spawnKey))
            {
                SpawnEnemyAtMousePosition();
            }
        }

        // ========== 初始化 ==========

        private void InitializeEggPrefab()
        {
            if (_eggPrefab != null) return;

            Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
            if (eggs.Length > 0)
            {
                _eggPrefab = eggs[Random.Range(0, eggs.Length)];
            }
            else
            {
                Debug.LogError($"{LogTag} 未找到 Egg 预制体");
            }
        }

        private void LoadAvailablePresets()
        {
            try
            {
                List<CharacterRandomPreset> presets = GameplayDataSettings.CharacterRandomPresetData.presets;
                _presetCache.Clear();
                
                foreach (var preset in presets)
                {
                    if (!string.IsNullOrEmpty(preset.nameKey))
                    {
                        _presetCache[preset.nameKey] = preset;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 加载角色预设失败: {ex.Message}");
            }
        }

        // ========== 生成逻辑 ==========

        /// <summary>
        /// 在鼠标位置生成敌人
        /// </summary>
        public void SpawnEnemyAtMousePosition(string presetName = null)
        {
            if (!ValidateSpawnConditions()) return;

            Vector3 spawnPosition = GetMouseGroundPosition();
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogWarning($"{LogTag} 无法获取有效的鼠标位置");
                return;
            }

            string targetPreset = string.IsNullOrEmpty(presetName) ? defaultPresetName : presetName;
            CharacterRandomPreset preset = GetPreset(targetPreset);
            
            if (preset == null)
            {
                Debug.LogError($"{LogTag} 角色预设未找到: {targetPreset}");
                return;
            }

            // 应用自定义配置
            preset = ApplyCustomConfig(preset);

            // 生成 Egg
            Egg egg = SpawnEgg(spawnPosition, preset);
            
            Debug.Log($"{LogTag} 生成敌人: {spawnPosition}, 预设: {preset.nameKey}, 团队: {enemyConfig.team}");

            // 应用精英词缀
            if (spawnAsElite && eliteAffixes.Count > 0)
            {
                StartCoroutine(ApplyEliteAffixesDelayed(egg));
            }
        }

        private bool ValidateSpawnConditions()
        {
            if (_eggPrefab == null)
            {
                Debug.LogError($"{LogTag} Egg 预制体未初始化");
                return false;
            }

            if (LevelManager.Instance == null || LevelManager.Instance.MainCharacter == null)
            {
                Debug.LogError($"{LogTag} 主角色未找到");
                return false;
            }

            return true;
        }

        private Egg SpawnEgg(Vector3 position, CharacterRandomPreset preset)
        {
            CharacterMainControl mainCharacter = LevelManager.Instance.MainCharacter;
            Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
            
            // 忽略与主角的碰撞
            Collider eggCollider = egg.GetComponent<Collider>();
            Collider characterCollider = mainCharacter.GetComponent<Collider>();
            if (eggCollider != null && characterCollider != null)
            {
                Physics.IgnoreCollision(eggCollider, characterCollider, true);
            }

            egg.Init(position, Vector3.zero, mainCharacter, preset, eggSpawnDelay);
            return egg;
        }

        private IEnumerator ApplyEliteAffixesDelayed(Egg egg)
        {
            yield return new WaitForSeconds(eggSpawnDelay + 0.5f);
        }

        private Vector3 GetMouseGroundPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 尝试射线检测地面
            int groundLayerMask = LayerMask.GetMask("Default", "Ground", "Terrain");
            if (groundLayerMask == 0) groundLayerMask = ~0;

            if (Physics.Raycast(ray, out hit, 1000f, groundLayerMask))
            {
                return hit.point;
            }

            // 备用方案：使用地面平面
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            float distance;
            if (groundPlane.Raycast(ray, out distance))
            {
                Vector3 point = ray.GetPoint(distance);
                point.y = 0.1f;
                return point;
            }

            return Vector3.zero;
        }

        private CharacterRandomPreset GetPreset(string presetName)
        {
            _presetCache.TryGetValue(presetName, out CharacterRandomPreset preset);
            return preset;
        }

        private CharacterRandomPreset ApplyCustomConfig(CharacterRandomPreset preset)
        {
            CharacterRandomPreset modifiedPreset = Instantiate(preset);

            // 基础属性
            modifiedPreset.health = enemyConfig.health;
            modifiedPreset.moveSpeedFactor = enemyConfig.moveSpeedFactor;

            // 战斗属性
            modifiedPreset.damageMultiplier = enemyConfig.damageMultiplier;
            modifiedPreset.gunScatterMultiplier = enemyConfig.gunScatterMultiplier;
            modifiedPreset.reactionTime = enemyConfig.reactionTime;
            modifiedPreset.shootDelay = enemyConfig.shootDelay;
            modifiedPreset.shootTimeRange = new Vector2(enemyConfig.shootDelay, enemyConfig.shootDelay);
            modifiedPreset.shootTimeSpaceRange = new Vector2(enemyConfig.shootDelay, enemyConfig.shootDelay);

            // 感知属性
            modifiedPreset.sightAngle = enemyConfig.sightAngle;
            modifiedPreset.sightDistance *= enemyConfig.sightDistanceMultiplier;
            modifiedPreset.hearingAbility = enemyConfig.hearingAbility;
            modifiedPreset.nightVisionAbility = enemyConfig.nightVisionAbility;
            modifiedPreset.nightReactionTimeFactor = enemyConfig.nightReactionTimeFactor;

            // 行为设置
            modifiedPreset.canDash = enemyConfig.canDash;
            modifiedPreset.shootCanMove = enemyConfig.shootCanMove;
            modifiedPreset.showHealthBar = enemyConfig.showHealthBar;
            modifiedPreset.showName = enemyConfig.showName;

            // 团队设置
            modifiedPreset.team = enemyConfig.team;

            return modifiedPreset;
        }

        // ========== 公共接口 ==========

        public void SetSpawningEnabled(bool enabled)
        {
            enableSpawning = enabled;
        }

        public void SetDefaultPreset(string presetName)
        {
            defaultPresetName = presetName;
        }

        public void SetEliteMode(bool isElite, List<string> affixes = null)
        {
            spawnAsElite = isElite;
            if (affixes != null)
            {
                eliteAffixes = new List<string>(affixes);
            }
        }

        public void SetTeam(Teams team)
        {
            enemyConfig.team = team;
        }

        // ========== 配置类 ==========

        /// <summary>
        /// 敌人预设配置
        /// </summary>
        [System.Serializable]
        public class EnemyPresetConfig
        {
            [Header("基础属性")]
            public float health = 100f;
            public float moveSpeedFactor = 1.5f;
            
            [Header("战斗属性")]
            public float damageMultiplier = 1.0f;
            public float gunScatterMultiplier = 0.5f;
            public float reactionTime = 0.5f;
            public float shootDelay = 0.2f;
            
            [Header("感知属性")]
            public float sightAngle = 180f;
            public float sightDistanceMultiplier = 1.0f;
            public float hearingAbility = 0.3f;
            public float nightVisionAbility = 0.5f;
            public float nightReactionTimeFactor = 1.0f;
            
            [Header("行为设置")]
            public bool canDash = true;
            public bool shootCanMove = true;
            public bool showHealthBar = true;
            public bool showName = true;

            [Header("团队设置")]
            public Teams team = Teams.all;
        }
    }
}