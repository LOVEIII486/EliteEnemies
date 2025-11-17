using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies
{
    /// <summary>
    /// AI 字段修改器 
    /// 支持生成时延迟修改、战斗中立即修改、临时修改等多种场景
    /// </summary>
    public static class AIFieldModifier
    {
        private const string LogTag = "[EliteEnemies.AIFieldModifier]";

        // 存储待修改的属性（生成时使用）
        private static readonly Dictionary<CharacterMainControl, List<PendingModification>> _pendingModifications 
            = new Dictionary<CharacterMainControl, List<PendingModification>>();

        // 存储原始值（用于恢复）
        private static readonly Dictionary<CharacterMainControl, Dictionary<string, object>> _originalValues
            = new Dictionary<CharacterMainControl, Dictionary<string, object>>();

        // ========== AI 字段列表 ==========
        
        private static readonly HashSet<string> AIFields = new HashSet<string>
        {
            // AI 行为
            "reactionTime", "shootDelay", "shootCanMove", "canDash", "canTalk",
            "defaultWeaponOut",
            
            // AI 感知
            "sightDistance", "sightAngle", "hearingAbility", "forceTracePlayerDistance",
            "nightReactionTimeFactor",
            
            // AI 战斗
            "patrolRange", "combatMoveRange", "forgetTime", 
            "patrolTurnSpeed", "combatTurnSpeed",
            
            // AI 其他
            "itemSkillChance", "itemSkillCoolTime",
        };

        private struct PendingModification
        {
            public string FieldName;
            public float Value;
            public bool Multiply;
        }

        // ========== 基础接口 ==========

        /// <summary>
        /// 检查字段是否可修改
        /// </summary>
        public static bool CanModify(string fieldName)
        {
            return AIFields.Contains(fieldName);
        }

        // ========== 生成时使用（延迟修改）==========

        /// <summary>
        /// 延迟修改（生成时使用，等待 preset 赋值完成）
        /// 适用场景：AICharacterController.Init() Postfix 中
        /// </summary>
        public static void ModifyDelayed(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} Character 为空");
                return;
            }

            if (!CanModify(fieldName))
            {
                Debug.LogWarning($"{LogTag} 字段 '{fieldName}' 不在现有可修改字段表中");
            }

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

            // 启动延迟应用协程
            if (character.gameObject.activeInHierarchy)
            {
                character.StartCoroutine(ApplyPendingModifications(character));
            }
        }

        /// <summary>
        /// 批量延迟修改
        /// </summary>
        public static void ModifyDelayedBatch(CharacterMainControl character, Dictionary<string, float> modifications, bool multiply = false)
        {
            if (character == null || modifications == null) return;

            foreach (var kvp in modifications)
            {
                ModifyDelayed(character, kvp.Key, kvp.Value, multiply);
            }
        }

        // ========== 立即修改 ==========

        /// <summary>
        /// 立即修改（战斗中使用，永久修改）
        /// 适用场景：血量触发、阶段变化等
        /// </summary>
        public static void ModifyImmediate(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} Character 为空");
                return;
            }

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 无法找到 {character.characterPreset.nameKey}");
                return;
            }

            ApplyModification(ai, fieldName, value, multiply);
        }

        /// <summary>
        /// 批量立即修改
        /// </summary>
        public static void ModifyImmediateBatch(CharacterMainControl character, Dictionary<string, float> modifications, bool multiply = false)
        {
            if (character == null || modifications == null) return;

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null) return;

            foreach (var kvp in modifications)
            {
                ApplyModification(ai, kvp.Key, kvp.Value, multiply);
            }
        }

        /// <summary>
        /// 临时修改（保存原始值，可恢复）
        /// 适用场景：临时 Buff、状态效果
        /// </summary>
        /// <returns>是否成功修改</returns>
        public static bool ModifyTemporary(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} Character 为空");
                return false;
            }

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 无法找到");
                return false;
            }

            // 保存原始值
            if (!_originalValues.ContainsKey(character))
            {
                _originalValues[character] = new Dictionary<string, object>();
            }

            // 只在第一次保存原始值
            if (!_originalValues[character].ContainsKey(fieldName))
            {
                var originalValue = GetFieldValue(ai, fieldName);
                if (originalValue != null)
                {
                    _originalValues[character][fieldName] = originalValue;
                }
            }

            // 应用修改
            ApplyModification(ai, fieldName, value, multiply);
            return true;
        }

        /// <summary>
        /// 恢复原始值
        /// </summary>
        public static void RestoreOriginal(CharacterMainControl character, string fieldName)
        {
            if (character == null) return;

            if (!_originalValues.ContainsKey(character) || 
                !_originalValues[character].ContainsKey(fieldName))
            {
                Debug.LogWarning($"{LogTag} 字段 {fieldName} 无初始值！");
                return;
            }

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null) return;

            var originalValue = _originalValues[character][fieldName];
            SetFieldValue(ai, fieldName, originalValue);

            // 清理保存的值
            _originalValues[character].Remove(fieldName);
            if (_originalValues[character].Count == 0)
            {
                _originalValues.Remove(character);
            }
        }

        /// <summary>
        /// 恢复所有原始值
        /// </summary>
        public static void RestoreAll(CharacterMainControl character)
        {
            if (character == null || !_originalValues.ContainsKey(character))
                return;

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null) return;

            foreach (var kvp in _originalValues[character])
            {
                SetFieldValue(ai, kvp.Key, kvp.Value);
            }

            _originalValues.Remove(character);
        }

        // ========== 条件触发修改 ==========

        /// <summary>
        /// 根据条件动态修改（持续监测）
        /// 适用场景：距离触发、血量触发等
        /// </summary>
        public static ConditionalModifier ModifyOnCondition(
            CharacterMainControl character,
            string fieldName,
            System.Func<bool> condition,
            float valueIfTrue,
            float valueIfFalse,
            bool multiply = false)
        {
            if (character == null || condition == null)
            {
                Debug.LogWarning($"{LogTag} Character 或 condition 为空");
                return null;
            }

            var conditionalMod = character.gameObject.AddComponent<ConditionalModifier>();
            conditionalMod.Initialize(character, fieldName, condition, valueIfTrue, valueIfFalse, multiply);
            return conditionalMod;
        }

        // ========== 内部实现 ==========

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            // 等待1帧，确保 CharacterRandomPreset.CreateCharacterAsync 完成
            yield return null;

            if (character == null || !_pendingModifications.ContainsKey(character))
                yield break;

            var ai = character.GetComponent<AICharacterController>();
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 未找到");
                _pendingModifications.Remove(character);
                yield break;
            }

            // 应用所有待修改的属性
            var modifications = _pendingModifications[character];
            foreach (var mod in modifications)
            {
                ApplyModification(ai, mod.FieldName, mod.Value, mod.Multiply);
            }
            
            _pendingModifications.Remove(character);
        }

        private static void ApplyModification(AICharacterController ai, string fieldName, float value, bool multiply)
        {
            try
            {
                Type aiType = ai.GetType();
                FieldInfo field = aiType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null)
                {
                    Debug.LogWarning($"{LogTag} 字段 '{fieldName}' 未在 AICharacterController 中找到");
                    return;
                }

                if (field.FieldType == typeof(bool))
                {
                    // 布尔类型
                    field.SetValue(ai, value > 0.5f);
                }
                else if (field.FieldType == typeof(float))
                {
                    // 浮点类型
                    if (multiply)
                    {
                        float currentValue = (float)field.GetValue(ai);
                        field.SetValue(ai, currentValue * value);
                    }
                    else
                    {
                        field.SetValue(ai, value);
                    }
                }
                else if (field.FieldType == typeof(int))
                {
                    // 整数类型
                    if (multiply)
                    {
                        int currentValue = (int)field.GetValue(ai);
                        field.SetValue(ai, Mathf.RoundToInt(currentValue * value));
                    }
                    else
                    {
                        field.SetValue(ai, Mathf.RoundToInt(value));
                    }
                }
                else
                {
                    Debug.LogWarning($"{LogTag} 不支持的字段类型: {field.FieldType}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 修改字段失败： {fieldName}: {ex.Message}");
            }
        }

        private static object GetFieldValue(AICharacterController ai, string fieldName)
        {
            try
            {
                Type aiType = ai.GetType();
                FieldInfo field = aiType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field?.GetValue(ai);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 未找到： {fieldName}: {ex.Message}");
                return null;
            }
        }

        private static void SetFieldValue(AICharacterController ai, string fieldName, object value)
        {
            try
            {
                Type aiType = ai.GetType();
                FieldInfo field = aiType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                field?.SetValue(ai, value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 设置失败： {fieldName}: {ex.Message}");
            }
        }

        // ========== 生命周期管理 ==========

        /// <summary>
        /// 清理角色的待修改列表和原始值（角色死亡时调用）
        /// </summary>
        public static void Cleanup(CharacterMainControl character)
        {
            if (character == null) return;

            if (_pendingModifications.ContainsKey(character))
            {
                _pendingModifications.Remove(character);
            }

            if (_originalValues.ContainsKey(character))
            {
                _originalValues.Remove(character);
            }
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public static void ClearAll()
        {
            _pendingModifications.Clear();
            _originalValues.Clear();
        }

        // ========== 可用字段整理 ==========

        public static class Fields
        {
            // AI 行为
            public const string ReactionTime = "reactionTime";
            public const string ShootDelay = "shootDelay";
            public const string ShootCanMove = "shootCanMove";
            public const string CanDash = "canDash";
            public const string CanTalk = "canTalk";
            public const string DefaultWeaponOut = "defaultWeaponOut";
            
            // AI 感知
            public const string SightDistance = "sightDistance";
            public const string SightAngle = "sightAngle";
            public const string HearingAbility = "hearingAbility";
            public const string ForceTracePlayerDistance = "forceTracePlayerDistance";
            public const string NightReactionTimeFactor = "nightReactionTimeFactor";
            
            // AI 战斗
            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";
            public const string PatrolTurnSpeed = "patrolTurnSpeed";
            public const string CombatTurnSpeed = "combatTurnSpeed";
            
            // AI 其他
            public const string ItemSkillChance = "itemSkillChance";
            public const string ItemSkillCoolTime = "itemSkillCoolTime";
        }
    }

    /// <summary>
    /// 条件触发修改组件（挂载到角色上，持续监测）
    /// </summary>
    public class ConditionalModifier : MonoBehaviour
    {
        private CharacterMainControl _character;
        private AICharacterController _ai;
        private string _fieldName;
        private System.Func<bool> _condition;
        private float _valueIfTrue;
        private float _valueIfFalse;
        private bool _multiply;
        private bool _lastState;

        public void Initialize(
            CharacterMainControl character,
            string fieldName,
            System.Func<bool> condition,
            float valueIfTrue,
            float valueIfFalse,
            bool multiply)
        {
            _character = character;
            _ai = character.GetComponent<AICharacterController>();
            _fieldName = fieldName;
            _condition = condition;
            _valueIfTrue = valueIfTrue;
            _valueIfFalse = valueIfFalse;
            _multiply = multiply;
            _lastState = false;

            // 立即检查一次
            CheckAndApply();
        }

        private void Update()
        {
            if (_character == null || _ai == null || _condition == null)
            {
                Destroy(this);
                return;
            }

            CheckAndApply();
        }

        private void CheckAndApply()
        {
            bool currentState = _condition();
            if (currentState != _lastState)
            {
                float value = currentState ? _valueIfTrue : _valueIfFalse;
                AIFieldModifier.ModifyImmediate(_character, _fieldName, value, _multiply);
                _lastState = currentState;
            }
        }

        private void OnDestroy()
        {
            _condition = null;
        }
    }
}
