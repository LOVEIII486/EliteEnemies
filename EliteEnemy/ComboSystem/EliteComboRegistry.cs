using System.Collections.Generic;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.ComboSystem
{
    public static class EliteComboRegistry
    {
        public static readonly List<EliteComboDefinition> ComboPool = new List<EliteComboDefinition>
        {
            new EliteComboDefinition(
                "omni_artisan", 
                LocalizationManager.GetText("Combo_OmniArtisan_Name", "全域匠师"), 
                new List<string> { "Musician", "Chef", "Gunsmith", "Locksmith" }, 
                0.5f, "E6C27A"
            ),
            
            new EliteComboDefinition(
                "hyper_colossus", 
                LocalizationManager.GetText("Combo_HyperColossus_Name", "超构巨躯"), 
                new List<string> { "Hardening", "Giant", "Slime" }, 
                0.7f, "8FAFC6"
            ),
            
            new EliteComboDefinition(
                "phase_stalker", 
                LocalizationManager.GetText("Combo_PhaseStalker_Name", "相位潜猎者"), 
                new List<string> { "Mini", "NineDragons", "Invisible" }, 
                1f, "9FA8DA"
            ),

            new EliteComboDefinition(
                "overload_simulacrum", 
                LocalizationManager.GetText("Combo_OverloadSimulacrum_Name", "过载映像体"), 
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
                LocalizationManager.GetText("Combo_ChaosDevour_Name", "混沌蚕食"), 
                new List<string> { "Chaos", "DungEater", "MagazineCurse" }, 
                1f, "B57BA6"
            ),

            new EliteComboDefinition(
                "suppression_field", 
                LocalizationManager.GetText("Combo_SuppressionField_Name", "全域抑制场"), 
                new List<string> { "Blindness", "Slow", "Stun", "EMP" }, 
                1f, "7FD1AE"
            ),

            new EliteComboDefinition(
                "killzone_barrage", 
                LocalizationManager.GetText("Combo_KillzoneBarrage_Name", "火力绞杀网"), 
                new List<string> { "Revenge", "Grenadier", "MultiShot" }, 
                1f, "FF7A8A"
            ),

            new EliteComboDefinition(
                "immortal_blood_pact", 
                LocalizationManager.GetText("Combo_ImmortalBloodPact_Name", "不灭血誓"), 
                new List<string> { "Guardian", "Undead", "Vampirism" }, 
                0.7f, "A7C7E7"
            )
        };
    }
}