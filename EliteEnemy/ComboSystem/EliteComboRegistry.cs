using System.Collections.Generic;
using System.Linq;
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
            // 注意：这里绝对不能调用 LocalizationManager.GetText！
            ComboPool = new List<EliteComboDefinition>
            {
                new EliteComboDefinition("omni_artisan", "全域匠师", new List<string> { "Musician", "Chef", "Gunsmith", "Locksmith" }, 0.5f, "E6C27A"),
                
                new EliteComboDefinition("hyper_colossus", "超构巨躯", new List<string> { "Hardening", "Giant", "Slime" }, 0.7f, "8FAFC6"),
                
                new EliteComboDefinition("phase_stalker", "相位潜猎者", new List<string> { "Mini", "NineDragons", "Invisible" }, 1f, "9FA8DA"),

                new EliteComboDefinition("overload_simulacrum", "过载映像体", new List<string> { "MimicTear", "NineDragons", "Overload" }, 1f, "FFB347")
                .WithWhitelist(
                    "EnemyPreset_Scav", "EnemyPreset_Scav_Elete", "EnemyPreset_Scav_Farm", 
                    "EnemyPreset_Scav_low", "EnemyPreset_Scav_low_ak74", "EnemyPreset_USEC_Farm", 
                    "EnemyPreset_USEC_HiddenWareHouse", "EnemyPreset_USEC_Low", "EnemyPreset_JLab_Raider",
                    "EnemyPreset_Boss_BALeader_Child", "EnemyPreset_Boss_3Shot_Child", 
                    "EnemyPreset_Boss_Speedy_Child", "EnemyPreset_Boss_Storm_1_Child","EnemyPreset_Boss_ShortEagle_Elete"
                ),

                new EliteComboDefinition("chaos_devour", "混沌蚕食", new List<string> { "Chaos", "DungEater", "MagazineCurse" }, 1f, "B57BA6"),

                new EliteComboDefinition("suppression_field", "全域抑制场", new List<string> { "Blindness", "Slow", "Stun", "EMP" }, 1f, "7FD1AE"),

                new EliteComboDefinition("killzone_barrage", "火力绞杀网", new List<string> { "Revenge", "Grenadier", "MultiShot" }, 1f, "FF7A8A"),

                new EliteComboDefinition("immortal_blood_pact", "不灭血誓", new List<string> { "Guardian", "Undead", "Vampirism" }, 0.7f, "A7C7E7")
            };
        }
    }
}