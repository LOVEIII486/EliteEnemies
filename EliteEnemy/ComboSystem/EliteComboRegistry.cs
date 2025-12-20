using System.Collections.Generic;
using System.Linq;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.ComboSystem
{
    public static class EliteComboRegistry
    {
        public static List<EliteComboDefinition> ComboPool { get; private set; }

        static EliteComboRegistry()
        {
            InitializePool();
        }

        private static void InitializePool()
        {
            ComboPool = new List<EliteComboDefinition>
            {
                // 1. 【全域匠师】 - 暖金色 (#E6C27A)
                // 音乐家 / 厨师 / 枪匠 / 锁匠 —— 全功能支援与生产专家
                new EliteComboDefinition(
                    "omni_artisan",
                    LocalizationManager.GetText("Combo_omni_artisan_Name", "全域匠师"),
                    new List<string> { "Musician", "Chef", "Gunsmith", "Locksmith" },
                    0.5f, "E6C27A"
                ),

                // 2. 【超构巨躯】 - 钢铁灰蓝 (#8FAFC6)
                // 硬化 + 巨大化 + 史莱姆 —— 高耐久的形态强化单位
                new EliteComboDefinition(
                    "hyper_colossus",
                    LocalizationManager.GetText("Combo_hyper_colossus_Name", "超构巨躯"),
                    new List<string> { "Hardening", "Giant", "Slime" },
                    0.7f, "8FAFC6"
                ),

                // 3. 【相位潜猎者】 - 冷相紫 (#9FA8DA)
                // 迷你 + 迅捷 + 隐身 —— 高机动、难命中的潜行猎杀者
                new EliteComboDefinition(
                    "phase_stalker",
                    LocalizationManager.GetText("Combo_phase_stalker_Name", "相位潜猎者"),
                    new List<string> { "Mini", "NineDragons", "Invisible" },
                    1f, "9FA8DA"
                ),

                // 4. 【过载映像体】 - 灼橙色 (#FFB347)
                // 仿身泪滴 + 九龙 + 过载 —— 不稳定的高压战斗复制体
                new EliteComboDefinition(
                    "overload_simulacrum",
                    LocalizationManager.GetText("Combo_overload_simulacrum_Name", "过载映像体"),
                    new List<string> { "MimicTear", "NineDragons", "Overload" },
                    1f, "FFB347"
                ).WithWhitelist(
                    "EnemyPreset_Scav", 
                    "EnemyPreset_Scav_Elete", 
                    "EnemyPreset_Scav_Farm", 
                    "EnemyPreset_Scav_low", 
                    "EnemyPreset_Scav_low_ak74",
                    "EnemyPreset_USEC_Farm", 
                    "EnemyPreset_USEC_HiddenWareHouse", 
                    "EnemyPreset_USEC_Low",
                    "EnemyPreset_JLab_Raider",
                    "EnemyPreset_Boss_BALeader_Child", 
                    "EnemyPreset_Boss_3Shot_Child", 
                    "EnemyPreset_Boss_Speedy_Child",
                    "EnemyPreset_Boss_Storm_1_Child"
                ),

                // 5. 【混沌蚕食】 - 病蚀紫 (#B57BA6)
                // 混沌 + 食粪者 + 弹匣诅咒 —— 资源与状态的全面侵蚀
                new EliteComboDefinition(
                    "chaos_devour",
                    LocalizationManager.GetText("Combo_chaos_devour_Name", "混沌蚕食"),
                    new List<string> { "Chaos", "DungEater", "MagazineCurse" },
                    1f, "B57BA6"
                ),

                // 6. 【全域抑制场】 - 电磁薄绿 (#7FD1AE)
                // 致盲 + 缓速 + 震慑 + EMP —— 多重控制叠加的封锁领域
                new EliteComboDefinition(
                    "suppression_field",
                    LocalizationManager.GetText("Combo_suppression_field_Name", "全域抑制场"),
                    new List<string> { "Blindness", "Slow", "Stun", "EMP" },
                    1f, "7FD1AE"
                ),

                // 7. 【火力绞杀网】 - 高危玫红 (#FF7A8A)
                // 报复 + 掷弹手 + 多重射击 —— 弹幕与爆炸构成的死亡火力区
                new EliteComboDefinition(
                    "killzone_barrage",
                    LocalizationManager.GetText("Combo_killzone_barrage_Name", "火力绞杀网"),
                    new List<string> { "Revenge", "Grenadier", "MultiShot" },
                    1f, "FF7A8A"
                ),

                // 8. 【不灭血誓】 - 血誓圣蓝 (#A7C7E7)
                // 守护 + 不死 + 吸血 —— 高续航、难以终结的生存循环
                new EliteComboDefinition(
                    "immortal_blood_pact",
                    LocalizationManager.GetText("Combo_immortal_blood_pact_Name", "不灭血誓"),
                    new List<string> { "Guardian", "Undead", "Vampirism" },
                    0.7f, "A7C7E7"
                )
            };
        }


        public static EliteComboDefinition GetRandomCombo()
        {
            if (ComboPool == null || ComboPool.Count == 0) return null;

            float totalWeight = ComboPool.Sum(c => c.Weight);
            float roll = Random.Range(0f, totalWeight);
            float currentSum = 0;

            foreach (var combo in ComboPool)
            {
                currentSum += combo.Weight;
                if (roll <= currentSum) return combo;
            }

            return ComboPool[0];
        }
    }
}