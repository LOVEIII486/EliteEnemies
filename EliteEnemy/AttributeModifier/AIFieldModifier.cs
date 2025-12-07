using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    public static class AIFieldModifier
    {
        private const string LogTag = "[EliteEnemies.AIFieldModifier]";
        
        private class ModificationApplier : MonoBehaviour
        {
            private void Start()
            {
                var character = GetComponent<CharacterMainControl>();
                if (character != null) character.StartCoroutine(ApplyPendingModifications(character));
                Destroy(this);
            }
        }

        internal static AICharacterController GetAI(CharacterMainControl character)
        {
            if (character == null) return null;
            var ai = character.GetComponentInChildren<AICharacterController>(true);
            if (ai != null) return ai;
            return character.GetComponentInParent<AICharacterController>();
        }

        private struct PendingModification
        {
            public string FieldName;
            public float Value;
            public bool Multiply;
        }

        private static readonly Dictionary<CharacterMainControl, List<PendingModification>> _pendingModifications = new();
        private static readonly HashSet<CharacterMainControl> _processingCharacters = new();

        // ========== AI 字段定义 ==========
        public static class Fields
        {
            public const string ReactionTime = "reactionTime";
            public const string ShootDelay = "shootDelay";
            public const string ShootCanMove = "shootCanMove";
            public const string CanDash = "canDash";
            public const string DefaultWeaponOut = "defaultWeaponOut";
            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";
            public const string ItemSkillChance = "itemSkillChance";
            public const string ItemSkillCoolTime = "itemSkillCoolTime";

            // 使用虚拟字段名来映射 Vector2 的分量
            public const string ShootTimeMin = "shootTimeRange_min";
            public const string ShootTimeMax = "shootTimeRange_max";
        }

        private static readonly HashSet<string> ValidFields = new HashSet<string>
        {
            Fields.ReactionTime, Fields.ShootDelay, Fields.ShootCanMove, Fields.CanDash, Fields.DefaultWeaponOut,
            Fields.PatrolRange, Fields.CombatMoveRange, Fields.ForgetTime,
            Fields.ItemSkillChance, Fields.ItemSkillCoolTime,
            Fields.ShootTimeMin, Fields.ShootTimeMax
        };

        public static bool CanModify(string fieldName) => ValidFields.Contains(fieldName);

        // ========== 接口 ==========

        public static void ModifyDelayed(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null) return;
            if (!_pendingModifications.ContainsKey(character)) _pendingModifications[character] = new List<PendingModification>();

            _pendingModifications[character].Add(new PendingModification { FieldName = fieldName, Value = value, Multiply = multiply });

            if (!_processingCharacters.Contains(character))
            {
                _processingCharacters.Add(character);
                if (character.gameObject.activeInHierarchy) character.StartCoroutine(ApplyPendingModifications(character));
                else if (character.GetComponent<ModificationApplier>() == null) character.gameObject.AddComponent<ModificationApplier>();
            }
        }

        public static void ModifyImmediate(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            var ai = GetAI(character);
            if (ai != null) ApplyModification(ai, fieldName, value, multiply);
        }

        // ========== 核心逻辑 ==========

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            yield return new WaitForEndOfFrame();
            if (character != null && GetAI(character) == null) yield return new WaitForEndOfFrame();

            if (character == null)
            {
                CleanUp(character);
                yield break;
            }

            var ai = GetAI(character);
            if (ai != null && _pendingModifications.TryGetValue(character, out var list))
            {
                foreach (var mod in list) ApplyModification(ai, mod.FieldName, mod.Value, mod.Multiply);
            }
            CleanUp(character);
        }

        private static void CleanUp(CharacterMainControl character)
        {
            if (character != null)
            {
                _pendingModifications.Remove(character);
                _processingCharacters.Remove(character);
            }
        }

        private static void ApplyModification(object target, string fieldName, float value, bool multiply)
        {
            try
            {
                Type type = target.GetType();

                // 处理 Vector2 类型的特殊字段映射
                if (fieldName == Fields.ShootTimeMin || fieldName == Fields.ShootTimeMax)
                {
                    FieldInfo vField = type.GetField("shootTimeRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (vField != null && vField.FieldType == typeof(Vector2))
                    {
                        Vector2 currentVec = (Vector2)vField.GetValue(target);
                        float currentVal = (fieldName == Fields.ShootTimeMin) ? currentVec.x : currentVec.y;
                        float newVal = multiply ? currentVal * value : value;

                        if (fieldName == Fields.ShootTimeMin) currentVec.x = newVal;
                        else currentVec.y = newVal;

                        vField.SetValue(target, currentVec);
                        return;
                    }
                }

                // 标准反射处理
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (field == null) return;

                if (field.FieldType == typeof(bool))
                {
                    field.SetValue(target, value > 0.5f);
                }
                else if (field.FieldType == typeof(float))
                {
                    if (multiply)
                    {
                        float current = (float)field.GetValue(target);
                        field.SetValue(target, current * value);
                    }
                    else
                    {
                        field.SetValue(target, value);
                    }
                }
                else if (field.FieldType == typeof(int))
                {
                    if (multiply)
                    {
                        int current = (int)field.GetValue(target);
                        field.SetValue(target, Mathf.RoundToInt(current * value));
                    }
                    else
                    {
                        field.SetValue(target, Mathf.RoundToInt(value));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 修改 {fieldName} 异常: {ex.Message}");
            }
        }
    }
}