using System.Collections.Generic;
using EliteEnemies.EliteEnemy.BuffsSystem;
using ItemStatsSystem.Stats; // 确保引用了 Modifier 类所在的命名空间
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// Stat 修改器 - 通过 Item Stats 修改属性
    /// 已根据 CharacterMainControl 源码进行全量校准
    /// </summary>
    public static class StatModifier
    {
        private const string LogTag = "[EliteEnemies.StatModifier]";

        // ========== Stat 属性列表 (官方全量版) ==========
        private static readonly HashSet<string> StatAttributes = new HashSet<string>
        {
            // === 生存基础 ===
            "MaxHealth", 
            "Stamina", "StaminaDrainRate", "StaminaRecoverRate", "StaminaRecoverTime",
            "MaxEnergy", "EnergyCost", // 饱食度
            "MaxWater", "WaterCost",   // 水分
            "FoodGain", "HealGain",
            "MaxWeight",
            
            // === 移动能力 ===
            "WalkSpeed", "WalkAcc",
            "RunSpeed", "RunAcc",
            "TurnSpeed", "AimTurnSpeed",
            "DashSpeed", "DashCanControl",
            "Moveability", 
            
            // === 枪械战斗 ===
            "GunDamageMultiplier", 
            "GunCritRateGain", "GunCritDamageGain",
            "GunDistanceMultiplier", "BulletSpeedMultiplier",
            "GunScatterMultiplier", "RecoilControl", "ReloadSpeedGain",
            
            // === 近战战斗 ===
            "MeleeDamageMultiplier", 
            "MeleeCritRateGain", "MeleeCritDamageGain",
            
            // === 防御与抗性 ===
            "HeadArmor", "BodyArmor", "GasMask", "StormProtection",
            "ElementFactor_Physics", "ElementFactor_Fire", "ElementFactor_Poison",
            "ElementFactor_Electricity", "ElementFactor_Space", "ElementFactor_Ghost",
            
            // === 感知与潜行 ===
            "ViewDistance", "ViewAngle", 
            "NightVisionAbility", "NightVisionType",
            "HearingAbility", "SenseRange", "SoundVisable",
            "VisableDistanceFactor",
            "WalkSoundRange", "RunSoundRange",
            
            // === 其他 ===
            "InventoryCapacity", "PetCapcity", "FlashLight"
        };

        // ========== 基础接口 ==========

        public static bool CanModify(string attributeName)
        {
            return StatAttributes.Contains(attributeName);
        }

        public static Modifier AddModifier(CharacterMainControl character, string attributeName, float value, ModifierType type)
        {
            if (character == null || character.CharacterItem == null) return null;
            if (!CanModify(attributeName))
            {
                Debug.LogWarning($"{LogTag} 属性 {attributeName} 不在支持列表中");
                return null;
            }

            var stat = character.CharacterItem.GetStat(attributeName);
            if (stat == null)
            {
                Debug.LogWarning($"{LogTag} 无法获取 Stat: {attributeName}");
                return null;
            }

            // 使用 Modifier 构造函数
            var modifier = new Modifier(type, value, EliteBuffModifierManager.Instance); 
            stat.AddModifier(modifier);
            
            return modifier; 
        }

        public static void RemoveModifier(CharacterMainControl character, string attributeName, Modifier modifier)
        {
            if (character == null || character.CharacterItem == null || modifier == null) return;
            
            var stat = character.CharacterItem.GetStat(attributeName);
            if (stat != null)
            {
                stat.RemoveModifier(modifier);
            }
        }

        // ========== 常用常量定义 ==========
        public static class Attributes
        {
            // 生存
            public const string MaxHealth = "MaxHealth";
            public const string Stamina = "Stamina";
            public const string MaxWeight = "MaxWeight";
            
            // 伤害
            public const string GunDamageMultiplier = "GunDamageMultiplier";
            public const string MeleeDamageMultiplier = "MeleeDamageMultiplier";
            public const string GunCritRateGain = "GunCritRateGain";
            
            // 移动
            public const string WalkSpeed = "WalkSpeed";
            public const string RunSpeed = "RunSpeed";
            public const string WalkAcc = "WalkAcc";
            public const string RunAcc = "RunAcc";
            public const string Moveability = "Moveability";
            public const string TurnSpeed = "TurnSpeed";
            public const string AimTurnSpeed = "AimTurnSpeed";
            
            // 射击
            public const string GunScatterMultiplier = "GunScatterMultiplier";
            public const string BulletSpeedMultiplier = "BulletSpeedMultiplier";
            public const string GunDistanceMultiplier = "GunDistanceMultiplier";
            public const string RecoilControl = "RecoilControl";
            public const string ReloadSpeedGain = "ReloadSpeedGain";
            
            // 感知
            public const string ViewDistance = "ViewDistance";
            public const string ViewAngle = "ViewAngle";
            public const string HearingAbility = "HearingAbility";
            public const string SenseRange = "SenseRange";
            public const string VisableDistanceFactor = "VisableDistanceFactor";
            
            // 防御
            public const string HeadArmor = "HeadArmor";
            public const string BodyArmor = "BodyArmor";
            
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