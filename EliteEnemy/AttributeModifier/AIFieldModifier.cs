using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// AI 字段修改器 
    /// 仅处理非 Stat 的纯逻辑字段 (如巡逻范围、遗忘时间等)
    /// </summary>
    public static class AIFieldModifier
    {
        private const string LogTag = "[EliteEnemies.AIFieldModifier]";
        
        // ========== AI 字段列表 ==========
        public static class Fields
        {
            // 战斗行为
            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";
            
            // 行为开关
            public const string CanDash = "canDash";
            public const string ShootCanMove = "shootCanMove";
            
            // 注意：SightDistance, SightAngle 等已移除，请使用 StatModifier
        }

        private static readonly HashSet<string> ValidFields = new HashSet<string>
        {
            Fields.PatrolRange,
            Fields.CombatMoveRange,
            Fields.ForgetTime,
            Fields.CanDash,
            Fields.ShootCanMove
        };

        public static bool CanModify(string fieldName) => ValidFields.Contains(fieldName);

        // ========== 核心逻辑 ==========

        internal static AICharacterController GetAI(CharacterMainControl character)
        {
            if (character == null) return null;
            var ai = character.GetComponentInChildren<AICharacterController>(true);
            if (ai != null) return ai;
            return character.GetComponentInParent<AICharacterController>();
        }

        public static void ModifyImmediate(CharacterMainControl character, string fieldName, float value, bool multiply)
        {
            var ai = GetAI(character);
            if (ai == null) return;

            ApplyModification(ai, fieldName, value, multiply);
        }

        private static void ApplyModification(object target, string fieldName, float value, bool multiply)
        {
            try
            {
                var type = target.GetType();
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (field != null)
                {
                    if (field.FieldType == typeof(float))
                    {
                        float original = (float)field.GetValue(target);
                        float final = multiply ? original * value : original + value;
                        field.SetValue(target, final);
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        int original = (int)field.GetValue(target);
                        int final = multiply ? (int)(original * value) : original + (int)value;
                        field.SetValue(target, final);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        // 对于布尔值，大于0视为true
                        field.SetValue(target, value > 0);
                    }
                }
                else
                {
                    var prop = type.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite)
                    {
                        if (prop.PropertyType == typeof(float))
                        {
                            float original = (float)prop.GetValue(target);
                            float final = multiply ? original * value : original + value;
                            prop.SetValue(target, final);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 修改 AI 字段 {fieldName} 失败: {ex.Message}");
            }
        }
    }
}