using System;
using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Core;
using EliteEnemies.DebugTools;
using EliteEnemies.Stats;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// AI 角色初始化时应用精英效果
    /// </summary>
    [HarmonyPatch(typeof(AICharacterController), nameof(AICharacterController.Init))]
    internal static class EliteSpawnPatch
    {
        private const string LogTag = "[EliteEnemies.Patch]";

        static void Postfix(AICharacterController __instance, CharacterMainControl _characterMainControl)
        {
            try
            {
                var cmc = _characterMainControl;
                if (!cmc) return;   // 空引用：连归类都无从谈起，不记账

                // 下面三道提前 return 都**记账**（未参与判定）。早先它们什么都不留，
                // 于是"日志里怎么完全没看见那只怪"无从回答——总数也就对不上账。
                if (cmc.IsMainCharacter)
                {
                    SessionStats.RecordNotInvolved(SkipReason.MainCharacter);
                    return;
                }

                // 排除友军
                var main = LevelManager.Instance?.MainCharacter;
                if (main && cmc.Team == main.Team)
                {
                    SessionStats.RecordNotInvolved(SkipReason.FriendlyTeam);
                    return;
                }

                var preset = cmc.characterPreset;
                if (preset == null)
                {
                    SessionStats.RecordNotInvolved(SkipReason.NoPreset);
                    return;
                }

                // 获取资源名作为唯一标识符
                string rName = preset.name;
                string baseName = EliteEnemyCore.ResolveBaseName(cmc);
                
                // 1. 忽略逻辑判定
                //
                // ⚠ 这里**只能**判预设，判不了「角色身上的标记」：本补丁是
                // AICharacterController.Init 的 Postfix，而那次 Init 是在
                // CreateCharacterAsync **内部**跑的——此刻调用方还没拿到角色，
                // 任何"生成之后再挂标记"的做法都赶不上这一次判定。
                // （本工程历史上正是这么试过：EliteIgnoredTag 组件，从不生效，已删。）
                if (EliteEnemyCore.IsIgnoredPreset(preset) ||
                    PresetDirectory.IgnoredGenericPresets.Contains(rName))
                {
                    SessionStats.RecordSkipped(rName, baseName, SkipReason.Ignored);
                    return;
                }
                
                // 2. 类型分类判定
                bool isBoss = PresetDirectory.BossPresets.Contains(rName);
                bool isMerchant = PresetDirectory.MerchantPresets.Contains(rName);
                bool isNormal = PresetDirectory.IsEligiblePreset(preset);
                
                // 3. 自动注册逻辑
                if (!isBoss && !isMerchant && !isNormal)
                {
                    if (PresetDirectory.TryAutoRegisterExternalPreset(preset))
                    {
                        isNormal = true;
                    }
                }

                // 4. 精英化概率计算
                float chance = 0f;
                if (isBoss)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.BossEliteChance);
                else if (isMerchant)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.MerchantEliteChance);
                else if (isNormal)
                    chance = Mathf.Clamp01(EliteEnemyCore.Config.NormalEliteChance);
                else
                {
                    SessionStats.RecordSkipped(rName, baseName, SkipReason.IneligibleType);
                    return;
                }

                // 随机判定
                if (UnityEngine.Random.value > chance)
                {
                    SessionStats.RecordSkipped(rName, baseName, SkipReason.ChanceMissed);
                    return;
                }

                // 5. 应用精英化
                int maxCount = Mathf.Max(1, EliteEnemyCore.Config.MaxAffixCount);
                var affixes = AffixSelector.SelectRandomAffixes(maxCount, cmc);
                
                if (affixes == null || affixes.Count == 0)
                {
                    SessionStats.RecordSkipped(rName, baseName, SkipReason.NoAffixSelected);
                    return;
                }

                // 修改属性并标记
                EliteEnemyCore.ForceMakeElite(cmc, affixes);

                // 6. 附加行为组件
                AttachBehaviorComponent(cmc, affixes);

                SessionStats.RecordElite(rName, baseName, affixes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} AI初始化补丁执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void AttachBehaviorComponent(CharacterMainControl cmc, List<string> affixes)
        {
            if (cmc == null || affixes == null || affixes.Count == 0) return;

            // 检查是否有任何词缀需要特殊的逻辑监听
            bool needsBehavior = false;
            foreach (string affixName in affixes)
            {
                if (AffixBehaviorManager.IsRegistered(affixName))
                {
                    needsBehavior = true;
                    break;
                }
            }

            if (!needsBehavior) return;

            // 幂等：同一个角色身上**只挂一份**。
            //
            // `EliteBehaviorComponent.Initialize` 只在**实例内**幂等（`_isInitialized`），
            // 挡不住"第二个实例"。而真挂上两份的后果是**静默**的：
            // 每个词条行为各跑一遍，且 `EliteStatSource` 的来源型修改器会**加两次**，
            // 而撤销走的是 `RemoveAllModifiersFromSource`（按来源整体撤）⇒ 只撤一次
            // ⇒ **永久残留**。
            //
            // 今天**不可达**（已实测）：`AICharacterController.Init` 全树只有两个调用点——
            // `CharacterRandomPreset.cs:360` 与 `EnemyCreator.cs:69` 的
            // `Object.Instantiate(aiController).Init(...)`——**都是新实例**。
            // 这条守卫防的是"游戏把 `Init` 改成可重入（池化 / 复活复用）"或
            // "本模组将来新增一条调用路径"：那时它会从不可达直接变成每只精英都双份。
            var existing = cmc.GetComponent<EliteBehaviorComponent>();
            if (existing != null)
            {
                Debug.LogWarning($"{LogTag} {cmc.name} 身上已有 EliteBehaviorComponent，" +
                                 "已跳过重复挂载——**这条日志若出现，说明「每次精英化只挂一次」的前提变了**，" +
                                 "请复核 AICharacterController.Init 的调用路径。");
                return;
            }

            // 添加核心逻辑驱动组件
            var component = cmc.gameObject.AddComponent<EliteBehaviorComponent>();
            component.Initialize(cmc, affixes);

            // 注册死亡清理回调
            cmc.BeforeCharacterSpawnLootOnDead += (damageInfo) => { 
                if(component != null) component.OnDeath(damageInfo); 
            };
        }
    }
}
