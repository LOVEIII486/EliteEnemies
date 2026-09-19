using System;
using System.Collections.Generic;
using System.Linq;
using EliteEnemies.Affixes;
using EliteEnemies.Combos;
using EliteEnemies.Settings;
using SodaCraft.Localizations;
using UnityEngine;
using EliteEnemies.Modifiers;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 精英敌人核心系统 
    /// </summary>
    public static class EliteEnemyCore
    {
        private const string LogTag = "[EliteEnemies.Core]";

        private static EliteEnemiesConfig _config = new EliteEnemiesConfig();
        public static EliteEnemiesConfig Config => _config;

        /// <summary>
        /// **本机是否拥有「精英逻辑」的权威**——精英判定与精英掉落都归它管。
        ///
        /// <para>默认 <c>true</c>（单机与联机主机都是如此）。<b>联机客户端会被置为 <c>false</c></b>，
        /// 因为在那套模型里精英由主机判定、结果经网络下发；客户端自己再判一次就会
        /// <b>两端各自随机、结果不一致</b>（实测已确认，见
        /// <c>docs\联机兼容可行性分析.md</c> §2.1）。</para>
        ///
        /// <para><b>为什么是一个字段而不是直接引用联机模块</b>：遵守
        /// <c>docs\联机兼容可行性分析.md</c> §5.5 的单向依赖纪律——
        /// <c>Core</c> 不认识 <c>EliteEnemies.Coop</c>，由后者在激活/停机时<b>设置</b>这里。
        /// 方向是 Coop → Core，不是反过来。</para>
        ///
        /// <para>⚠ <b>客户端仍然会给复制体挂 <see cref="EliteMarker"/></b>——那是<b>显示</b>用的
        /// （血条标签与配色读它）。「挂标记」与「拥有权威」是两件事，不要合并。</para>
        /// </summary>
        /// <summary>
        /// 覆盖「谁是权威」的判定。**默认 <c>null</c> ⇒ 本机就是权威**（单机、联机主机）。
        /// 由联机模块在激活时设成一个"现查当前角色"的委托。
        ///
        /// <para>⚠ <b>刻意用委托而不是缓存成 bool</b>：主机/客户端身份在一次会话里
        /// <b>可能变化</b>（联机模组的 <c>StartNetwork</c>/<c>StopNetwork</c> 会改 <c>IsServer</c>），
        /// 缓存下来就会过期，而过期表现为"某一端悄悄按错的权威跑了"——正是本项目最忌的静默失效。</para>
        /// </summary>
        public static Func<bool> EliteAuthorityOverride { get; set; }

        /// <summary>本机是否为精英逻辑的权威。读法见 <see cref="EliteAuthorityOverride"/>。</summary>
        public static bool IsEliteAuthority
        {
            get
            {
                var provider = EliteAuthorityOverride;
                return provider == null || provider();
            }
        }
        
        // 由生成器创建的临时预设（EggSpawnHelper 的 CreateModifiedPreset 用 Instantiate 造）
        // 的实例 ID——这些预设对应的敌人不应精英化。
        //
        // ⚠ 为什么存 ID 而不是对象引用：存引用会让这个集合**把每个临时预设都钉在内存里**，
        //   永不回收。存 ID 则只占 4 字节，且不延长任何对象的寿命。
        //
        // ⚠ 但它**必须**在场景边界清空，见 ClearIgnoredPresets 的注释——否则陈旧 ID 会被复用。
        private static readonly HashSet<int> IgnoredPresetInstanceIDs = new HashSet<int>();

        /// <summary>
        /// 注册一个预设实例为“忽略精英化”
        /// 用于 EggSpawnHelper 生成的临时预设
        /// </summary>
        public static void RegisterIgnoredPreset(ScriptableObject preset)
        {
            if (preset == null) return;
            IgnoredPresetInstanceIDs.Add(preset.GetInstanceID());
        }

        /// <summary>
        /// 撤销一次「忽略精英化」登记。**必须与副本的销毁成对调用，且必须在销毁之前**
        /// （<c>GetInstanceID()</c> 对已销毁对象会抛）。
        ///
        /// <para><b>为什么必须成对</b>：这个集合按实例 ID 记账，而 ID 在对象被回收后可能
        /// 被复用（见 <see cref="ClearIgnoredPresets"/> 的注释）。副本若在关卡中途被销毁
        /// 而登记还留着，那个复用的 ID 就可能命中一个毫不相干的新预设，让那个敌人
        /// **静默地不被精英化**——正是本工程最忌讳的那类失效。</para>
        ///
        /// <para>调用点：<c>EggSpawnHelper.ReleasePreset</c>（副本被销毁的唯一出口）。</para>
        /// </summary>
        public static void UnregisterIgnoredPreset(ScriptableObject preset)
        {
            if (preset == null) return;
            IgnoredPresetInstanceIDs.Remove(preset.GetInstanceID());
        }

        /// <summary>
        /// 检查预设是否在忽略名单中
        /// </summary>
        public static bool IsIgnoredPreset(ScriptableObject preset)
        {
            if (preset == null) return false;
            return IgnoredPresetInstanceIDs.Contains(preset.GetInstanceID());
        }

        /// <summary>
        /// 清空「忽略精英化」的临时预设名单。**必须在场景卸载时调用。**
        ///
        /// <para>理由是一条 Unity 的时序事实：<c>Resources.UnloadUnusedAssets</c> 会在
        /// **非叠加式加载场景时被自动调用**，回收掉不再被引用的 ScriptableObject ——
        /// 而 <c>EggSpawnHelper.CreateModifiedPreset</c> 造的临时预设恰好就是这类
        /// （<c>Instantiate</c> 出来、生成完就没人再引用、**从不显式销毁**）。
        /// 它们被回收后，<c>GetInstanceID()</c> 占用的号会被释放并可能分给新对象。</para>
        ///
        /// <para>于是这个集合里那些陈旧条目就有两种坏处：既永远清不掉（只增不减），
        /// 又可能**匹配上一个毫不相干的新预设**，让那个敌人静默地不被精英化。
        /// 在场景边界清空即可同时消掉两者——那时该场景的敌人都已销毁，
        /// 不存在"还在飞行中"的生成操作。</para>
        ///
        /// <para>调用点：<c>ModBehaviour.OnSceneUnloaded</c>（与 <c>EliteLootSystem</c>
        /// 的缓存清理放在一起，它们清的是同一类跨场景残留状态）。</para>
        /// </summary>
        public static void ClearIgnoredPresets()
        {
            IgnoredPresetInstanceIDs.Clear();
        }

        // ========== 公共接口 ==========

        public static void UpdateConfig(EliteEnemiesConfig newConfig)
        {
            if (newConfig == null)
            {
                Debug.LogError($"{LogTag} 配置更新失败: 配置为空");
                return;
            }

            _config = newConfig;
        }
        
        /// <summary>
        /// 强制将敌人变为精英
        /// </summary>
        /// <summary>
        /// 精英基础属性加成的**来源标识**。写入（<see cref="ForceMakeElite"/>）与撤销
        /// （<see cref="StripElite"/>）必须用同一个字符串，所以提成常量——两处各写一份必然漂移。
        /// </summary>
        private const string EliteStatSource = "EliteBaseStats";

        /// <summary>
        /// 兜底：确认这个角色**没有**被精英化；若有，报错并就地撤销。
        ///
        /// <para><b>为什么需要它</b>：「召唤物不能带词条」这条规则的主防护是
        /// **生成前把副本预设登记为忽略**（<c>EggSpawnHelper.RegisterIgnoredPreset</c>）。
        /// 它有效是因为它在 <c>CreateCharacterAsync</c> **之前**执行，赶得上
        /// <c>EliteSpawnPatch</c> 在 <c>AICharacterController.Init</c> 里的那次判定。</para>
        ///
        /// <para>但那条链**只有一个支点，且失效时无声**：谁把某个调用改成
        /// <c>preventElite: false</c>，召唤物就会静默地带上词条——不报错、日志里也看不出。
        /// 所以生成完成后在这里再验一次，把静默失效变成**响的**。</para>
        /// </summary>
        public static void EnsureNotElite(CharacterMainControl cmc)
        {
            if (!cmc) return;

            bool hasMarker = cmc.GetComponent<EliteMarker>() != null;
            bool hasBehaviors = cmc.GetComponent<EliteBehaviorComponent>() != null;
            if (!hasMarker && !hasBehaviors) return;

            Debug.LogError($"{LogTag} 本应被忽略的角色却被精英化了（{cmc.name}）——" +
                           "「生成前登记忽略预设」那道防护失效了，已就地撤销。");
            StripElite(cmc);
        }

        /// <summary>
        /// 撤销一次精英化：属性、行为组件、标记三样都要摘。
        ///
        /// <para>顺序有讲究：<c>EliteBehaviorComponent.OnDestroy</c> 会逐个调
        /// <c>behavior.OnCleanup</c>（各词条由此撤销自己的 Stat/AI 修改）并退订事件，
        /// 所以直接 <c>Destroy</c> 它即可——**不要**自己手写撤销，那会和行为类里的清理重复。</para>
        /// </summary>
        public static void StripElite(CharacterMainControl cmc)
        {
            if (!cmc) return;

            // 基础属性加成走来源撤销（各词条的修改由下面 Destroy 行为组件时撤销）
            CharacterModifiers.ClearAll(cmc, EliteStatSource);

            var behaviors = cmc.GetComponent<EliteBehaviorComponent>();
            if (behaviors != null) UnityEngine.Object.Destroy(behaviors);

            var marker = cmc.GetComponent<EliteMarker>();
            if (marker != null) UnityEngine.Object.Destroy(marker);
        }

        public static void ForceMakeElite(CharacterMainControl cmc, IReadOnlyList<string> affixes)
        {
            if (!cmc) return;

            // 1. 计算所有词缀带来的总属性倍率 (hp, dmg, spd 均为最终倍率，如 2.0)
            AccumulateFromAffixes(affixes, out float hp, out float dmg, out float spd);
            
            // 2. 应用基础属性加成
            CharacterModifiers.Quick.ApplyElitePowerup(cmc, hp, dmg, spd, EliteStatSource);

            // 3. 执行精英化标记逻辑
            TagAsElite(cmc, new List<string>(affixes), ResolveBaseName(cmc));
        }

        // ========== 倍率计算 ==========

        internal static void AccumulateFromAffixes(IReadOnlyList<string> affixes, out float hpMult, out float dmgMult,
            out float spdMult)
        {
            hpMult = 1f;
            dmgMult = 1f;
            spdMult = 1f;

            if (affixes == null) return;

            foreach (string name in affixes)
            {
                if (EliteAffixes.TryGetAffix(name, out var affix))
                {
                    hpMult *= affix.HealthMultiplier;
                    dmgMult *= affix.DamageMultiplier;
                    spdMult *= affix.MoveSpeedMultiplier;
                }
            }

            hpMult *= Config.GlobalHealthMultiplier;
            dmgMult *= Config.GlobalDamageMultiplier;
            spdMult *= Config.GlobalSpeedMultiplier;
        }

        // ========== 精英标记与命名 ==========

        public static void TagAsElite(CharacterMainControl cmc, List<string> affixes, string baseName)
        {
            if (!cmc) return;

            var marker = cmc.GetComponent<EliteMarker>();
            if (!marker) marker = cmc.gameObject.AddComponent<EliteMarker>();

            marker.BaseName = baseName;
            marker.Affixes = affixes ?? new List<string>();
        }

        internal static string ResolveBaseName(CharacterMainControl cmc)
        {
            var preset = cmc.characterPreset;
            if (preset != null && !string.IsNullOrEmpty(preset.nameKey))
            {
                string localized = preset.nameKey.ToPlainText();
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            string rawName = cmc.name ?? "未知敌人";
            rawName = rawName.Replace("(Clone)", "").Trim();

            if (string.Equals(rawName, "Character", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(rawName))
                rawName = "未知敌人";

            return rawName;
        }

        internal static string BuildColoredPrefix(IReadOnlyList<string> affixes)
        {
            if (affixes == null || affixes.Count == 0) return "*";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string affixKey in affixes)
            {
                sb.Append(EliteAffixes.TryGetAffix(affixKey, out var affix)
                    ? affix.ColoredTag
                    : $"[{affixKey}]");
            }

            return sb.ToString();
        }
        
        public static string GetEliteFullDisplayName(CharacterMainControl cmc)
        {
            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null) return ResolveBaseName(cmc);

            // 如果是 Combo 怪，优先显示 CustomDisplayName
            if (!string.IsNullOrEmpty(marker.CustomDisplayName))
            {
                return $"{marker.CustomDisplayName} {marker.BaseName}";
            }

            // 普通精英怪：[词缀标签] 名字
            string prefix = BuildColoredPrefix(marker.Affixes);
            return $"{prefix} {marker.BaseName}";
        }
    }
}