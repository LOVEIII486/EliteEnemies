using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.Core
{
    /// <summary>
    /// 敌人生成辅助工具
    /// 提供便捷的敌人生成方法，可复制现有敌人或创建新预设敌人
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
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static void InitializeEggPrefab()
        {
            if (_eggPrefab != null) return;

            Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
            if (eggs.Length > 0)
            {
                _eggPrefab = eggs[UnityEngine.Random.Range(0, eggs.Length)];
                //Debug.Log($"{LogTag} Egg 预制体已加载: {_eggPrefab.name}");
            }
            else
            {
                Debug.LogError($"{LogTag} 未找到 Egg 预制体");
            }
        }

        // ========== 公共 API：生成相同预设的敌人 ==========

        /// <summary>
        /// 生成与指定敌人相同预设的新敌人,可自定义属性倍率
        /// </summary>
        public CharacterMainControl SpawnClone(
            CharacterMainControl originalEnemy,
            Vector3 position,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customDisplayName = null,
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

                var modifiedPreset = CreateModifiedPreset(preset, healthMultiplier, damageMultiplier, speedMultiplier);

                // 如果需要禁止精英化，立即标记
                if (preventElite)
                {
                    modifiedPreset.nameKey = EliteEnemyCore.AddNonEliteSuffix(modifiedPreset.nameKey);
                }

                Egg egg = SpawnEgg(position, modifiedPreset, originalEnemy);
                // 延迟应用其他修改（尺寸、词缀、回调）
                if (affixes != null || !Mathf.Approximately(scaleMultiplier, 1f) || onSpawned != null)
                {
                    StartCoroutine(ApplyDelayedModifications(
                        position,
                        scaleMultiplier,
                        affixes,
                        preventElite,
                        originalEnemy,
                        customDisplayName,
                        onSpawned));
                }

                //Debug.Log($"{LogTag} 成功生成克隆敌人: {preset.nameKey}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成克隆敌人异常: {ex.Message}");
                return null;
            }
        }

        // ========== 公共 API：通过预设生成敌人 ==========

        /// <summary>
        /// 通过预设生成敌人
        /// </summary>
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
                var modifiedPreset = CreateModifiedPreset(preset, healthMultiplier, damageMultiplier, speedMultiplier);
                CharacterMainControl effectiveSpawner = spawner ?? _player;

                // 如果需要禁止精英化，立即标记
                if (preventElite)
                {
                    modifiedPreset.nameKey = EliteEnemyCore.AddNonEliteSuffix(modifiedPreset.nameKey);
                }

                // 使用 Egg 生成敌人
                Egg egg = SpawnEgg(position, modifiedPreset, effectiveSpawner);

                // 延迟应用修改
                if (affixes != null || !Mathf.Approximately(scaleMultiplier, 1f) || preventElite)
                {
                    StartCoroutine(ApplyDelayedModifications(
                        position,
                        scaleMultiplier,
                        affixes,
                        preventElite,
                        effectiveSpawner,
                        customDisplayName,
                        onSpawned));
                }

                //Debug.Log($"{LogTag} 成功通过预设生成敌人: {preset.nameKey}");
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
                scaleMultiplier, affixes, preventElite, customDisplayName, onSpawned);
        }

        // ========== 公共 API：批量生成 ==========

        /// <summary>
        /// 批量生成敌人（圆形分布）
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
                preventElite, customDisplayName, onAllSpawned));
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
            string customDisplayName,
            System.Action<List<CharacterMainControl>> onAllSpawned)
        {
            float angleStep = 360f / count;
            List<CharacterMainControl> spawnedEnemies = new List<CharacterMainControl>();
            int spawnedCount = 0;

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                Vector3 spawnPosition = centerPosition + offset;

                // 使用回调收集生成的角色
                SpawnClone(
                    originalEnemy,
                    spawnPosition,
                    healthMultiplier,
                    damageMultiplier,
                    speedMultiplier,
                    scaleMultiplier,
                    null,
                    preventElite,
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

            // 等待所有生成完成
            while (spawnedCount < count)
            {
                yield return null;
            }

            Debug.Log($"{LogTag} 批量生成完成: {spawnedEnemies.Count}/{count} 个敌人");

            // 调用回调返回所有生成的角色
            onAllSpawned?.Invoke(spawnedEnemies);
        }

        // ========== 公共 API：在玩家前方生成 ==========

        /// <summary>
        /// 在玩家前方生成敌人
        /// </summary>
        public CharacterMainControl SpawnCloneInFrontOfPlayer(
            CharacterMainControl originalEnemy,
            float distance = 5f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null)
        {
            if (!_isReady || _player == null)
            {
                Debug.LogWarning($"{LogTag} 无法在玩家前方生成：未就绪或玩家为空");
                return null;
            }

            Vector3 spawnPosition = _player.transform.position + _player.transform.forward * distance;
            return SpawnClone(originalEnemy, spawnPosition, healthMultiplier, damageMultiplier, speedMultiplier,
                scaleMultiplier, affixes);
        }

        /// <summary>
        /// 通过预设名在玩家前方生成敌人
        /// </summary>
        public CharacterMainControl SpawnByPresetNameInFrontOfPlayer(
            string presetName,
            float distance = 5f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true)
        {
            if (!_isReady || _player == null)
            {
                Debug.LogWarning($"{LogTag} 无法在玩家前方生成：未就绪或玩家为空");
                return null;
            }

            Vector3 spawnPosition = _player.transform.position + _player.transform.forward * distance;
            return SpawnByPresetName(presetName, spawnPosition, null, healthMultiplier, damageMultiplier,
                speedMultiplier, scaleMultiplier, affixes, preventElite);
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

            // 忽略与生成者的碰撞
            Collider eggCollider = egg.GetComponent<Collider>();
            Collider spawnerCollider = spawner.GetComponent<Collider>();
            if (eggCollider != null && spawnerCollider != null)
            {
                Physics.IgnoreCollision(eggCollider, spawnerCollider, true);
            }

            // 初始化 Egg（spawner 决定队伍关系）
            egg.Init(position, Vector3.zero, spawner, preset, DefaultEggSpawnDelay);

            return egg;
        }

        private CharacterRandomPreset CreateModifiedPreset(
            CharacterRandomPreset original,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier)
        {
            // 深拷贝预设
            CharacterRandomPreset modified = Instantiate(original);

            // 应用倍率
            modified.health = original.health * healthMultiplier;
            modified.damageMultiplier = original.damageMultiplier * damageMultiplier;
            modified.moveSpeedFactor = original.moveSpeedFactor * speedMultiplier;

            return modified;
        }

        // 修改延迟应用方法
        private IEnumerator ApplyDelayedModifications(
            Vector3 spawnPosition,
            float scaleMultiplier,
            List<string> affixes,
            bool preventElite,
            CharacterMainControl summoner,
            string customDisplayName,
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

            // 如果禁止精英化，创建 Marker 并保存原始名称
            if (preventElite || !string.IsNullOrEmpty(customDisplayName))
            {
                var marker = enemy.GetComponent<EliteEnemyCore.EliteMarker>();
                if (marker == null)
                {
                    marker = enemy.gameObject.AddComponent<EliteEnemyCore.EliteMarker>();
                }

                if (!string.IsNullOrEmpty(customDisplayName))
                {
                    marker.CustomDisplayName = customDisplayName;
                }
            }

            // 应用尺寸
            if (!Mathf.Approximately(scaleMultiplier, 1f))
            {
                enemy.transform.localScale = Vector3.one * scaleMultiplier;
            }

            // 应用词缀(仅当未禁止时)
            if (affixes != null && affixes.Count > 0 && !preventElite)
            {
                EliteEnemyCore.ForceMakeElite(enemy, affixes);
            }

            // 调用回调
            onSpawned?.Invoke(enemy);
        }

        private CharacterMainControl FindEnemyNearPosition(Vector3 position)
        {
            var allCharacters = FindObjectsOfType<CharacterMainControl>();
            foreach (var character in allCharacters)
            {
                if (character == null || character == _player) continue;

                // 逻辑修正：检查是否已经被处理过
                // 如果这个敌人身上已经有了 EliteMarker，并且已经有了自定义名字，说明它被其他协程“认领”了
                var existingMarker = character.GetComponent<EliteEnemyCore.EliteMarker>();
                if (existingMarker != null && !string.IsNullOrEmpty(existingMarker.CustomDisplayName))
                {
                    continue; // 跳过已处理的，找下一个
                }

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

        // ========== 状态检查 ==========

        public bool IsReady => _isReady;

        public CharacterMainControl GetPlayer() => _player;
    }
}