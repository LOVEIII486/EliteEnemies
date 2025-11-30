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
            if (ai != null) return ai;
            return character.GetComponentInParent<AICharacterController>();
        }

        // 存储结构
        private struct PendingModification
        {
            public string FieldName;
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
            public const string ShootDelay = "shootDelay";
            public const string ShootCanMove = "shootCanMove";
            public const string CanDash = "canDash";
            public const string DefaultWeaponOut = "defaultWeaponOut";
            
            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";
            
            public const string ItemSkillChance = "itemSkillChance";
            public const string ItemSkillCoolTime = "itemSkillCoolTime";
        }

        private static readonly HashSet<string> ValidFields = new HashSet<string>
        {
            Fields.ReactionTime, Fields.ShootDelay, Fields.ShootCanMove, Fields.CanDash, Fields.DefaultWeaponOut,
            Fields.PatrolRange, Fields.CombatMoveRange, Fields.ForgetTime,
            Fields.ItemSkillChance, Fields.ItemSkillCoolTime
        };

        public static bool CanModify(string fieldName) => ValidFields.Contains(fieldName);

        // ========== 延迟修改接口  ==========

        public static void ModifyDelayed(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null) return;

            if (!_pendingModifications.ContainsKey(character))
            {
                _pendingModifications[character] = new List<PendingModification>();
            }

            _pendingModifications[character].Add(new PendingModification
            {
                FieldName = fieldName,
                Value = value,
                Multiply = multiply
            });

            if (!_processingCharacters.Contains(character))
            {
                _processingCharacters.Add(character);

                if (character.gameObject.activeInHierarchy)
                {
                    character.StartCoroutine(ApplyPendingModifications(character));
                }
                else
                {
                    if (character.GetComponent<ModificationApplier>() == null)
                    {
                        character.gameObject.AddComponent<ModificationApplier>();
                    }
                }
            }
        }

        // ========== 立即修改接口 ==========

        public static void ModifyImmediate(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            var ai = GetAI(character);
            if (ai != null)
            {
                ApplyModification(ai, fieldName, value, multiply);
            }
        }

        // ========== 核心协程 ==========

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            // 等待帧结束。确保所有 Start/Update 初始化逻辑都跑完了。
            yield return new WaitForEndOfFrame();

            // 如果 AI 控制器还没挂载再等一帧
            if (character != null && GetAI(character) == null)
            {
                yield return new WaitForEndOfFrame();
            }

            if (character == null)
            {
                CleanUp(character);
                yield break;
            }

            var ai = GetAI(character);
            if (ai != null && _pendingModifications.TryGetValue(character, out var list))
            {
                foreach (var mod in list)
                {
                    ApplyModification(ai, mod.FieldName, mod.Value, mod.Multiply);
                }
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

        // ========== 反射逻辑 ==========

        private static void ApplyModification(object target, string fieldName, float value, bool multiply)
        {
            try
            {
                Type type = target.GetType();
                
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

                if (field == null)
                {
                    Debug.LogWarning($"{LogTag} 字段 '{fieldName}' 未在 {type.Name} 中找到");
                    return;
                }

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