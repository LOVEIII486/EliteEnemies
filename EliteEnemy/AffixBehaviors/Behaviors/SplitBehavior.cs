using System.Collections.Generic;
using System.Reflection;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.Core;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using SodaCraft.Localizations;
using EliteEnemies.DebugTool;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【分裂】词缀 - 敌人残血时分裂成多个较弱的小怪
    /// 集成了防卡死全局计数限制
    /// </summary>
    public class SplitBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Split";

        private static readonly int MinSplitCount = 2;
        private static readonly int MaxSplitCount = 4;
        private static readonly float SplitRadius = 2.0f;
        private static readonly float SplitHealthRatio = 0.6f;
        private static readonly float SplitDamageRatio = 0.7f;
        private static readonly float SplitSpeedRatio = 1.15f;

        // 全局活跃分裂体计数器 & 上限配置
        // 限制场景中同时存在的分裂体不超过 40 
        public static int GlobalActiveSplitClones { get; private set; } = 0;

        private CharacterMainControl _originalCharacter;
        private bool _hasSplit = false;

        // 场景重置时清零计数器，防止数值残留
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GlobalActiveSplitClones = 0;
        }

        // 维护计数器
        public class SplitCloneMarker : MonoBehaviour
        {
            private void Start()
            {
                SplitBehavior.GlobalActiveSplitClones++;
            }

            private void OnDestroy()
            {
                SplitBehavior.GlobalActiveSplitClones = Mathf.Max(0, SplitBehavior.GlobalActiveSplitClones - 1);
            }
        }

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

            // 简单限制无限分裂
            if (health.MaxHealth <= 20)
            {
                // Debug.LogWarning("[EliteEnemies.SplitBehavior] 敌人太弱小，无法分裂");
                return;
            }

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
            int maxClones = EliteEnemyCore.Config.SplitAffixMaxCloneCount;
            float minFps = EliteEnemyCore.Config.SplitAffixMinFPSThreshold;
            
            // 1. 熔断检查
            float currentFPS = 1.0f / Mathf.Max(Time.smoothDeltaTime, 0.001f);
            if (currentFPS < minFps || GlobalActiveSplitClones >= maxClones)
            {
                Debug.LogWarning($"[SplitBehavior] 熔断触发 (FPS:{currentFPS:F1}, Count:{GlobalActiveSplitClones})");
                return;
            }

            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady) return;
            
            // 2. 捕获父级当前的 Buff 快照
            List<Buff> buffsToInherit = new List<Buff>();
            var activeBuffs = BuffInheritanceHelper.GetActiveBuffs(character);
            if (activeBuffs != null)
            {
                foreach (var b in activeBuffs)
                {
                    if (b != null) buffsToInherit.Add(b);
                }
            }

            // // 3. 捕获父级当前的精英词缀
            // List<string> affixesToInherit = new List<string>();
            // var parentMarker = character.GetComponent<EliteEnemyCore.EliteMarker>();
            // if (parentMarker != null && parentMarker.Affixes != null)
            // {
            //     affixesToInherit.AddRange(parentMarker.Affixes);
            //     if (affixesToInherit.Contains(AffixName)) affixesToInherit.Remove(AffixName);
            // }
            
            Vector3 deathPosition = character.transform.position;
            int splitCount = Random.Range(MinSplitCount, MaxSplitCount + 1);
            int remainingQuota = maxClones - GlobalActiveSplitClones;
            if (splitCount > remainingQuota) splitCount = Mathf.Max(1, remainingQuota);

            string originalName = character.characterPreset.nameKey.ToPlainText();

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
                customDisplayName: originalName,
                onAllSpawned: (clones) => 
                {
                    if (clones == null) return;
                    foreach (var clone in clones)
                    {
                        if (clone != null)
                        {
                            clone.gameObject.AddComponent<SplitCloneMarker>();

                            // // 应用继承的精英词缀
                            // if (affixesToInherit.Count > 0)
                            // {
                            //     EliteEnemyCore.ForceMakeElite(clone, affixesToInherit);
                            // }

                            // 应用继承的普通 Buff
                            if (buffsToInherit.Count > 0)
                            {
                                BuffInheritanceHelper.ApplyBuffsTo(clone, buffsToInherit);
                            }
                        }
                    }
                });
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

    [HarmonyPatch(typeof(InteractableLootbox), "CreateFromItem")]
    public static class SplitLootPatch
    {
        [HarmonyPostfix]
        public static void Postfix(InteractableLootbox __result, Item item)
        {
            if (__result == null || item == null) return;

            CharacterMainControl character = GetCharacterFromItem(item);
            if (character == null) return;

            if (character.GetComponent<SplitBehavior.SplitCloneMarker>() != null)
            {
                ClearCloneLootBoxInventory(__result);
            }
        }

        private static CharacterMainControl GetCharacterFromItem(Item item)
        {
            MethodInfo method = item.GetType().GetMethod("GetCharacterItem");
            if (method != null)
            {
                Item characterItem = method.Invoke(item, null) as Item;
                if (characterItem?.GetComponent<CharacterMainControl>() is CharacterMainControl c) return c;
            }

            Transform current = item.transform;
            int depth = 0;
            while (current != null && depth < 10)
            {
                if (current.GetComponent<CharacterMainControl>() is CharacterMainControl c) return c;
                current = current.parent;
                depth++;
            }

            return null;
        }

        private static void ClearCloneLootBoxInventory(InteractableLootbox lootbox)
        {
            var inv = lootbox.Inventory;
            if (inv == null) return;

            var items = new List<Item>();
            foreach (var it in inv)
                if (it != null)
                    items.Add(it);

            if (items.Count <= 2) return;

            HashSet<int> keepIndices = new HashSet<int>();
            while (keepIndices.Count < 2)
            {
                keepIndices.Add(Random.Range(0, items.Count));
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (keepIndices.Contains(i)) continue;
                items[i]?.DestroyTree();
            }
        }
    }

    public static class BuffInheritanceHelper
    {
        private static FieldInfo _buffsField;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            var type = typeof(Duckov.Buffs.CharacterBuffManager);
            _buffsField = type.GetField("buffs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                          ?? type.GetField("_buffs",
                              BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            _initialized = true;
        }

        public static List<Duckov.Buffs.Buff> GetActiveBuffs(CharacterMainControl target)
        {
            if (target == null) return null;
            var manager = target.GetComponent<Duckov.Buffs.CharacterBuffManager>();
            if (manager == null) return null;

            Initialize();
            if (_buffsField == null) return null;

            return _buffsField.GetValue(manager) as List<Duckov.Buffs.Buff>;
        }

        public static void ApplyBuffsTo(CharacterMainControl target, List<Duckov.Buffs.Buff> buffsToCopy)
        {
            if (target == null || buffsToCopy == null || buffsToCopy.Count == 0) return;

            foreach (var originalBuff in buffsToCopy)
            {
                if (originalBuff == null) continue;

                // 克隆 Buff 实例
                var clonedBuff = Object.Instantiate(originalBuff);

                // 修正名称 (移除 (Clone) 后缀)
                clonedBuff.name = originalBuff.name.Replace("(Clone)", "");
                
                try
                {
                    target.AddBuff(clonedBuff, null, 1);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BuffInheritance] 无法继承 Buff {clonedBuff.name}: {e.Message}");
                    Object.Destroy(clonedBuff.gameObject);
                }
            }
        }
    }
}