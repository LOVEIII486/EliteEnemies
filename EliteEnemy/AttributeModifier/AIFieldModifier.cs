using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// AI 字段修改器 
    /// 支持生成时延迟修改、战斗中立即修改、临时修改等多种场景
    /// </summary>
    public static class AIFieldModifier
    {
        /// <summary>
        /// 辅助组件：用于在对象激活时启动协程
        /// </summary>
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
        private const string LogTag = "[EliteEnemies.AIFieldModifier]";
        
        /// <summary>
        /// Duckov 的 AI 不与 Character 在同一个物体，因此必须统一查找
        /// </summary>
        internal static AICharacterController GetAI(CharacterMainControl character)
        {
            if (character == null) return null;

            // 1. 子物体（兼容某些 prefab）
            var ai = character.GetComponentInChildren<AICharacterController>(true);
            if (ai != null) return ai;

            // 2. 父物体（Duckov 默认结构）
            ai = character.GetComponentInParent<AICharacterController>();
            if (ai != null) return ai;

            return null;
        }

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

        public static bool CanModify(string fieldName)
        {
            return AIFields.Contains(fieldName);
        }

        // ========== 生成时使用（延迟修改）==========
        
        private static readonly HashSet<CharacterMainControl> _processingCharacters = new HashSet<CharacterMainControl>();
        
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

            // 🔥 修复:只在没有协程运行时才启动新协程
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

        public static void ModifyDelayedBatch(CharacterMainControl character, Dictionary<string, float> modifications, bool multiply = false)
        {
            if (character == null || modifications == null) return;

            foreach (var kvp in modifications)
            {
                ModifyDelayed(character, kvp.Key, kvp.Value, multiply);
            }
        }

        // ========== 立即修改 ==========

        public static void ModifyImmediate(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} Character 为空");
                return;
            }

            var ai = GetAI(character);
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 无法找到 {character.characterPreset?.nameKey}");
                return;
            }

            ApplyModification(ai, fieldName, value, multiply);
        }

        public static void ModifyImmediateBatch(CharacterMainControl character, Dictionary<string, float> modifications, bool multiply = false)
        {
            if (character == null || modifications == null) return;

            var ai = GetAI(character);
            if (ai == null) return;

            foreach (var kvp in modifications)
            {
                ApplyModification(ai, kvp.Key, kvp.Value, multiply);
            }
        }

        // ========== 临时修改 ==========

        public static bool ModifyTemporary(CharacterMainControl character, string fieldName, float value, bool multiply = false)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} Character 为空");
                return false;
            }

            var ai = GetAI(character);
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 无法找到");
                return false;
            }

            if (!_originalValues.ContainsKey(character))
            {
                _originalValues[character] = new Dictionary<string, object>();
            }

            if (!_originalValues[character].ContainsKey(fieldName))
            {
                var originalValue = GetFieldValue(ai, fieldName);
                if (originalValue != null)
                {
                    _originalValues[character][fieldName] = originalValue;
                }
            }

            ApplyModification(ai, fieldName, value, multiply);
            return true;
        }

        public static void RestoreOriginal(CharacterMainControl character, string fieldName)
        {
            if (character == null) return;

            if (!_originalValues.ContainsKey(character) ||
                !_originalValues[character].ContainsKey(fieldName))
            {
                Debug.LogWarning($"{LogTag} 字段 {fieldName} 无初始值！");
                return;
            }

            var ai = GetAI(character);
            if (ai == null) return;

            var originalValue = _originalValues[character][fieldName];
            SetFieldValue(ai, fieldName, originalValue);

            _originalValues[character].Remove(fieldName);
            if (_originalValues[character].Count == 0)
            {
                _originalValues.Remove(character);
            }
        }

        public static void RestoreAll(CharacterMainControl character)
        {
            if (character == null || !_originalValues.ContainsKey(character))
                return;

            var ai = GetAI(character);
            if (ai == null) return;

            foreach (var kvp in _originalValues[character])
            {
                SetFieldValue(ai, kvp.Key, kvp.Value);
            }

            _originalValues.Remove(character);
        }

        // ========== 条件触发修改 ==========

        public static ConditionalModifier ModifyOnCondition(
            CharacterMainControl character,
            string fieldName,
            Func<bool> condition,
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

        // ========== 延迟修改处理 ==========

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            yield return new WaitForEndOfFrame();

            if (character == null || !_pendingModifications.ContainsKey(character))
            {
                _processingCharacters.Remove(character);
                yield break;
            }

            var ai = GetAI(character);
            if (ai == null)
            {
                Debug.LogWarning($"{LogTag} AICharacterController 未找到");
                _pendingModifications.Remove(character);
                _processingCharacters.Remove(character);
                yield break;
            }

            var modifications = _pendingModifications[character];
            foreach (var mod in modifications)
            {
                // Debug.Log($"{LogTag} ApplyModification {character.characterPreset.nameKey} {character.GetHashCode()} ");
                ApplyModification(ai, mod.FieldName, mod.Value, mod.Multiply);
            }

            _pendingModifications.Remove(character);
            _processingCharacters.Remove(character);  // 🔥 记得清理
        }

        // ========== 内部实现：字段反射修改 ==========

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
                    field.SetValue(ai, value > 0.5f);
                }
                else if (field.FieldType == typeof(float))
                {
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
                //Debug.LogWarning($"{LogTag} 修改字段成功: {fieldName} | {value} | {multiply}");
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

        public static void Cleanup(CharacterMainControl character)
        {
            if (character == null) return;

            if (_pendingModifications.ContainsKey(character))
                _pendingModifications.Remove(character);

            if (_originalValues.ContainsKey(character))
                _originalValues.Remove(character);
            
            _processingCharacters.Remove(character);
        }

        public static void ClearAll()
        {
            _pendingModifications.Clear();
            _originalValues.Clear();
            _processingCharacters.Clear();
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
        private Func<bool> _condition;
        private float _valueIfTrue;
        private float _valueIfFalse;
        private bool _multiply;
        private bool _lastState;

        public void Initialize(
            CharacterMainControl character,
            string fieldName,
            Func<bool> condition,
            float valueIfTrue,
            float valueIfFalse,
            bool multiply)
        {
            _character = character;
            _ai = AIFieldModifier.GetAI(character);   // 🔥修改：统一使用 GetAI()
            _fieldName = fieldName;
            _condition = condition;
            _valueIfTrue = valueIfTrue;
            _valueIfFalse = valueIfFalse;
            _multiply = multiply;
            _lastState = false;

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
