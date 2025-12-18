using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SodaCraft.Localizations;

namespace EliteEnemies.EliteEnemy.Core
{
    /// <summary>
    /// 敌人生成辅助工具 (重构版：基于 Asset Name 标识)
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

        public bool IsReady => _isReady;

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

        // ========== 公共 API：生成敌人 ==========

        public CharacterMainControl SpawnClone(
            CharacterMainControl originalEnemy,
            Vector3 position,
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
            if (!ValidateSpawnConditions(originalEnemy)) return null;

            try
            {
                var preset = originalEnemy.characterPreset;
                if (preset == null) return null;

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

                SpawnEgg(position, modifiedPreset, originalEnemy);

                StartCoroutine(ApplyDelayedModifications(
                    position,
                    scaleMultiplier,
                    affixes,
                    preventElite,
                    onSpawned));

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成克隆敌人异常: {ex.Message}");
                return null;
            }
        }

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
            if (!_isReady || preset == null) return null;

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

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                CharacterMainControl effectiveSpawner = spawner ?? _player;
                SpawnEgg(position, modifiedPreset, effectiveSpawner);

                StartCoroutine(ApplyDelayedModifications(
                    position,
                    scaleMultiplier,
                    affixes,
                    preventElite,
                    onSpawned));

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 通过预设生成敌人异常: {ex.Message}");
                return null;
            }
        }

        public CharacterMainControl SpawnByPresetName(
            string resourceName,
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
            var preset = FindPreset(resourceName);
            if (preset == null)
            {
                Debug.LogError($"{LogTag} 未找到预设资源: {resourceName}");
                return null;
            }

            return SpawnByPreset(preset, position, spawner, healthMultiplier, damageMultiplier, speedMultiplier,
                scaleMultiplier, affixes, preventElite, customKeySuffix, customDisplayName, onSpawned);
        }


        /// <summary>
        /// 在指定中心点环绕生成多个敌人克隆体
        /// </summary>
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
            string customKeySuffix = null, // 修正后的参数名
            string customDisplayName = null,
            System.Action<List<CharacterMainControl>> onAllSpawned = null)
        {
            if (!ValidateSpawnConditions(originalEnemy) || count <= 0)
            {
                onAllSpawned?.Invoke(null);
                return;
            }

            StartCoroutine(SpawnCloneCircleCoroutine(
                originalEnemy, centerPosition, count, radius,
                healthMultiplier, damageMultiplier, speedMultiplier, scaleMultiplier,
                preventElite, customKeySuffix, customDisplayName, onAllSpawned));
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
            string customKeySuffix, // 修正后的参数名
            string customDisplayName,
            System.Action<List<CharacterMainControl>> onAllSpawned)
        {
            float angleStep = 360f / count;
            List<CharacterMainControl> spawnedEnemies = new List<CharacterMainControl>();
            int completedCount = 0;

            // 确定基础后缀，若未传则使用默认值
            string finalSuffix = !string.IsNullOrEmpty(customKeySuffix) ? customKeySuffix : "EE_Circle";

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 spawnPosition = centerPosition + offset;

                // 调用重构后的 SpawnClone，它会处理 name/nameKey 的后缀对齐和本地化注入
                SpawnClone(
                    originalEnemy: originalEnemy,
                    position: spawnPosition,
                    healthMultiplier: healthMultiplier,
                    damageMultiplier: damageMultiplier,
                    speedMultiplier: speedMultiplier,
                    scaleMultiplier: scaleMultiplier,
                    affixes: null,
                    preventElite: preventElite,
                    customKeySuffix: finalSuffix,
                    customDisplayName: customDisplayName,
                    onSpawned: (enemy) =>
                    {
                        if (enemy != null) spawnedEnemies.Add(enemy);
                        completedCount++;
                    });
            }

            // 等待所有成员完成延迟初始化（防止回调拿到的列表不完整）
            while (completedCount < count)
            {
                yield return null;
            }

            onAllSpawned?.Invoke(spawnedEnemies);
        }

        // ========== 核心逻辑重构 ==========

        private CharacterRandomPreset CreateModifiedPreset(
            CharacterRandomPreset original,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            string customKeySuffix = null,
            string customDisplayName = null)
        {
            CharacterRandomPreset modified = Instantiate(original);

            // 1. 确定标识后缀
            string suffix = !string.IsNullOrEmpty(customKeySuffix) ? customKeySuffix : "EE_Clone";

            // 2. [关键重构] 同步修改 name 和 nameKey，确保与新的判定系统兼容
            // 同时增加 EndsWith 检查防止递归生成导致名称无限延长
            if (!original.name.EndsWith($"_{suffix}"))
            {
                modified.name = $"{original.name}_{suffix}";
                modified.nameKey = $"{original.nameKey}_{suffix}";
            }
            else
            {
                modified.name = original.name;
                modified.nameKey = original.nameKey;
            }

            // 3. 应用属性倍率
            modified.health = original.health * healthMultiplier;
            modified.damageMultiplier = original.damageMultiplier * damageMultiplier;
            modified.moveSpeedFactor = original.moveSpeedFactor * speedMultiplier;

            // 4. [保留并优化] 处理本地化注入
            if (!string.IsNullOrEmpty(customDisplayName))
            {
                modified.showName = true;
                modified.showHealthBar = true;
                string targetKey = modified.nameKey;

                var overrideDict = LocalizationManager.overrideTexts;
                if (overrideDict != null)
                {
                    // 仅当值不同时才写入，避免字典冗余操作
                    if (!overrideDict.TryGetValue(targetKey, out string current) || current != customDisplayName)
                    {
                        overrideDict[targetKey] = customDisplayName;
                    }
                }
            }

            return modified;
        }

        private CharacterRandomPreset FindPreset(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;

            try
            {
                var allPresets = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();
                foreach (var preset in allPresets)
                {
                    // [修改] 切换为按 .name (Asset Name) 查找
                    if (preset.name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
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

        // ========== 内部辅助 ==========

        private bool ValidateSpawnConditions(CharacterMainControl originalEnemy = null)
        {
            if (!_isReady || _eggPrefab == null) return false;
            if (originalEnemy != null && originalEnemy.characterPreset == null) return false;
            return true;
        }

        private void SpawnEgg(Vector3 position, CharacterRandomPreset preset, CharacterMainControl spawner)
        {
            Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
            Collider eggCol = egg.GetComponent<Collider>();
            Collider spawnerCol = spawner.GetComponent<Collider>();
            if (eggCol != null && spawnerCol != null) Physics.IgnoreCollision(eggCol, spawnerCol, true);
            egg.Init(position, Vector3.zero, spawner, preset, DefaultEggSpawnDelay);
        }

        private IEnumerator ApplyDelayedModifications(
            Vector3 spawnPosition,
            float scaleMultiplier,
            List<string> affixes,
            bool preventElite,
            System.Action<CharacterMainControl> onSpawned = null)
        {
            yield return new WaitForSeconds(DefaultEggSpawnDelay + 0.3f);

            CharacterMainControl enemy = FindEnemyNearPosition(spawnPosition);
            if (enemy != null)
            {
                if (preventElite) EliteEnemyCore.MarkAsIgnored(enemy.gameObject);
                if (!Mathf.Approximately(scaleMultiplier, 1f))
                    enemy.transform.localScale = Vector3.one * scaleMultiplier;
                if (affixes != null && affixes.Count > 0 && !preventElite)
                    EliteEnemyCore.ForceMakeElite(enemy, affixes);
            }

            onSpawned?.Invoke(enemy);
        }

        private CharacterMainControl FindEnemyNearPosition(Vector3 position)
        {
            var all = FindObjectsOfType<CharacterMainControl>();
            CharacterMainControl best = null;
            float min = 2.5f;
            foreach (var c in all)
            {
                if (c == null || c.IsMainCharacter) continue;
                float d = Vector3.Distance(c.transform.position, position);
                if (d < min)
                {
                    min = d;
                    best = c;
                }
            }

            return best;
        }

        // 其余 API (SpawnCloneCircle 等) 调用 SpawnClone 即可自动适配新逻辑
    }
}