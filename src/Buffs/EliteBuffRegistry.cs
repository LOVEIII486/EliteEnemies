using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// 精英 Buff 注册表：**扫描程序集 → 为每个 <see cref="EliteBuffBase"/> 子类建一个模板 → 自检**。
    ///
    /// <para><b>它不是运行期的派发表。</b>派发按**类型**走虚方法（Buff 实例本身就是子类），
    /// 所以每帧、每个实例都不经过这里。它只在注册时被用到，以及被
    /// <see cref="EliteBuffs.Apply{T}"/> 用来取一次模板。</para>
    ///
    /// <para><b>为什么按 owner 记账</b>：本模组只有一个注册者，但现在留下这层结构的成本是
    /// 「注册期一个字符串键、运行期零开销」，而不留的话，将来把本模块搬进前置库、
    /// 或真出现第二个注册者时，要改的是每一处调用。与 <c>..\Docs\agent\04-架构模式.md</c>
    /// 的「注册表 + owner」是同一套。</para>
    ///
    /// <para>⚠ <b>不假装能解决跨模组的 ID 冲突</b>：游戏的 Buff 身份就是那个数字 ID
    /// （<c>CharacterBuffManager.AddBuff</c> 按 ID 查已有实例），而没有任何机制能让两个模组
    /// 协商号段。我们能做的是"自己不出错 + 撞了就报"——见 <see cref="Validate"/>。</para>
    /// </summary>
    public static class EliteBuffRegistry
    {
        private const string LogTag = "[EliteEnemies.BuffRegistry]";

        /// <summary>本模组占用的 Buff 数字 ID 号段（含两端）。原生 Buff 在四位数量级，避开它们。</summary>
        public const int IdRangeMin = 99900;
        public const int IdRangeMax = 99999;

        private static readonly Dictionary<string, List<EliteBuffBase>> ByOwner =
            new Dictionary<string, List<EliteBuffBase>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, EliteBuffBase> ByName =
            new Dictionary<string, EliteBuffBase>(StringComparer.Ordinal);

        private static readonly Dictionary<Type, EliteBuffBase> ByType =
            new Dictionary<Type, EliteBuffBase>();

        /// <summary>当前已登记的 Buff 数（供启动日志与调试用）。</summary>
        public static int Count => ByName.Count;

        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 扫描 <paramref name="assembly"/> 里所有 <see cref="EliteBuffBase"/> 子类并登记。
        /// 对同一个 owner 重复调用是安全的（第二次直接返回）。
        /// </summary>
        public static void RegisterAll(string owner, Assembly assembly)
        {
            if (string.IsNullOrEmpty(owner) || assembly == null) return;

            if (ByOwner.ContainsKey(owner))
            {
                Debug.LogWarning($"{LogTag} owner '{owner}' 已经登记过，跳过");
                return;
            }

            var discovered = new List<EliteBuffBase>();
            foreach (var type in DiscoverTypes(assembly))
            {
                var template = BuildTemplate(type);
                if (template != null) discovered.Add(template);
            }

            // ── 先自检、再登记：撞号/重名/越界的后果都是**静默吞并**（见类型注释），
            //    所以宁可在启动期报错也不让它悄悄上线 ──
            var accepted = Validate(discovered);

            var list = new List<EliteBuffBase>(accepted.Count);
            foreach (var template in accepted)
            {
                ByName[template.BuffName] = template;
                ByType[template.GetType()] = template;
                list.Add(template);
            }

            ByOwner[owner] = list;
            Debug.Log($"{LogTag} [{owner}] 已登记 {list.Count} 个 Buff：" +
                      string.Join(", ", list.ConvertAll(t => $"{t.BuffName}({t.BuffId})")));
        }

        /// <summary>注销某个 owner 登记的全部 Buff，并销毁其模板。</summary>
        public static void UnregisterAll(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return;
            if (!ByOwner.TryGetValue(owner, out var templates)) return;

            foreach (var template in templates)
            {
                if (template == null) continue;
                ByName.Remove(template.BuffName);
                ByType.Remove(template.GetType());
                if (template.gameObject != null) UnityEngine.Object.Destroy(template.gameObject);
            }

            ByOwner.Remove(owner);
            Debug.Log($"{LogTag} [{owner}] 已注销 {templates.Count} 个 Buff");
        }

        /// <summary>按类型取模板（<see cref="EliteBuffs.Apply{T}"/> 用它）。未登记时返回 null。</summary>
        public static EliteBuffBase TemplateOf<T>() where T : EliteBuffBase
            => ByType.TryGetValue(typeof(T), out var t) ? t : null;

        /// <summary>按 <see cref="EliteBuffBase.BuffName"/> 取模板。未登记时返回 null。</summary>
        public static EliteBuffBase TemplateOf(string buffName)
            => !string.IsNullOrEmpty(buffName) && ByName.TryGetValue(buffName, out var t) ? t : null;

        // ═══════════════════════════════════════════════════════════════
        //  本地化注入
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 校验每个 Buff 的名称/描述键都在 CSV 里存在。
        ///
        /// <para><b>推送不在这里做</b>：<c>EliteEnemies.Localization.LocalizationManager</c>
        /// 会把**全部** CSV 文本推给游戏本地化器（含 Buff 的键），所以这里只需要校验。
        /// 早先这里自己逐键 <c>SetOverrideText</c>，那是"谁记得推谁推"的写法——
        /// 已收敛成一处。</para>
        ///
        /// <para>Buff 的 <c>DisplayName</c>/<c>Description</c> 由游戏自己的本地化器解析
        /// （<c>displayName.ToPlainText()</c>，<c>Buff.cs:130</c>/<c>:134</c>），
        /// 所以键缺失时界面会显示 <c>*Buff_Xxx_Name*</c>——**必须报出来**。</para>
        /// </summary>
        public static void ValidateLocalization()
        {
            int missing = 0;

            foreach (var template in ByName.Values)
            {
                if (template == null) continue;

                if (!EliteEnemies.Localization.LocalizationManager.HasKey(template.NameKey))
                {
                    missing++;
                    Debug.LogError($"{LogTag} 缺少本地化键 '{template.NameKey}'（{template.GetType().Name}）——" +
                                   "请到 localization\\*.csv 里补上，否则界面会显示原始键名");
                }

                if (template.HasDescription
                    && !EliteEnemies.Localization.LocalizationManager.HasKey(template.DescriptionKey))
                {
                    missing++;
                    Debug.LogError($"{LogTag} 缺少本地化键 '{template.DescriptionKey}'（{template.GetType().Name}）——" +
                                   "请到 localization\\*.csv 里补上");
                }
            }

            Debug.Log(missing == 0
                ? $"{LogTag} {ByName.Count} 个 Buff 的本地化键齐全"
                : $"{LogTag} ⚠ 有 {missing} 个 Buff 本地化键缺失（见上方报错）");
        }

        // ═══════════════════════════════════════════════════════════════

        private static IEnumerable<Type> DiscoverTypes(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 有类型加载失败时 GetTypes 会整体抛异常——退而用能拿到的那部分，
                // 但**不静默**：把失败数报出来。
                Debug.LogError($"{LogTag} 扫描程序集时有类型加载失败（拿到 {ex.Types?.Length ?? 0} 个），" +
                               $"可能漏登记 Buff：{ex.Message}");
                types = ex.Types;
            }

            if (types == null) yield break;

            foreach (var type in types)
            {
                if (type == null) continue;
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(EliteBuffBase).IsAssignableFrom(type)) continue;
                if (type.ContainsGenericParameters) continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    // 长得像 Buff 却登记不上——**逐条报**，别让它静默消失
                    Debug.LogError($"{LogTag} {type.FullName} 继承自 EliteBuffBase 但没有公开无参构造器，无法登记");
                    continue;
                }

                yield return type;
            }
        }

        private static EliteBuffBase BuildTemplate(Type type)
        {
            try
            {
                var go = new GameObject($"EliteBuffTemplate_{type.Name}");
                UnityEngine.Object.DontDestroyOnLoad(go);

                var template = (EliteBuffBase)go.AddComponent(type);
                template.ConfigureTemplate();
                return template;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 为 {type.FullName} 建模板失败: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 逐条检查并返回应当登记的模板。任何一条不通过都 <c>LogError</c> 并**丢弃该项**——
        /// 宁可这个 Buff 不生效（响），也不要它上线后被静默吞并（静默）。
        /// </summary>
        private static List<EliteBuffBase> Validate(List<EliteBuffBase> discovered)
        {
            var accepted = new List<EliteBuffBase>(discovered.Count);
            var seenIds = new Dictionary<int, EliteBuffBase>();
            var seenNames = new Dictionary<string, EliteBuffBase>(StringComparer.Ordinal);

            foreach (var template in discovered)
            {
                var name = template.BuffName;
                var id = template.BuffId;

                if (string.IsNullOrEmpty(name))
                {
                    Debug.LogError($"{LogTag} {template.GetType().Name} 的 BuffName 为空，已丢弃");
                    continue;
                }

                if (id < IdRangeMin || id > IdRangeMax)
                {
                    Debug.LogError($"{LogTag} {name} 的 ID {id} 落在保留号段 " +
                                   $"[{IdRangeMin}, {IdRangeMax}] 之外，已丢弃——" +
                                   "号段外的 ID 可能与原生 Buff 撞号，撞号的后果是**静默吞并**（你的 Buff 根本不会被实例化）");
                    continue;
                }

                if (seenNames.TryGetValue(name, out var nameOwner))
                {
                    Debug.LogError($"{LogTag} Buff 名 '{name}' 重复：{nameOwner.GetType().Name} 与 " +
                                   $"{template.GetType().Name}，后者已丢弃");
                    continue;
                }

                if (seenIds.TryGetValue(id, out var idOwner))
                {
                    Debug.LogError($"{LogTag} Buff ID {id} 重复：{idOwner.GetType().Name} 与 " +
                                   $"{template.GetType().Name}（{name}），后者已丢弃——" +
                                   "同 ID 的两个 Buff 会被游戏当成同一个，施加时互相吞并");
                    continue;
                }

                seenNames[name] = template;
                seenIds[id] = template;
                accepted.Add(template);
            }

            if (accepted.Count != discovered.Count)
            {
                Debug.LogError($"{LogTag} 扫描到 {discovered.Count} 个 Buff，其中 " +
                               $"{discovered.Count - accepted.Count} 个未通过自检被丢弃（原因见上）");
            }

            return accepted;
        }
    }
}
