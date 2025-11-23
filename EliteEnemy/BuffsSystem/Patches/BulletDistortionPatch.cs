using System;
using System.Reflection;
using EliteEnemies.BuffsSystem.Effects;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.BuffsSystem.Patches
{
    /// <summary>
    /// 子弹扭曲补丁 - 使受扭曲影响的玩家的子弹产生弧形偏转
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "Update")]
    public class BulletDistortionPatch
    {
        private const string LogTag = "[EliteEnemies.BulletDistortion]";
        
        // 反射缓存
        private static FieldInfo _velocityField;
        private static bool _fieldInitialized = false;

        // 偏转强度
        private const float DeflectionStrength = 6f; // 度/帧
        
        [HarmonyPrefix]
        public static void Prefix(Projectile __instance)
        {
            try
            {
                // 检查是否是玩家射出的子弹
                if (__instance.context.fromCharacter == null) return;
                if (__instance.context.fromCharacter != CharacterMainControl.Main) return;
                
                // 检查玩家是否受扭曲影响
                if (!BulletDeflectionTracker.Instance.IsPlayerDistorted(__instance.context.fromCharacter)) 
                    return;

                // 初始化反射字段
                if (!_fieldInitialized)
                {
                    _velocityField = typeof(Projectile).GetField("velocity", 
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    _fieldInitialized = true;
                }

                if (_velocityField == null) return;

                // 获取当前速度
                Vector3 currentVelocity = (Vector3)_velocityField.GetValue(__instance);
                
                // 速度为零则跳过
                if (currentVelocity.magnitude < 0.1f) return;
                
                // 计算偏转后的速度
                Vector3 deflectedVelocity = ApplyCurveDeflection(currentVelocity);
                
                // 设置新速度
                _velocityField.SetValue(__instance, deflectedVelocity);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 补丁执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用弧形偏转
        /// </summary>
        private static Vector3 ApplyCurveDeflection(Vector3 currentVelocity)
        {
            Vector3 perpendicular = Vector3.Cross(currentVelocity, Vector3.up).normalized;
            
            if (perpendicular.magnitude < 0.1f)
            {
                perpendicular = Vector3.Cross(currentVelocity, Vector3.forward).normalized;
            }
            
            float randomDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            
            Quaternion rotation = Quaternion.AngleAxis(
                DeflectionStrength * randomDirection, 
                perpendicular
            );
            
            return rotation * currentVelocity;
        }
    }
}