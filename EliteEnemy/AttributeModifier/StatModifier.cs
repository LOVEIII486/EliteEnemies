using System;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Stats;

namespace EliteEnemies
{
    /// <summary>
    /// Stat 修改器 - 通过 Item Stats 修改属性
    /// </summary>
    public static class StatModifier
    {
        private const string LogTag = "[EliteEnemies.StatModifier]";

        // ========== Stat 属性列表 ==========
        
        private static readonly HashSet<string> StatAttributes = new HashSet<string>
        {
            // 生命
            "MaxHealth", "CurrentHealth",
            
            // 伤害
            "GunDamageMultiplier", "MeleeDamageMultiplier", "GunCritRateGain",
            
            // 移动
            "WalkSpeed", "RunSpeed", "SprintSpeed",
            
            // 射击
            "GunScatterMultiplier", "BulletSpeedMultiplier", "GunDistanceMultiplier",
            
            // 视野
            "ViewDistance", "ViewAngle", "NightVisionAbility",
            
            // 防御
            "ArmorValue", "DodgeChance",
            
            // 元素抗性
            "ElementFactor_Physics", "ElementFactor_Fire", "ElementFactor_Poison",
            "ElementFactor_Electricity", "ElementFactor_Space", "ElementFactor_Ghost",
        };

        // ========== 基础接口 ==========

        /// <summary>
        /// 检查属性是否可通过 Stat 修改
        /// </summary>
        public static bool CanModify(string attributeName)
        {
            return StatAttributes.Contains(attributeName);
        }

        /// <summary>
        /// 设置 Stat 基础值（永久修改，不可逆）
        /// </summary>
        public static void Set(CharacterMainControl character, string statName, float value)
        {
            if (character?.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} CharacterItem is null");
                return;
            }

            try
            {
                var stat = character.CharacterItem.GetStat(statName);
                if (stat != null)
                {
                    stat.BaseValue = value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} Failed to set {statName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 倍乘 Stat 基础值（永久修改，不可逆）
        /// </summary>
        public static void Multiply(CharacterMainControl character, string statName, float multiplier)
        {
            if (character?.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} CharacterItem is null");
                return;
            }

            try
            {
                var stat = character.CharacterItem.GetStat(statName);
                if (stat != null)
                {
                    stat.BaseValue *= multiplier;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} Failed to multiply {statName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加临时 Modifier（可移除，适合 Buff）
        /// </summary>
        /// <returns>返回 Modifier 对象，用于后续移除</returns>
        public static Modifier AddModifier(
            CharacterMainControl character, 
            string statName, 
            float value, 
            ModifierType type = ModifierType.PercentageMultiply)
        {
            if (character?.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} CharacterItem is null");
                return null;
            }

            try
            {
                var stat = character.CharacterItem.GetStat(statName);
                if (stat == null)
                {
                    Debug.LogWarning($"{LogTag} Stat '{statName}' not found");
                    return null;
                }

                var modifier = new Modifier(type, value, character);
                stat.AddModifier(modifier);
                return modifier;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} Failed to add modifier to {statName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 移除 Modifier
        /// </summary>
        public static void RemoveModifier(CharacterMainControl character, string statName, Modifier modifier)
        {
            if (character?.CharacterItem == null || modifier == null)
            {
                return;
            }

            try
            {
                var stat = character.CharacterItem.GetStat(statName);
                stat?.RemoveModifier(modifier);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} Failed to remove modifier from {statName}: {ex.Message}");
            }
        }

        // ========== 批量修改接口 ==========

        /// <summary>
        /// 批量修改基础属性（用于精英初始化）
        /// </summary>
        public static void ApplyMultipliers(
            CharacterMainControl character, 
            float? healthMult = null,
            float? damageMult = null, 
            float? speedMult = null,
            bool healToFull = false)
        {
            if (character?.CharacterItem == null)
            {
                Debug.LogWarning($"{LogTag} CharacterItem is null");
                return;
            }

            try
            {
                // 血量
                if (healthMult.HasValue && !Mathf.Approximately(healthMult.Value, 1f))
                {
                    Multiply(character, "MaxHealth", healthMult.Value);
                    if (healToFull && character.Health != null)
                    {
                        character.Health.SetHealth(character.Health.MaxHealth);
                    }
                }

                // 伤害
                if (damageMult.HasValue && !Mathf.Approximately(damageMult.Value, 1f))
                {
                    Multiply(character, "GunDamageMultiplier", damageMult.Value);
                    Multiply(character, "MeleeDamageMultiplier", damageMult.Value);
                }

                // 速度
                if (speedMult.HasValue && !Mathf.Approximately(speedMult.Value, 1f))
                {
                    Multiply(character, "WalkSpeed", speedMult.Value);
                    Multiply(character, "RunSpeed", speedMult.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} ApplyMultipliers failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量修改（字典方式）
        /// </summary>
        public static void ApplyBatch(
            CharacterMainControl character, 
            Dictionary<string, float> modifications, 
            bool multiply = false)
        {
            if (character?.CharacterItem == null || modifications == null)
            {
                return;
            }

            foreach (var kvp in modifications)
            {
                if (multiply)
                    Multiply(character, kvp.Key, kvp.Value);
                else
                    Set(character, kvp.Key, kvp.Value);
            }
        }

        // ========== 可用属性 ==========

        public static class Attributes
        {
            // 生命
            public const string MaxHealth = "MaxHealth";
            public const string CurrentHealth = "CurrentHealth";
            
            // 伤害
            public const string GunDamageMultiplier = "GunDamageMultiplier";
            public const string MeleeDamageMultiplier = "MeleeDamageMultiplier";
            public const string GunCritRateGain = "GunCritRateGain";
            
            // 移动
            public const string WalkSpeed = "WalkSpeed";
            public const string RunSpeed = "RunSpeed";
            public const string SprintSpeed = "SprintSpeed";
            
            // 射击
            public const string GunScatterMultiplier = "GunScatterMultiplier";
            public const string BulletSpeedMultiplier = "BulletSpeedMultiplier";
            public const string GunDistanceMultiplier = "GunDistanceMultiplier";
            
            // 视野
            public const string ViewDistance = "ViewDistance";
            public const string ViewAngle = "ViewAngle";
            public const string NightVisionAbility = "NightVisionAbility";
            
            // 防御
            public const string ArmorValue = "ArmorValue";
            public const string DodgeChance = "DodgeChance";
            
            // 元素
            public const string ElementFactor_Physics = "ElementFactor_Physics";
            public const string ElementFactor_Fire = "ElementFactor_Fire";
            public const string ElementFactor_Poison = "ElementFactor_Poison";
            public const string ElementFactor_Electricity = "ElementFactor_Electricity";
            public const string ElementFactor_Space = "ElementFactor_Space";
            public const string ElementFactor_Ghost = "ElementFactor_Ghost";
        }
    }
}
