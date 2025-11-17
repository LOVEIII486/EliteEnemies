using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【分裂】词缀 - 敌人死亡时分裂成多个较弱的小怪
    /// </summary>
    public class SplitBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Split";
        
        private static readonly int MinSplitCount = 2; // 最少分裂数量
        private static readonly int MaxSplitCount = 4; // 最多分裂数量
        private static readonly float SplitRadius = 2.0f; // 分裂半径
        private static readonly float SplitHealthRatio = 0.5f; // 分身血量比例
        private static readonly float SplitDamageRatio = 0.5f; // 分身伤害比例
        private static readonly float SplitSpeedRatio = 1.1f; // 分身速度比例
        private static readonly float EggSpawnDelay = 0.001f; // Egg 孵化延迟

        private static Egg eggPrefab = null;
        private CharacterMainControl _originalCharacter;
        private CharacterRandomPreset _originalPreset;
        private bool _hasSplit = false; // 防止重复分裂

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _originalCharacter = character;
            _hasSplit = false;

            // 缓存原始预设信息
            if (character != null && character.characterPreset != null)
            {
                _originalPreset = character.characterPreset;
                
                if (character.Health != null)
                {
                    Health.OnHurt += OnCharacterHurt;
                }
            }
            else
            {
                Debug.LogWarning($"[SplitBehavior] {character?.name} 无法获取角色预设，分裂可能失败");
            }
            
            InitializeEggPrefab();
        }

        /// <summary>
        /// 监听受伤事件，在死亡前触发分裂
        /// </summary>
        private void OnCharacterHurt(Health health, DamageInfo damageInfo)
        {
            if (health != _originalCharacter?.Health) return;
            if (_hasSplit) return; // 已经分裂过了

            if (health.CurrentHealth < health.MaxHealth / 3)
            {
                _hasSplit = true;
                TriggerSplit(_originalCharacter);
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_originalCharacter != null)
            {
                Health.OnHurt -= OnCharacterHurt;
            }
        }

        /// <summary>
        /// 触发分裂效果
        /// </summary>
        private void TriggerSplit(CharacterMainControl character)
        {
            if (eggPrefab == null)
            {
                Debug.LogError("[SplitBehavior] Egg 预制体未初始化，无法分裂");
                return;
            }

            if (_originalPreset == null)
            {
                Debug.LogWarning("[SplitBehavior] 原始预设丢失，无法分裂");
                return;
            }

            Vector3 deathPosition = character.transform.position;
            int splitCount = Random.Range(MinSplitCount, MaxSplitCount + 1);

            //Debug.Log($"[SplitBehavior] {character.name} 触发分裂！将生成 {splitCount} 个分身");
            
            for (int i = 0; i < splitCount; i++)
            {
                SpawnSplitClone(deathPosition, i, splitCount);
            }
        }

        /// <summary>
        /// 生成单个分身
        /// </summary>
        private void SpawnSplitClone(Vector3 centerPosition, int index, int totalCount)
        {
            // 计算分身生成位置
            float angle = (360f / totalCount) * index;
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * SplitRadius;
            Vector3 spawnPosition = centerPosition + offset;
            spawnPosition.y = centerPosition.y; // 同一高度

            // 创建弱化版本的预设
            CharacterRandomPreset weakenedPreset = CreateWeakenedPreset(_originalPreset);

            // 使用死亡敌人自己作为生成者，而不是玩家，这样可以确保分身继承正确的敌对关系
            CharacterMainControl spawner = _originalCharacter;
            if (spawner == null)
            {
                Debug.LogError("[SplitBehavior] 原始角色引用丢失，无法生成分身");
                return;
            }
            
            Egg egg = Object.Instantiate(eggPrefab, spawnPosition, Quaternion.identity);

            // 忽略与原敌人的碰撞，避免生成时卡住
            Collider eggCollider = egg.GetComponent<Collider>();
            Collider spawnerCollider = spawner.GetComponent<Collider>();
            if (eggCollider != null && spawnerCollider != null)
            {
                Physics.IgnoreCollision(eggCollider, spawnerCollider, true);
            }

            // 使用死亡敌人作为 spawner，这样分身会继承正确的团队关系
            Vector3 velocity = Vector3.zero;
            egg.Init(spawnPosition, velocity, spawner, weakenedPreset, EggSpawnDelay);

            // 注册分身死亡事件，清空掉落
            ModBehaviour.Instance?.StartCoroutine(RegisterCloneDeathHandler(spawnPosition));

            //Debug.Log($"[SplitBehavior] 分身 {index + 1}/{totalCount} 已生成于: {spawnPosition}，生成者: {spawner.name}");
        }

        /// <summary>
        /// 注册分身死亡后清空战利品箱的处理器
        /// </summary>
        private IEnumerator RegisterCloneDeathHandler(Vector3 spawnPosition)
        {
            // 等待分身生成
            yield return new WaitForSeconds(EggSpawnDelay + 0.2f);

            // 查找生成的分身
            CharacterMainControl clone = FindCloneNearPosition(spawnPosition);
            if (clone != null)
            {
                // 注册死亡事件：清空战利品箱
                clone.BeforeCharacterSpawnLootOnDead += (damageInfo) =>
                {
                    ModBehaviour.Instance?.StartCoroutine(ClearCloneLootBox(clone.transform.position));
                };
                //Debug.Log($"[SplitBehavior] 已为分身 {clone.name} 注册掉落清空处理");
            }
            else
            {
                Debug.LogWarning("[SplitBehavior] 未找到生成的分身，无法注册掉落清空");
            }
        }

        /// <summary>
        /// 清空分身死亡位置的战利品箱
        /// </summary>
        /// <summary>
        /// 分身死亡位置的战利品箱：随机仅保留 1 件掉落（统一规则，便于测试）
        /// 如需改回“仅 Boss 保留一件、普通清空”，见下方已注释的判断块。
        /// </summary>
        private IEnumerator ClearCloneLootBox(Vector3 deathPosition)
        {
            // 等待战利品箱生成
            yield return new WaitForSeconds(0.1f);

            // 定位最近 LootBox
            InteractableLootbox lootbox = FindNearbyLootBox(deathPosition);
            if (lootbox == null || lootbox.Inventory == null) yield break;

            var inv = lootbox.Inventory;
            var items = new List<Item>();
            foreach (var it in inv)
                if (it != null)
                    items.Add(it);

            if (items.Count <= 1) yield break; // 0 或 1 件无需处理

            int keepIndex = UnityEngine.Random.Range(0, items.Count);
            Item keepItem = items[keepIndex];

            for (int i = 0; i < items.Count; i++)
            {
                if (i == keepIndex) continue;
                var it = items[i];
                if (it != null) it.DestroyTree();
            }
            // Debug.Log($"[SplitBehavior] 测试模式：仅保留掉落 {keepItem?.name ?? "(null)"} 于 {deathPosition}");
        }


        /// <summary>
        /// 查找指定位置附近的战利品箱（2m 范围内）
        /// </summary>
        private InteractableLootbox FindNearbyLootBox(Vector3 position)
        {
            var allLootBoxes = Object.FindObjectsOfType<InteractableLootbox>();
            foreach (var lootbox in allLootBoxes)
            {
                if (Vector3.Distance(lootbox.transform.position, position) < 2f)
                    return lootbox;
            }

            return null;
        }

        /// <summary>
        /// 查找指定位置附近的角色（2m 范围内）
        /// </summary>
        private CharacterMainControl FindCloneNearPosition(Vector3 position)
        {
            var allCharacters = Object.FindObjectsOfType<CharacterMainControl>();
            foreach (var character in allCharacters)
            {
                if (character == null || character == _originalCharacter) continue;

                float distance = Vector3.Distance(character.transform.position, position);
                if (distance < 2f) // 2米范围内
                {
                    return character;
                }
            }

            return null;
        }

        /// <summary>
        /// 创建弱化版本的预设（降低属性，移除精英词缀）
        /// </summary>
        private CharacterRandomPreset CreateWeakenedPreset(CharacterRandomPreset original)
        {
            if (original == null) return null;

            // 深拷贝预设
            CharacterRandomPreset weakened = Object.Instantiate(original);

            // 降低属性
            weakened.health = original.health * SplitHealthRatio;
            weakened.damageMultiplier = original.damageMultiplier * SplitDamageRatio;
            weakened.moveSpeedFactor = original.moveSpeedFactor * SplitSpeedRatio;
            weakened.showName = true;
            weakened.showHealthBar = true;
            weakened.team = original.team;
            weakened.reactionTime *= 0.8f;
            weakened.shootDelay *= 0.9f;
            weakened.canDash = true;

            return weakened;
        }

        /// <summary>
        /// 初始化 Egg 预制体
        /// </summary>
        private static void InitializeEggPrefab()
        {
            if (eggPrefab != null) return;
            
            Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
            if (eggs.Length > 0)
            {
                eggPrefab = eggs[Random.Range(0, eggs.Length)];
                //Debug.Log($"[SplitBehavior] Egg 预制体已加载: {eggPrefab.name}");
            }
            else
            {
                Debug.LogError("[SplitBehavior] 未找到 Egg 预制体");
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_originalCharacter != null)
            {
                Health.OnHurt -= OnCharacterHurt;
            }
            _originalCharacter = null;
            _originalPreset = null;
            _hasSplit = false;
        }
    }
}