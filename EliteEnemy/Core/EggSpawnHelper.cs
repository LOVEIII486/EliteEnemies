using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection; // 引入反射命名空间
using UnityEngine;

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

                // [重构] 在创建预设时直接应用自定义名字
                var modifiedPreset = CreateModifiedPreset(
                    preset, 
                    healthMultiplier, 
                    damageMultiplier, 
                    speedMultiplier, 
                    customDisplayName // 传入自定义名字
                );

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                Egg egg = SpawnEgg(position, modifiedPreset, originalEnemy);
                
                // [重构] 延迟修改中不再包含名字修改逻辑
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
                // [重构] 在创建预设时直接应用自定义名字
                var modifiedPreset = CreateModifiedPreset(
                    preset, 
                    healthMultiplier, 
                    damageMultiplier, 
                    speedMultiplier,
                    customDisplayName // 传入自定义名字
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

            Collider eggCollider = egg.GetComponent<Collider>();
            Collider spawnerCollider = spawner.GetComponent<Collider>();
            if (eggCollider != null && spawnerCollider != null)
            {
                Physics.IgnoreCollision(eggCollider, spawnerCollider, true);
            }

            egg.Init(position, Vector3.zero, spawner, preset, DefaultEggSpawnDelay);

            return egg;
        }

        // [重构核心] 修改了此方法，增加了 overrideNameKey 参数，并使用反射修改
        private CharacterRandomPreset CreateModifiedPreset(
            CharacterRandomPreset original,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            string overrideNameKey = null) // 新增可选参数
        {
            // 深拷贝预设
            CharacterRandomPreset modified = Instantiate(original);

            // 1. 应用倍率（直接访问公有字段）
            modified.health = original.health * healthMultiplier;
            modified.damageMultiplier = original.damageMultiplier * damageMultiplier;
            modified.moveSpeedFactor = original.moveSpeedFactor * speedMultiplier;

            // 2. 应用自定义名字（反射修改，参考了 RandomNpc 的写法）
            if (!string.IsNullOrEmpty(overrideNameKey))
            {
                try
                {
                    // 设置 name (Unity 对象的内部名字，方便调试)
                    modified.name = "CustomPreset_" + overrideNameKey;

                    // 设置 nameKey 字段
                    FieldInfo nameKeyField = typeof(CharacterRandomPreset).GetField("displayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (nameKeyField != null)
                    {
                        nameKeyField.SetValue(modified, overrideNameKey);
                        //Debug.Log($"{LogTag} 已通过反射设置预设 NameKey: {overrideNameKey}");
                    }
                    else
                    {
                        // 如果反射失败，尝试直接赋值（如果它是public的话）
                        modified.nameKey = overrideNameKey; 
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LogTag} 设置自定义名字失败: {ex.Message}");
                }
            }

            return modified;
        }

        // 修改延迟应用方法，移除了 customDisplayName 参数
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

            // 仅保留 EliteIgnoredTag 标记，移除名字修改逻辑
            if (preventElite)
            {
                EliteEnemyCore.MarkAsIgnored(enemy.gameObject);
            }

            // 应用尺寸
            if (!Mathf.Approximately(scaleMultiplier, 1f))
            {
                enemy.transform.localScale = Vector3.one * scaleMultiplier;
            }

            // 应用词缀
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

                // 检查是否已经被处理过（这里主要通过 EliteIgnoredTag 或 EliteMarker 判断）
                // 因为现在名字是原生的，不能单纯靠名字判断了
                // 暂时使用简单的距离判断，或者检查是否已有 behavior 组件（如果是精英）
                
                // 优化：如果有 EliteIgnoredTag 组件，且没有 EliteMarker (尚未被系统识别)，说明是刚生成的忽略怪？
                // 实际上这里的防重入逻辑比较难做，因为现在生成过程更原生了。
                // 维持原来的逻辑：找最近的、且没有被其他逻辑抢占的（这里简化为找最近的）

                float distance = Vector3.Distance(character.transform.position, position);
                if (distance < 2.5f) 
                {
                    // 简单的防重入：如果回调里需要精确控制，可以加个临时 Tag
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