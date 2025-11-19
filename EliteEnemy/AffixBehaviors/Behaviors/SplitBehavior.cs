using System.Collections;
using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【分裂】词缀 - 敌人残血时分裂成多个较弱的小怪
    /// </summary>
    public class SplitBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Split";
        
        private static readonly int MinSplitCount = 2;
        private static readonly int MaxSplitCount = 4;
        private static readonly float SplitRadius = 2.0f;
        private static readonly float SplitHealthRatio = 0.5f;
        private static readonly float SplitDamageRatio = 0.7f;
        private static readonly float SplitSpeedRatio = 1.1f;

        private CharacterMainControl _originalCharacter;
        private bool _hasSplit = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _originalCharacter = character;
            _hasSplit = false;

            if (character?.Health != null)
            {
                Health.OnHurt += OnCharacterHurt;
            }
        }

        private void OnCharacterHurt(Health health, DamageInfo damageInfo)
        {
            if (health != _originalCharacter?.Health) return;
            if (_hasSplit) return;

            // 残血时触发分裂
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

        private void TriggerSplit(CharacterMainControl character)
        {
            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady)
            {
                Debug.LogWarning("[SplitBehavior] EggSpawnHelper 未就绪");
                return;
            }

            Vector3 deathPosition = character.transform.position;
            int splitCount = Random.Range(MinSplitCount, MaxSplitCount + 1);
    
            helper.SpawnCloneCircle(
                originalEnemy: character,
                centerPosition: deathPosition,
                count: splitCount,
                radius: SplitRadius,
                healthMultiplier: SplitHealthRatio,
                damageMultiplier: SplitDamageRatio,
                speedMultiplier: SplitSpeedRatio,
                scaleMultiplier: 1f,
                preventElite: false,
                onAllSpawned: (clones) =>  // 直接获取所有分身
                {
                    if (clones == null || clones.Count == 0)
                    {
                        return;
                    }
                    foreach (var clone in clones)
                    {
                        if (clone != null)
                        {
                            clone.BeforeCharacterSpawnLootOnDead += (damageInfo) =>
                            {
                                ModBehaviour.Instance?.StartCoroutine(ClearCloneLootBox(clone.transform.position));
                            };
                        }
                    }
                });
        }

        private IEnumerator ClearCloneLootBox(Vector3 deathPosition)
        {
            yield return new WaitForSeconds(0.1f);

            InteractableLootbox lootbox = FindNearbyLootBox(deathPosition);
            if (lootbox == null || lootbox.Inventory == null) yield break;

            var inv = lootbox.Inventory;
            var items = new List<Item>();
            foreach (var it in inv)
                if (it != null)
                    items.Add(it);

            if (items.Count <= 1) yield break;

            // 随机保留一件掉落
            int keepIndex = Random.Range(0, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (i == keepIndex) continue;
                items[i]?.DestroyTree();
            }
        }

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

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_originalCharacter != null)
            {
                Health.OnHurt -= OnCharacterHurt;
            }
            _originalCharacter = null;
            _hasSplit = false;
        }
    }
}