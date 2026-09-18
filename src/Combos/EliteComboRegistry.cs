using System;
using System.Collections.Generic;
using EliteEnemies.Affixes;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Combos
{
    public static class EliteComboRegistry
    {
        private const string LogTag = "[EliteEnemies.Combo]";

        public static readonly List<EliteComboDefinition> ComboPool = new List<EliteComboDefinition>
        {
            new EliteComboDefinition(
                "omni_artisan", 
                new LocalizedText("EliteEnemies_Combo_OmniArtisan_Name", "全域匠师"), 
                new List<string> { "Musician", "Chef", "Gunsmith", "Locksmith" }, 
                0.5f, "E6C27A"
            ),
            
            new EliteComboDefinition(
                "hyper_colossus", 
                new LocalizedText("EliteEnemies_Combo_HyperColossus_Name", "超构巨躯"), 
                new List<string> { "Hardening", "Giant", "Slime" }, 
                0.7f, "8FAFC6"
            ),
            
            new EliteComboDefinition(
                "phase_stalker", 
                new LocalizedText("EliteEnemies_Combo_PhaseStalker_Name", "相位潜猎者"), 
                new List<string> { "Mini", "NineDragons", "Invisible" }, 
                1f, "9FA8DA"
            ),

            new EliteComboDefinition(
                "overload_simulacrum", 
                new LocalizedText("EliteEnemies_Combo_OverloadSimulacrum_Name", "过载映像体"), 
                new List<string> { "MimicTear", "NineDragons", "Overload" }, 
                1f, "FFB347"
            ).WithWhitelist(
                "EnemyPreset_Scav", "EnemyPreset_Scav_Elete", "EnemyPreset_Scav_Farm", 
                "EnemyPreset_Scav_low", "EnemyPreset_Scav_low_ak74", "EnemyPreset_USEC_Farm", 
                "EnemyPreset_USEC_HiddenWareHouse", "EnemyPreset_USEC_Low", "EnemyPreset_JLab_Raider",
                "EnemyPreset_Boss_BALeader_Child", "EnemyPreset_Boss_3Shot_Child", 
                "EnemyPreset_Boss_Speedy_Child", "EnemyPreset_Boss_Storm_1_Child","EnemyPreset_Boss_ShortEagle_Elete"
            ),

            new EliteComboDefinition(
                "chaos_devour", 
                new LocalizedText("EliteEnemies_Combo_ChaosDevour_Name", "混沌蚕食"), 
                new List<string> { "Chaos", "DungEater", "MagazineCurse" }, 
                1f, "B57BA6"
            ),

            new EliteComboDefinition(
                "suppression_field", 
                new LocalizedText("EliteEnemies_Combo_SuppressionField_Name", "全域抑制场"), 
                new List<string> { "Blindness", "Slow", "Stun", "EMP" }, 
                1f, "7FD1AE"
            ),

            new EliteComboDefinition(
                "killzone_barrage", 
                new LocalizedText("EliteEnemies_Combo_KillzoneBarrage_Name", "火力绞杀网"), 
                new List<string> { "Revenge", "Grenadier", "MultiShot" }, 
                1f, "FF7A8A"
            ),

            new EliteComboDefinition(
                "immortal_blood_pact", 
                new LocalizedText("EliteEnemies_Combo_ImmortalBloodPact_Name", "不灭血誓"), 
                new List<string> { "Guardian", "Undead", "Vampirism" }, 
                0.7f, "A7C7E7"
            )
        };

        /// <summary>
        /// 启动自检：<c>ComboPool</c> 与词条池之间的一致性。
        ///
        /// <para><b>为什么需要它</b>：combo 的失效态**完全无声**。若某天改了词条键名，
        /// combo 里写的 id 就变成未知键，于是——</para>
        /// <list type="bullet">
        /// <item><c>EliteEnemyCore.AccumulateFromAffixes</c> 的 <c>TryGetAffix</c> **静默跳过**
        /// ⇒ 属性加成少一份；</item>
        /// <item><c>BuildColoredPrefix</c> 会在血条上显示 <c>[原键名]</c> 兜底
        /// ⇒ **看起来像正常显示，实际是坏的**；</item>
        /// <item>设置界面的描述里**静默少列**一条词条；</item>
        /// <item>**没有任何一行日志。**</item>
        /// </list>
        ///
        /// <para>同理 <c>ComboId</c> 撞车时，<c>GameConfig</c> 的两个开关会被并成一个
        /// （一起开、一起关），界面上同样看不出来。</para>
        ///
        /// <para>形态与 <c>AffixBehaviorRegistration.ValidateAffixCorrespondence</c> 一致：
        /// **只报错，不抛异常、不阻断启动**——自检是让问题可见，不是让模组加载失败。</para>
        ///
        /// <para>⚠ 调用点放在 <c>ModBehaviour</c>，**不能**放进
        /// <c>AffixBehaviorRegistration</c>：本模块已依赖 <c>Affixes</c>，反向再依赖一次会成环
        /// （见 <c>docs\词条模块审查与设计.md</c> §8 的依赖矩阵）。</para>
        /// </summary>
        public static void ValidatePool()
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var combo in ComboPool)
            {
                if (combo == null) continue;

                if (!seenIds.Add(combo.ComboId))
                {
                    Debug.LogError($"{LogTag} 组合 id 重复：'{combo.ComboId}'——" +
                                   "两个组合会共用同一个开关（一起开、一起关），请改名。");
                }

                if (combo.AffixIds == null) continue;

                foreach (string affixId in combo.AffixIds)
                {
                    if (!EliteAffixes.TryGetAffix(affixId, out _))
                    {
                        Debug.LogError($"{LogTag} 组合 '{combo.ComboId}' 引用了**不存在的词条** " +
                                       $"'{affixId}'——该词条的属性加成与显示都会被静默跳过，" +
                                       "请核对键名。");
                    }
                }
            }
        }
    }
}