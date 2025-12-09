using System.Collections.Generic;
using System.Reflection;
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
        // 限制场景中同时存在的分裂体不超过 40 个，防止指数级爆炸
        public static int GlobalActiveSplitClones { get; private set; } = 0;
        private const int MaxGlobalSplitClones = 40;
        private const float MinFPSThreshold = 30.0f; // 最低帧率阈值，低于此值不分裂

        private CharacterMainControl _originalCharacter;
        private bool _hasSplit = false;
        
        // 场景重置时清零计数器，防止数值残留
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GlobalActiveSplitClones = 0;
        }

        // 标记组件现在负责维护计数器
        public class SplitCloneMarker : MonoBehaviour 
        {
            private void Start()
            {
                // 只有当物体激活时才计入
                SplitBehavior.GlobalActiveSplitClones++;
            }

            private void OnDestroy()
            {
                // 销毁时释放配额
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
            float currentFPS = 1.0f / Mathf.Max(Time.smoothDeltaTime, 0.001f);
            if (currentFPS < MinFPSThreshold)
            {
                Debug.LogWarning($"[SplitBehavior] 性能熔断：当前FPS ({currentFPS:F1}) 低于阈值 ({MinFPSThreshold})，取消分裂。");
                return;
            }
            
            // 防卡死核心检查：如果当前活跃的分裂体过多，或者处理过的精英怪总量过大，则停止分裂
            if (GlobalActiveSplitClones >= MaxGlobalSplitClones)
            {
                Debug.LogWarning($"[SplitBehavior] 熔断机制触发：当前场上分裂体 ({GlobalActiveSplitClones}) 已达上限，取消分裂。");
                return;
            }

            // if (EliteEnemyTracker.TotalProcessedCount > 500) return;

            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady)
            {
                Debug.LogWarning("[EliteEnemies.SplitBehavior] EggSpawnHelper 未就绪");
                return;
            }

            Vector3 deathPosition = character.transform.position;
            int splitCount = Random.Range(MinSplitCount, MaxSplitCount + 1);
            
            // 再次检查配额，防止一次性生成导致溢出太多
            if (GlobalActiveSplitClones + splitCount > MaxGlobalSplitClones + 5) // 允许少量溢出 buffer
            {
                splitCount = Mathf.Max(1, MaxGlobalSplitClones - GlobalActiveSplitClones);
            }

            // 获取母体的本地化名称
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
                preventElite: false, // 分裂体默认不禁止再次精英化
                customDisplayName: originalName, // 传入母体名字
                onAllSpawned: (clones) => 
                {
                    if (clones == null) return;
                    foreach (var clone in clones)
                    {
                        if (clone != null)
                        {
                            // 添加标记，标记会自动增加全局计数
                            clone.gameObject.AddComponent<SplitCloneMarker>();
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
                if (it != null) items.Add(it);

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
}