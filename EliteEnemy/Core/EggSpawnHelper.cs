using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SodaCraft.Localizations; 

namespace EliteEnemies.EliteEnemy.Core
{
    /// <summary>
    /// 敌人生成辅助工具
    /// </summary>
    public class EggSpawnHelper : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.EggSpawnHelper]";
        private const float DefaultEggSpawnDelay = 0.001f;

        private static EggSpawnHelper _instance;
        public static EggSpawnHelper Instance => _instance;

        private bool _isReady = false;
        private CharacterMainControl _player;
        private static Egg _eggPrefab;

        // ========== 初始化 ==========

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            StartCoroutine(WaitForInitialization());
        }

        private IEnumerator WaitForInitialization()
        {
            InitializeEggPrefab();
            while (CharacterMainControl.Main == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            _player = CharacterMainControl.Main;
            _isReady = true;
            Debug.Log($"{LogTag} 初始化完成");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private static void InitializeEggPrefab()
        {
            if (_eggPrefab != null) return;
            Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
            if (eggs.Length > 0)
            {
                _eggPrefab = eggs[UnityEngine.Random.Range(0, eggs.Length)];
            }
            else
            {
                Debug.LogError($"{LogTag} 未找到 Egg 预制体");
            }
        }

        // ========== 公共 API：生成相同预设的敌人 ==========

        public CharacterMainControl SpawnClone(
            CharacterMainControl originalEnemy,
            Vector3 position,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,   // 用于构建唯一Key的后缀
            string customDisplayName = null, // 用于显示的中文名
            System.Action<CharacterMainControl> onSpawned = null)
        {
            if (!ValidateSpawnConditions(originalEnemy)) return null;

            try
            {
                var preset = originalEnemy.characterPreset;
                if (preset == null)
                {
                    Debug.LogError($"{LogTag} 原始敌人没有预设");
                    return null;
                }

                var modifiedPreset = CreateModifiedPreset(
                    preset, 
                    healthMultiplier, 
                    damageMultiplier, 
                    speedMultiplier, 
                    customKeySuffix,
                    customDisplayName
                );

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                Egg egg = SpawnEgg(position, modifiedPreset, originalEnemy);
                
                if (affixes != null || !Mathf.Approximately(scaleMultiplier, 1f) || onSpawned != null || preventElite)
                {
                    StartCoroutine(ApplyDelayedModifications(
                        position,
                        scaleMultiplier,
                        affixes,
                        preventElite,
                        originalEnemy,
                        onSpawned));
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成克隆敌人异常: {ex.Message}");
                return null;
            }
        }

        // ========== 公共 API：通过预设生成敌人 ==========

        public CharacterMainControl SpawnByPreset(
            CharacterRandomPreset preset,
            Vector3 position,
            CharacterMainControl spawner = null,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,   
            string customDisplayName = null, 
            System.Action<CharacterMainControl> onSpawned = null)
        {
            if (!_isReady || preset == null)
            {
                Debug.LogWarning($"{LogTag} 无法生成敌人：未就绪或预设为空");
                return null;
            }

            if (_eggPrefab == null)
            {
                Debug.LogError($"{LogTag} Egg 预制体未初始化");
                return null;
            }

            try
            {
                var modifiedPreset = CreateModifiedPreset(
                    preset, 
                    healthMultiplier, 
                    damageMultiplier, 
                    speedMultiplier,
                    customKeySuffix,
                    customDisplayName
                );
                
                CharacterMainControl effectiveSpawner = spawner ?? _player;

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                Egg egg = SpawnEgg(position, modifiedPreset, effectiveSpawner);

                if (affixes != null || !Mathf.Approximately(scaleMultiplier, 1f) || preventElite)
                {
                    StartCoroutine(ApplyDelayedModifications(
                        position,
                        scaleMultiplier,
                        affixes,
                        preventElite,
                        effectiveSpawner,
                        onSpawned));
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 通过预设生成敌人异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 通过预设名称生成敌人
        /// </summary>
        public CharacterMainControl SpawnByPresetName(
            string presetName,
            Vector3 position,
            CharacterMainControl spawner = null,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,   
            string customDisplayName = null, 
            System.Action<CharacterMainControl> onSpawned = null)
        {
            var preset = FindPreset(presetName);
            if (preset == null)
            {
                Debug.LogError($"{LogTag} 未找到预设: {presetName}");
                return null;
            }

            return SpawnByPreset(preset, position, spawner, healthMultiplier, damageMultiplier, speedMultiplier,
                scaleMultiplier, affixes, preventElite, customKeySuffix, customDisplayName, onSpawned);
        }

        // ========== 公共 API：批量生成 ==========

        public void SpawnCloneCircle(
            CharacterMainControl originalEnemy,
            Vector3 centerPosition,
            int count,
            float radius = 3f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            bool preventElite = true,
            string customKeySuffixPrefix = null, 
            string customDisplayName = null,
            System.Action<List<CharacterMainControl>> onAllSpawned = null)
        {
            if (!ValidateSpawnConditions(originalEnemy) || count <= 0)
            {
                Debug.LogWarning($"{LogTag} 无法批量生成：参数无效");
                onAllSpawned?.Invoke(null);
                return;
            }

            StartCoroutine(SpawnCloneCircleCoroutine(
                originalEnemy, centerPosition, count, radius,
                healthMultiplier, damageMultiplier, speedMultiplier, scaleMultiplier,
                preventElite, customKeySuffixPrefix, customDisplayName, onAllSpawned));
        }

        private IEnumerator SpawnCloneCircleCoroutine(
            CharacterMainControl originalEnemy,
            Vector3 centerPosition,
            int count,
            float radius,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            float scaleMultiplier,
            bool preventElite,
            string customKeySuffixPrefix,
            string customDisplayName,
            System.Action<List<CharacterMainControl>> onAllSpawned)
        {
            float angleStep = 360f / count;
            List<CharacterMainControl> spawnedEnemies = new List<CharacterMainControl>();
            int spawnedCount = 0;
            
            // [修改] 如果没有指定后缀，默认使用 "EE_NonSuffix"
            string baseSuffix = !string.IsNullOrEmpty(customKeySuffixPrefix) ? customKeySuffixPrefix : "EE_NonSuffix";

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                Vector3 spawnPosition = centerPosition + offset;

                // [修改] 直接传入 baseSuffix，不再添加 _{i} 数字序号
                // 这样这批敌人共享同一个 Key，避免冗余
                string instanceSuffix = baseSuffix;

                SpawnClone(
                    originalEnemy,
                    spawnPosition,
                    healthMultiplier,
                    damageMultiplier,
                    speedMultiplier,
                    scaleMultiplier,
                    null,
                    preventElite,
                    instanceSuffix,    
                    customDisplayName, 
                    (spawnedEnemy) =>
                    {
                        if (spawnedEnemy != null)
                        {
                            spawnedEnemies.Add(spawnedEnemy);
                        }

                        spawnedCount++;
                    });
            }

            while (spawnedCount < count)
            {
                yield return null;
            }

            Debug.Log($"{LogTag} 批量生成完成: {spawnedEnemies.Count}/{count} 个敌人");
            onAllSpawned?.Invoke(spawnedEnemies);
        }

        // ========== 公共 API：在玩家前方生成 ==========

        public CharacterMainControl SpawnCloneInFrontOfPlayer(
            CharacterMainControl originalEnemy,
            float distance = 5f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            string customKeySuffix = null,   
            string customDisplayName = null) 
        {
            if (!_isReady || _player == null)
            {
                Debug.LogWarning($"{LogTag} 无法在玩家前方生成：未就绪或玩家为空");
                return null;
            }

            Vector3 spawnPosition = _player.transform.position + _player.transform.forward * distance;
            return SpawnClone(originalEnemy, spawnPosition, healthMultiplier, damageMultiplier, speedMultiplier,
                scaleMultiplier, affixes, true, customKeySuffix, customDisplayName);
        }

        public CharacterMainControl SpawnByPresetNameInFrontOfPlayer(
            string presetName,
            float distance = 5f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,   
            string customDisplayName = null) 
        {
            if (!_isReady || _player == null)
            {
                Debug.LogWarning($"{LogTag} 无法在玩家前方生成：未就绪或玩家为空");
                return null;
            }

            Vector3 spawnPosition = _player.transform.position + _player.transform.forward * distance;
            return SpawnByPresetName(presetName, spawnPosition, null, healthMultiplier, damageMultiplier,
                speedMultiplier, scaleMultiplier, affixes, preventElite, customKeySuffix, customDisplayName);
        }

        // ========== 内部方法 ==========

        private bool ValidateSpawnConditions(CharacterMainControl originalEnemy = null)
        {
            if (!_isReady)
            {
                Debug.LogWarning($"{LogTag} 尚未就绪");
                return false;
            }

            if (_eggPrefab == null)
            {
                Debug.LogError($"{LogTag} Egg 预制体未初始化");
                return false;
            }

            if (originalEnemy != null && originalEnemy.characterPreset == null)
            {
                Debug.LogError($"{LogTag} 原始敌人没有预设");
                return false;
            }

            return true;
        }

        private Egg SpawnEgg(Vector3 position, CharacterRandomPreset preset, CharacterMainControl spawner)
        {
            Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);

            Collider eggCollider = egg.GetComponent<Collider>();
            Collider spawnerCollider = spawner.GetComponent<Collider>();
            if (eggCollider != null && spawnerCollider != null)
            {
                Physics.IgnoreCollision(eggCollider, spawnerCollider, true);
            }

            egg.Init(position, Vector3.zero, spawner, preset, DefaultEggSpawnDelay);

            return egg;
        }

        // [核心重构] 
        private CharacterRandomPreset CreateModifiedPreset(
            CharacterRandomPreset original,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            string customKeySuffix = null,    
            string customDisplayName = null)  
        {
            CharacterRandomPreset modified = Instantiate(original);

            // [修改] 你建议不修改 Unity 内部名，保持注释状态
            // modified.name = $"Preset_{uniqueKey}";

            // 1. 确定 Key 后缀
            // 默认使用统一的 "EE_NonSuffix" 以减少 Key 的多样性
            string suffix = !string.IsNullOrEmpty(customKeySuffix) ? customKeySuffix : "EE_NonSuffix";

            // 2. 设置 nameKey (关键逻辑：防递归 & 兜底显示)
            // 检查原始 Key 是否已经包含了这个后缀，如果是，就不再追加，防止 "Key_Suffix_Suffix"
            string finalKey;
            if (original.nameKey.EndsWith($"_{suffix}"))
            {
                // 已经有了，直接复用
                finalKey = original.nameKey;
            }
            else
            {
                // 没有，追加后缀
                finalKey = $"{original.nameKey}_{suffix}";
            }
            
            modified.nameKey = finalKey;

            // 3. 应用属性倍率
            modified.health = original.health * healthMultiplier;
            modified.damageMultiplier = original.damageMultiplier * damageMultiplier;
            modified.moveSpeedFactor = original.moveSpeedFactor * speedMultiplier;

            // 4. 处理显示名称 (Localiztion Override)
            // 如果没有提供自定义名字，默认使用 "???"
            string finalDisplayName = !string.IsNullOrEmpty(customDisplayName) ? customDisplayName : "???";

            try
            {
                // 只要有后缀改动，我们就需要确保这个 Key 能显示出东西
                // 强制开启名字显示
                modified.showName = true;
                modified.showHealthBar = true;

                // 注册到游戏原生字典
                if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
                {
                    // 注册映射关系：finalKey -> finalDisplayName
                    // 注意：如果 finalKey 已经存在且 value 相同，赋值也是安全的
                    SodaCraft.Localizations.LocalizationManager.overrideTexts[finalKey] = finalDisplayName;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 注册本地化文本失败: {ex.Message}");
            }

            return modified;
        }

        private IEnumerator ApplyDelayedModifications(
            Vector3 spawnPosition,
            float scaleMultiplier,
            List<string> affixes,
            bool preventElite,
            CharacterMainControl summoner,
            System.Action<CharacterMainControl> onSpawned = null)
        {
            yield return new WaitForSeconds(DefaultEggSpawnDelay + 0.3f);

            CharacterMainControl enemy = FindEnemyNearPosition(spawnPosition);
            if (enemy == null)
            {
                Debug.LogWarning($"{LogTag} 未找到生成的敌人");
                onSpawned?.Invoke(null);
                yield break;
            }

            if (preventElite)
            {
                EliteEnemyCore.MarkAsIgnored(enemy.gameObject);
            }

            if (!Mathf.Approximately(scaleMultiplier, 1f))
            {
                enemy.transform.localScale = Vector3.one * scaleMultiplier;
            }

            if (affixes != null && affixes.Count > 0 && !preventElite)
            {
                EliteEnemyCore.ForceMakeElite(enemy, affixes);
            }

            onSpawned?.Invoke(enemy);
        }

        private CharacterMainControl FindEnemyNearPosition(Vector3 position)
        {
            var allCharacters = FindObjectsOfType<CharacterMainControl>();
            foreach (var character in allCharacters)
            {
                if (character == null || character == _player) continue;

                float distance = Vector3.Distance(character.transform.position, position);
                if (distance < 2.5f) 
                {
                    return character;
                }
            }
            return null;
        }

        private CharacterRandomPreset FindPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return null;

            try
            {
                var allPresets = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();
                foreach (var preset in allPresets)
                {
                    if (preset.nameKey.Equals(presetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return preset;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 查找预设异常: {ex.Message}");
            }
            return null;
        }

        public bool IsReady => _isReady;
        public CharacterMainControl GetPlayer() => _player;
    }
}