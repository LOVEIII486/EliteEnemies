using System.Collections.Generic;
using Duckov.UI;
using EliteEnemies.Core;
using EliteEnemies.Visuals;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// 血条颜色与外观补丁
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), nameof(HealthBar.Refresh))]
    internal static class EliteHealthBarColorPatch
    {
        private const string LogTag = "[EliteEnemies.HealthBar]";

        // HealthBar.colorOverAmount 是 private，但定义在 TeamSoda.Duckov.Core
        // （Duckov/UI/HealthBar.cs:42），已由 Publicizer 在编译期公开，直接赋值即可。
        // 原先是用 AccessTools.Field 配字符串字段名「colorOverAmount」取字段的——
        // 游戏改名后不会编译失败，只会静默返回 null；配合原先的 `== null` 守卫，
        // 表现为「血条染色功能悄悄失效」，无报错无日志。

        /// <summary>
        /// 精英色（十六进制）→ 纯色 <see cref="Gradient"/> 的缓存。
        ///
        /// <para><b>为什么要缓存</b>：这个 Prefix 每次 <c>HealthBar.Refresh</c> 都跑
        /// （血条创建 + **每次血量变化**，见 <c>HealthBar.cs:197</c> / <c>:291</c> / <c>:296</c>），
        /// 而精英色只有 6 种可能值。原先每次都 <c>new Gradient()</c> + <c>GradientColorKey[2]</c>
        /// + <c>GradientAlphaKey[2]</c>，外加一次 <c>ColorUtility.TryParseHtmlString</c> 字符串解析——
        /// 全是短命垃圾。重火力交火时（每次受伤 × 屏上每个血条）会持续喂 GC。</para>
        ///
        /// <para><b>为什么多个血条可以共享同一份实例</b>：游戏侧对 <c>colorOverAmount</c> 只读——
        /// <c>HealthBar.cs:42</c> 声明，<c>:348</c> 只做 <c>colorOverAmount.Evaluate(num)</c>，
        /// 没有任何地方改它的内容。</para>
        ///
        /// <para><b>为什么不担心生命周期</b>：<c>UnityEngine.Gradient</c> 是普通托管类
        /// （不是 <c>UnityEngine.Object</c>），既不会随场景卸载变成"假空"，也不会被
        /// <c>UnloadUnusedAssets</c> 回收；条目数由颜色数封顶，不需要清理。</para>
        /// </summary>
        private static readonly Dictionary<string, Gradient> GradientCache =
            new Dictionary<string, Gradient>();

        [HarmonyPrefix]
        private static void Prefix(HealthBar __instance)
        {
            // 补丁体一律隔离异常：本 Prefix 每次血条刷新都会跑（血条创建 + 每次血量变化），
            // 抛出去会打断游戏自己的血条刷新流程。try/catch 在"不抛"的路径上零开销。
            try
            {
                if (__instance.target == null) return;

                var cmc = __instance.target.TryGetCharacter();
                if (cmc == null) return;

                // 检查是否有精英标记组件
                var marker = cmc.GetComponent<EliteMarker>();

                // 不是精英（无标记 / 词缀列表为空）⇒ 还原成**这条血条原本的**配色。
                // 血条是池化复用的，精英用过的会被复用给普通敌人，所以这一步不能省；
                // 而"原本的配色"是 EliteHealthBarUI 在 Awake 时存下来的真值——
                // 原先这里写的是一个硬编码纯红，依据是注释里那句"原生红也是非精英血条的颜色"，
                // 那句话**没有出处**（那是 [SerializeField]，真值在预制体里，源码查不到）。
                // 详见 EliteHealthBarUI.RestoreVanillaBarColor。
                if (marker == null || marker.Affixes == null || marker.Affixes.Count == 0)
                {
                    EliteHealthBarUI.RestoreVanillaBarColor(__instance);
                    return;
                }

                // 如果是精英怪，按词缀数量染色
                __instance.colorOverAmount = GetCachedGradient(GetHealthBarColorHex(marker.Affixes.Count));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 染色补丁执行失败（已隔离）: {ex}");
            }
        }

        /// <summary>
        /// 词缀数量 → 血条颜色（**十六进制**，因为缓存的键就是它）。颜色只在这里写一遍。
        ///
        /// <para>调用方保证 <paramref name="count"/> ≥ 1（只有"有词缀"的分支才会走到这里），
        /// 所以最后一个分支同时兜住 `≥ 6` 与理论上的 0/负数——后者到不了；
        /// 真到了说明调用方的前提破了，那时显示深渊黑也是可接受的降级。</para>
        /// </summary>
        private static string GetHealthBarColorHex(int count)
        {
            return count switch
            {
                1 => "#A673FF", // 紫色
                2 => "#FFD700", // 金色
                3 => "#FF10F0", // 霓虹粉
                4 => "#00FFFF", // 青色
                5 => "#8B0000", // 血月色
                _ => "#1A1A1A"  // ≥ 6：深渊黑
            };
        }

        private static Gradient GetCachedGradient(string hex)
        {
            if (GradientCache.TryGetValue(hex, out Gradient cached)) return cached;

            Gradient gradient = CreateSolidGradient(ParseColor(hex));
            GradientCache[hex] = gradient;
            return gradient;
        }

        private static Gradient CreateSolidGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }

    /// <summary>
    /// 词缀显示组件挂载
    /// </summary>
    [HarmonyPatch(typeof(HealthBar), nameof(HealthBar.Awake))]
    internal static class AttachEliteHealthBarPatch
    {
        private const string LogTag = "[EliteEnemies.HealthBar]";

        static void Postfix(HealthBar __instance)
        {
            // 隔离异常：挂不上 UI 不该把游戏创建血条的流程带走（每次都失败也只会各报一行）
            try
            {
                if (__instance.GetComponent<EliteHealthBarUI>() == null)
                {
                    __instance.gameObject.AddComponent<EliteHealthBarUI>();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 挂载精英 UI 失败（已隔离）: {ex}");
            }
        }
    }
}
