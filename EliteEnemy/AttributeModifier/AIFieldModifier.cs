using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// AI 字段修改器
    /// </summary>
    public static class AIFieldModifier
    {
        private const string LogTag = "[EliteEnemies.AIFieldModifier]";
        
        // 辅助组件：用于在对象激活时启动协程
        private class ModificationApplier : MonoBehaviour
        {
            private void Start()
            {
                var character = GetComponent<CharacterMainControl>();
                if (character != null)
                {
                    character.StartCoroutine(ApplyPendingModifications(character));
                }
                Destroy(this);
            }
        }

        internal static AICharacterController GetAI(CharacterMainControl character)
        {
            if (character == null) return null;
            var ai = character.GetComponentInChildren<AICharacterController>(true);
            return ai ?? character.GetComponentInParent<AICharacterController>();
        }

        private struct PendingModification
        {
            public string FieldPath;
            public float Value;
            public bool Multiply;
        }

        private static readonly Dictionary<CharacterMainControl, List<PendingModification>> _pendingModifications 
            = new Dictionary<CharacterMainControl, List<PendingModification>>();

        private static readonly HashSet<CharacterMainControl> _processingCharacters = new HashSet<CharacterMainControl>();

        // ========== AI 字段定义 ==========
        public static class Fields
        {
            public const string ReactionTime = "reactionTime";
            public const string BaseReactionTime = "baseReactionTime";
            public const string ShootDelay = "shootDelay";
            public const string ShootCanMove = "shootCanMove";
            public const string CanDash = "canDash";
            public const string DefaultWeaponOut = "defaultWeaponOut";
            public const string CombatTurnSpeed = "combatTurnSpeed";
            
            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";
            
            public const string ItemSkillChance = "itemSkillChance";
            public const string ItemSkillCoolTime = "itemSkillCoolTime";

            public const string ShootTimeMin = "shootTimeRange.x";
            public const string ShootTimeMax = "shootTimeRange.y";
            public const string ShootSpaceMin = "shootTimeSpaceRange.x";
            public const string ShootSpaceMax = "shootTimeSpaceRange.y";
            public const string DashCDMin = "dashCoolTimeRange.x";
            public const string DashCDMax = "dashCoolTimeRange.y";
            public const string DashCDRange = "dashCoolTimeRange"; 
        }

        private static readonly HashSet<string> ValidFields = new HashSet<string>
        {
            // 原始字段
            Fields.ReactionTime, Fields.BaseReactionTime, Fields.ShootDelay, Fields.ShootCanMove, 
            Fields.CanDash, Fields.DefaultWeaponOut, Fields.PatrolRange, Fields.CombatMoveRange, 
            Fields.ForgetTime, Fields.ItemSkillChance, Fields.ItemSkillCoolTime,
            // 新增路径字段
            Fields.ShootTimeMin, Fields.ShootTimeMax, Fields.ShootSpaceMin, Fields.ShootSpaceMax,
            Fields.DashCDMin, Fields.DashCDMax, Fields.DashCDRange
        };

        public static bool CanModify(string fieldName) => ValidFields.Contains(fieldName);

        // ========== 延迟修改接口 ==========

        public static void ModifyDelayed(CharacterMainControl character, string fieldPath, float value, bool multiply = false)
        {
            if (character == null) return;

            if (!_pendingModifications.ContainsKey(character))
            {
                _pendingModifications[character] = new List<PendingModification>();
            }

            _pendingModifications[character].Add(new PendingModification
            {
                FieldPath = fieldPath,
                Value = value,
                Multiply = multiply
            });

            if (!_processingCharacters.Contains(character))
            {
                _processingCharacters.Add(character);

                if (character.gameObject.activeInHierarchy)
                    character.StartCoroutine(ApplyPendingModifications(character));
                else if (character.GetComponent<ModificationApplier>() == null)
                    character.gameObject.AddComponent<ModificationApplier>();
            }
        }

        // ========== 立即修改接口 ==========

        public static void ModifyImmediate(CharacterMainControl character, string fieldPath, float value, bool multiply = false)
        {
            var ai = GetAI(character);
            if (ai != null) ApplyModification(ai, fieldPath, value, multiply);
        }

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            yield return new WaitForEndOfFrame();
            var ai = GetAI(character);
            if (ai != null && _pendingModifications.TryGetValue(character, out var list))
            {
                foreach (var mod in list)
                    ApplyModification(ai, mod.FieldPath, mod.Value, mod.Multiply);
            }
            if (character != null)
            {
                _pendingModifications.Remove(character);
                _processingCharacters.Remove(character);
            }
        }

        // ========== 核心核心逻辑：处理分量和类型映射 ==========

        private static void ApplyModification(object target, string fieldPath, float value, bool multiply)
        {
            try
            {
                Type type = target.GetType();
                string fieldName = fieldPath;
                int componentIndex = -1; // -1: 原生, 0: x, 1: y

                // 路径解析 (支持 field.x 语法)
                if (fieldPath.Contains("."))
                {
                    string[] parts = fieldPath.Split('.');
                    fieldName = parts[0];
                    componentIndex = parts[1].ToLower() == "x" ? 0 : 1;
                }

                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (field == null) return;

                // 1. 处理 Vector2 及其子组件
                if (field.FieldType == typeof(Vector2))
                {
                    Vector2 v2 = (Vector2)field.GetValue(target);
                    if (componentIndex == 0) v2.x = multiply ? v2.x * value : value;
                    else if (componentIndex == 1) v2.y = multiply ? v2.y * value : value;
                    else v2 = multiply ? v2 * value : new Vector2(value, value);
                    field.SetValue(target, v2);
                }
                // 2. 处理 Float
                else if (field.FieldType == typeof(float))
                {
                    float current = (float)field.GetValue(target);
                    field.SetValue(target, multiply ? current * value : value);
                }
                // 3. 恢复 Int 处理逻辑 (修复旧词条失效的关键)
                else if (field.FieldType == typeof(int))
                {
                    int current = (int)field.GetValue(target);
                    field.SetValue(target, multiply ? Mathf.RoundToInt(current * value) : Mathf.RoundToInt(value));
                }
                // 4. 处理 Bool
                else if (field.FieldType == typeof(bool))
                {
                    field.SetValue(target, value > 0.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 修改 '{fieldPath}' 失败: {ex.Message}");
            }
        }
    }
}