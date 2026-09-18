using UnityEngine;
using EliteEnemies.Core;
using HarmonyLib;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// 强制精英敌人显示血条
    /// </summary>
    [HarmonyPatch(typeof(Health), nameof(Health.Start))]
    internal static class ForceShowEliteHealthBarPatch
    {
        // 本文件补的是 `Health`（不是 `HealthBar`）——日志前缀原先写成了 `[EliteEnemies.HealthBar]`，
        // 排查时会把两个子系统混在一起，已改正。
        private const string LogTag = "[EliteEnemies.Health]";

        static void Postfix(Health __instance)
        {
            // 隔离异常：这是 `Health.Start` 上的补丁，抛出去会把游戏初始化这个角色的流程带走。
            try
            {
                var cmc = __instance.TryGetCharacter();
                if (cmc == null || cmc.IsMainCharacter) return;

                // 特殊UI黑名单怪物不显示血条
                if (cmc.characterPreset != null && PresetDirectory.IsUIHidden(cmc.characterPreset.name))
                {
                    return;
                }

                // 仅对精英怪强制开启血条
                var marker = cmc.GetComponent<EliteMarker>();
                if (marker != null && !__instance.showHealthBar)
                {
                    __instance.showHealthBar = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 强制血条补丁执行失败（已隔离）: {ex}");
            }
        }
    }
}
