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

        // 反射字段缓存
        private static readonly Dictionary<string, FieldInfo> _fieldCache = new Dictionary<string, FieldInfo>();

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
            if (character.aiCharacterController != null) return character.aiCharacterController;
            // 通过层级搜索
            return character.GetComponentInChildren<AICharacterController>(true);
        }

        private struct PendingModification
        {
            public string FieldPath;
            public float Value;
            public bool Multiply;
        }

        private static readonly Dictionary<CharacterMainControl, List<PendingModification>> _pendingModifications
            = new Dictionary<CharacterMainControl, List<PendingModification>>();

        private static readonly HashSet<CharacterMainControl> _processingCharacters =
            new HashSet<CharacterMainControl>();

        public static class Fields
        {
            public const string ReactionTime = "reactionTime";
            public const string BaseReactionTime = "baseReactionTime";
            public const string UpdateValueTimer = "updateValueTimer";

            public const string PatrolTurnSpeed = "patrolTurnSpeed";
            public const string CombatTurnSpeed = "combatTurnSpeed";

            public const string ShootDelay = "shootDelay";
            public const string ShootCanMove = "shootCanMove";

            public const string ShootTimeRange = "shootTimeRange";
            public const string ShootTimeMin = "shootTimeRange.x";
            public const string ShootTimeMax = "shootTimeRange.y";
            public const string ShootSpaceRange = "shootTimeSpaceRange";
            public const string ShootSpaceMin = "shootTimeSpaceRange.x";
            public const string ShootSpaceMax = "shootTimeSpaceRange.y";

            public const string CanDash = "canDash";
            public const string DashCDRange = "dashCoolTimeRange";
            public const string DashCDMin = "dashCoolTimeRange.x";
            public const string DashCDMax = "dashCoolTimeRange.y";

            public const string SightDistance = "sightDistance";
            public const string SightAngle = "sightAngle";
            public const string HearingAbility = "hearingAbility";

            public const string PatrolRange = "patrolRange";
            public const string CombatMoveRange = "combatMoveRange";
            public const string ForgetTime = "forgetTime";

            public const string HasSkill = "hasSkill";
            public const string SkillChance = "skillSuccessChance";
            public const string SkillCoolTimeRange = "skillCoolTimeRange";
            public const string SkillCoolTimeMin = "skillCoolTimeRange.x";
            public const string SkillCoolTimeMax = "skillCoolTimeRange.y";

            public const string DefaultWeaponOut = "defaultWeaponOut";
        }

        public static void ModifyDelayed(CharacterMainControl character, string fieldPath, float value,
            bool multiply = false)
        {
            if (character == null) return;

            if (!_pendingModifications.ContainsKey(character))
            {
                _pendingModifications[character] = new List<PendingModification>();
            }

            _pendingModifications[character].Add(new PendingModification
                { FieldPath = fieldPath, Value = value, Multiply = multiply });

            if (!_processingCharacters.Contains(character))
            {
                _processingCharacters.Add(character);
                if (character.gameObject.activeInHierarchy)
                    character.StartCoroutine(ApplyPendingModifications(character));
                else if (character.GetComponent<ModificationApplier>() == null)
                    character.gameObject.AddComponent<ModificationApplier>();
            }
        }

        private static IEnumerator ApplyPendingModifications(CharacterMainControl character)
        {
            yield return new WaitForEndOfFrame();

            var ai = GetAI(character);
            if (ai != null && _pendingModifications.TryGetValue(character, out var list))
            {
                foreach (var mod in list)
                {
                    ApplyInternal(ai, mod.FieldPath, mod.Value, mod.Multiply);
                }
            }

            if (character != null)
            {
                _pendingModifications.Remove(character);
                _processingCharacters.Remove(character);
            }
        }

        private static void ApplyInternal(AICharacterController ai, string fieldPath, float value, bool multiply)
        {
            try
            {
                string fieldName = fieldPath;
                int componentIndex = -1;

                if (fieldPath.Contains("."))
                {
                    string[] parts = fieldPath.Split('.');
                    fieldName = parts[0];
                    componentIndex = parts[1].ToLower() == "x" ? 0 : 1;
                }

                if (!_fieldCache.TryGetValue(fieldName, out FieldInfo fInfo))
                {
                    fInfo = typeof(AICharacterController).GetField(fieldName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _fieldCache[fieldName] = fInfo;
                }

                if (fInfo == null) return;

                if (fInfo.FieldType == typeof(Vector2))
                {
                    Vector2 v = (Vector2)fInfo.GetValue(ai);
                    if (componentIndex == 0) v.x = multiply ? v.x * value : value;
                    else if (componentIndex == 1) v.y = multiply ? v.y * value : value;
                    else v = multiply ? v * value : new Vector2(value, value);
                    fInfo.SetValue(ai, v);
                }
                else if (fInfo.FieldType == typeof(float))
                {
                    float current = (float)fInfo.GetValue(ai);
                    fInfo.SetValue(ai, multiply ? current * value : value);
                }
                else if (fInfo.FieldType == typeof(bool))
                {
                    fInfo.SetValue(ai, value > 0.5f);
                }
                else if (fInfo.FieldType == typeof(int))
                {
                    int current = (int)fInfo.GetValue(ai);
                    fInfo.SetValue(ai, multiply ? Mathf.RoundToInt(current * value) : Mathf.RoundToInt(value));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 反射修改字段 '{fieldPath}' 失败: {ex.Message}");
            }
        }
    }
}