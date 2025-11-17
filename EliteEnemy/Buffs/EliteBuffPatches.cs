using System;
using System.Collections.Generic;
using HarmonyLib;
using Duckov.Buffs;
using UnityEngine;
using ItemStatsSystem;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// 精英Buff的Harmony补丁 - 统一入口
    /// </summary>
    [HarmonyPatch(typeof(Buff), "Setup")]
    public class EliteBuffSetupPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Buff __instance, ref List<Effect> ___effects)
        {
            // 检查是否是已注册的精英Buff
            if (!IsEliteBuff(__instance.name)) return;

            // 清空BaseBuff的Effects，避免副作用
            ___effects.Clear();
        }

        [HarmonyPostfix]
        public static void Postfix(Buff __instance, ref string ___displayName, CharacterBuffManager manager)
        {
            // 检查是否是已注册的精英Buff
            string buffName = ExtractBuffName(__instance.name);
            if (buffName == null) return;

            var effect = EliteBuffRegistry.Instance.GetEffect(buffName);
            if (effect == null) return;

            var player = manager?.Master;
            if (player == null)
            {
                Debug.LogWarning($"[EliteEnemies.EliteBuffPatches] 玩家为null (Buff: {buffName})");
                return;
            }

            try
            {
                // 调用效果处理器
                effect.OnBuffSetup(__instance, player);
                
                // 设置显示名称（从本地化获取）
                ___displayName = LocalizationManager.GetText($"Buff_{buffName}_Name", buffName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EliteEnemies.EliteBuffPatches] Buff效果应用失败 ({buffName}): {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 检查是否是精英Buff
        /// </summary>
        private static bool IsEliteBuff(string buffName)
        {
            return buffName != null && buffName.StartsWith("EliteBuff_");
        }

        /// <summary>
        /// 提取Buff名称（去掉Unity的(Clone)后缀）
        /// </summary>
        private static string ExtractBuffName(string fullName)
        {
            if (fullName == null) return null;
    
            // 移除Unity的克隆后缀
            string cleanName = fullName.Replace("(Clone)", "").Trim();
    
            // 检查是否已注册
            return EliteBuffRegistry.Instance.IsRegistered(cleanName) ? cleanName : null;
        }
    }

    /// <summary>
    /// Buff销毁时的清理补丁
    /// </summary>
    [HarmonyPatch(typeof(Buff), "OnDestroy")]
    public class EliteBuffDestroyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Buff __instance)
        {
            // 检查是否是已注册的精英Buff
            string buffName = ExtractBuffName(__instance.name);
            if (buffName == null) return;

            var effect = EliteBuffRegistry.Instance.GetEffect(buffName);
            if (effect == null) return;

            try
            {
                var player = __instance.Character;
   
                effect.OnBuffDestroy(__instance, player);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EliteEnemies.EliteBuffPatches] Buff效果清理失败 ({buffName}): {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string ExtractBuffName(string fullName)
        {
            if (fullName == null) return null;
            string cleanName = fullName.Replace("(Clone)", "").Trim();
            return EliteBuffRegistry.Instance.IsRegistered(cleanName) ? cleanName : null;
        }
    }
}