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
                new EliteComboDefinition(
                    "heavy_tank",
                    LocalizationManager.GetText("Combo_heavy_tank_Name", "不破要塞"), 
                    new List<string> { "Hardening", "Regeneration", "Guardian" }, 
                    1.0f
                ),
                new EliteComboDefinition(
                    "phantom_assassin",
                    LocalizationManager.GetText("Combo_phantom_assassin_Name", "虚空行者"), 
                    new List<string> { "Invisibility", "Vampirism" }, 
                    0.8f
                ),
                new EliteComboDefinition(
                    "chaos_master",
                    LocalizationManager.GetText("Combo_chaos_master_Name", "狂乱之源"), 
                    new List<string> { "Chaos", "MultiShot", "Distortion" }, 
                    0.5f
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