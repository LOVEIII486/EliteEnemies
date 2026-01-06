using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// Stat 修改器
    /// </summary>
    public static class StatModifier
    {
        private const string LogTag = "[EliteEnemies.StatModifier]";

        /// <summary>
        /// 为角色添加属性修改器并返回实例
        /// </summary>
        /// <param name="enemy">目标角色</param>
        /// <param name="statKey">属性 Key</param>
        /// <param name="value">修改数值 (例如 0.5f 代表 +50%)</param>
        /// <param name="type">修改类型 (默认百分比叠加)</param>
        /// <param name="source">来源标识 (用于后续批量移除，默认使用 EliteAffix 前缀)</param>
        /// <returns>创建的 Modifier 实例，用于精准移除</returns>
        public static Modifier AddModifier(CharacterMainControl enemy, string statKey, float value, ModifierType type = ModifierType.PercentageMultiply, object source = null)
        {
            if (enemy == null || enemy.CharacterItem == null) return null;

            Stat targetStat = enemy.CharacterItem.Stats.GetStat(statKey);
            
            if (targetStat == null)
            {
                targetStat = enemy.GetComponent<StatCollection>()?.GetStat(statKey);
            }

            if (targetStat != null)
            {
                Modifier modifier = new Modifier(type, value, source ?? ("EliteAffix_" + statKey));
                targetStat.AddModifier(modifier);

                if (statKey == Attributes.MaxHealth && enemy.Health != null)
                {
                    enemy.Health.AddHealth(enemy.Health.MaxHealth);
                }

                return modifier;
            }

            Debug.LogWarning($"{LogTag} 未能在角色 {enemy.name} 上找到属性 Key: {statKey}");
            return null;
        }
        
        public static void RemoveModifier(CharacterMainControl enemy, string statKey, Modifier modifier)
        {
            if (enemy == null || modifier == null) return;

            Stat targetStat = enemy.CharacterItem?.Stats.GetStat(statKey);
            if (targetStat == null)
            {
                targetStat = enemy.GetComponent<StatCollection>()?.GetStat(statKey);
            }

            targetStat?.RemoveModifier(modifier);
        }
        
        public static void RemoveAllModifiersFromSource(CharacterMainControl enemy, object source)
        {
            if (enemy == null || enemy.CharacterItem == null || source == null) return;
            enemy.CharacterItem.RemoveAllModifiersFrom(source);
        }

        public static class Attributes
        {
            public const string MaxHealth = "MaxHealth"; 
            public const string Stamina = "Stamina";
            public const string StaminaDrainRate = "StaminaDrainRate";
            public const string StaminaRecoverRate = "StaminaRecoverRate";
            public const string StaminaRecoverTime = "StaminaRecoverTime";
            public const string MaxEnergy = "MaxEnergy";
            public const string EnergyCost = "EnergyCost";
            public const string MaxWater = "MaxWater";
            public const string WaterCost = "WaterCost";
            public const string MaxWeight = "MaxWeight";
            public const string FoodGain = "FoodGain";
            public const string HealGain = "HealGain";

            public const string MoveSpeed = "MoveSpeed"; // 不存在，自动分发到 WalkSpeed 和 RunSpeed
            public const string WalkSpeed = "WalkSpeed";
            public const string WalkAcc = "WalkAcc";
            public const string RunSpeed = "RunSpeed";
            public const string RunAcc = "RunAcc";
            public const string TurnSpeed = "TurnSpeed";
            public const string AimTurnSpeed = "AimTurnSpeed";
            public const string DashSpeed = "DashSpeed";
            public const string Moveability = "Moveability";

            public const string GunDamageMultiplier = "GunDamageMultiplier";
            public const string GunShootSpeedMultiplier = "GunShootSpeedMultiplier";
            public const string ReloadSpeedGain = "ReloadSpeedGain";
            public const string GunCritRateGain = "GunCritRateGain";
            public const string GunCritDamageGain = "GunCritDamageGain";
            public const string BulletSpeedMultiplier = "BulletSpeedMultiplier";
            public const string RecoilControl = "RecoilControl";
            public const string GunScatterMultiplier = "GunScatterMultiplier";
            public const string GunDistanceMultiplier = "GunDistanceMultiplier";
            
            public const string MeleeDamageMultiplier = "MeleeDamageMultiplier";
            public const string MeleeCritRateGain = "MeleeCritRateGain";
            public const string MeleeCritDamageGain = "MeleeCritDamageGain";

            public const string HeadArmor = "HeadArmor";
            public const string BodyArmor = "BodyArmor";
            public const string ElementFactor_Physics = "ElementFactor_Physics";
            public const string ElementFactor_Fire = "ElementFactor_Fire";
            public const string ElementFactor_Poison = "ElementFactor_Poison";
            public const string ElementFactor_Electricity = "ElementFactor_Electricity";
            public const string ElementFactor_Space = "ElementFactor_Space";
            public const string ElementFactor_Ghost = "ElementFactor_Ghost";
            public const string ElementFactor_Ice = "ElementFactor_Ice";

            public const string ViewDistance = "ViewDistance";
            public const string ViewAngle = "ViewAngle";
            public const string HearingAbility = "HearingAbility";
            public const string SenseRange = "SenseRange";
            public const string WalkSoundRange = "WalkSoundRange";
            public const string RunSoundRange = "RunSoundRange";
            public const string VisableDistanceFactor = "VisableDistanceFactor";

            public const string GasMask = "GasMask";
            public const string InventoryCapacity = "InventoryCapacity";
            public const string PetCapcity = "PetCapcity";
            public const string FlashLight = "FlashLight";
            
            public const string StormProtection = "StormProtection";
            public const string ColdProtection = "ColdProtection";
            public const string HeatProtection = "HeatProtection";
    
            public const string WaterEnergyRecoverMultiplier = "WaterEnergyRecoverMultiplier";
            public const string NightVisionAbility = "NightVisionAbility";
            public const string DashCanControl = "DashCanControl";
        }
    }
}