using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Visuals
{
    /// <summary>
    /// 封弊者（<c>Obscurer</c>）的"乱码标签"：给某个角色生成随机乱码文本与颜色，并按固定节奏刷新。
    ///
    /// <para><b>为什么它在 <c>Visuals</c> 而不是词条行为里</b>：这个效果**只有血条 UI 用**。
    /// 原先它住在 `Affixes\Behaviors\ObscurerBehavior` 里，于是
    /// <c>Visuals → Affixes.Behaviors</c>（UI 读它）与 <c>Affixes.Behaviors → Visuals</c>
    /// （行为用发光/护盾助手）**互相依赖**。挪到消费方这一侧之后依赖只剩一个方向
    /// （见 <c>docs\词条模块审查与设计.md</c> §8）。</para>
    ///
    /// <para>于是 <c>Obscurer</c> 变成**没有行为类**的纯显示词条——和 Tanky 那类纯数值词条同类。
    /// 启动时的"有词条无行为"警告会把它列出来，那是**预期**的（见
    /// <c>AffixBehaviorRegistration.ValidateAffixCorrespondence</c>）。</para>
    ///
    /// <para><b>刷新由调用方驱动</b>（UI 每帧调 <see cref="GetTag"/>）：超过
    /// <see cref="RefreshInterval"/> 才重掷一次。原实现靠行为组件的 <c>OnUpdate</c> 计时——
    /// 那是"为了一个每帧计时器而多挂一个 MonoBehaviour"，这里省掉了。</para>
    /// </summary>
    internal static class EliteGarbledLabel
    {
        /// <summary>
        /// 词条键。**必须与 `EliteAffixes.Pool` 里的键一致**——拼错不会报错，
        /// 只是乱码永远不显示（血条会显示成普通词条名）。
        /// </summary>
        internal const string AffixName = "Obscurer";

        /// <summary>
        /// 血条 UI 与该词条之间的**哨兵**：UI 构建词条前缀时若发现这个精英带 <c>Obscurer</c>，
        /// 就把前缀先设成这个值，之后每帧换成 <see cref="GetTag"/> 给的乱码富文本。
        ///
        /// <para>提成常量是为了"一个概念一处"：原先这个字符串在 UI 里有两处字面量副本，
        /// 改一处漏一处就会静默失效（哨兵比不中，乱码就永远不显示）。</para>
        /// </summary>
        internal const string PlaceholderLabel = "<OBSCURER_PLACEHOLDER>";

        /// <summary>乱码与颜色的刷新间隔（秒）。</summary>
        private const float RefreshInterval = 0.3f;

        private const int MinGarbledLength = 4;
        private const int MaxGarbledLength = 6;

        private const string GarbledChars =
            "▓▒░█▉▊▋▌▍▎▏▀▄▐▌" +
            "■□◆◇▲▼◀▶●○◎" +
            "★☆※§¶†‡" +
            "，。！？；：、…·《》【】（）「」『』" +
            "#@$%&*?!~^|/\\+=-_:;,.`";

        private sealed class State
        {
            public string Tag;
            public float NextRefreshTime;
        }

        private static readonly Dictionary<CharacterMainControl, State> States =
            new Dictionary<CharacterMainControl, State>();

        /// <summary>
        /// 取该角色当前的乱码富文本标签（形如 <c>&lt;color=#A1B2C3&gt;[▓▒█]&lt;/color&gt;</c>）。
        /// 到点会重掷一次乱码与颜色。**每帧调用是廉价的**：稳态只做一次 `Time.time` 比较，
        /// 字符串只在刷新时重建。
        /// </summary>
        internal static string GetTag(CharacterMainControl character)
        {
            if (character == null)
            {
                // 角色没了（假空）时不该往字典里塞条目：直接给一份临时的
                return BuildTag(GenerateGarbledText(), GenerateColorHex());
            }

            if (!States.TryGetValue(character, out State state))
            {
                state = new State();
                States[character] = state;
            }
            else if (Time.time < state.NextRefreshTime)
            {
                return state.Tag;
            }

            state.Tag = BuildTag(GenerateGarbledText(), GenerateColorHex());
            state.NextRefreshTime = Time.time + RefreshInterval;
            return state.Tag;
        }

        /// <summary>血条释放时调用：这个角色的乱码状态不再需要（与"谁创建谁清理"配对）。</summary>
        internal static void Forget(CharacterMainControl character)
        {
            if (character != null) States.Remove(character);
        }

        /// <summary>
        /// 场景卸载时清空。**只增不减的静态字典必须有人清**——理由与
        /// <c>EliteEnemyCore.ClearIgnoredPresets</c> 相同（跨场景残留 + 以角色记账）。
        /// 调用点：<c>ModBehaviour.OnSceneUnloaded</c>。
        /// </summary>
        internal static void Clear()
        {
            States.Clear();
        }

        /// <summary>这批词条里有没有"封弊者"（血条据此决定显示乱码标签而不是词条名）。</summary>
        internal static bool AffixesContainsObscurer(List<string> affixes)
            => affixes != null && affixes.Contains(AffixName);

        private static string BuildTag(string garbled, string colorHex)
            => $"<color={colorHex}>[{garbled}]</color>";

        private static string GenerateGarbledText()
        {
            int length = Random.Range(MinGarbledLength, MaxGarbledLength + 1);
            var sb = new System.Text.StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                sb.Append(GarbledChars[Random.Range(0, GarbledChars.Length)]);
            }

            return sb.ToString();
        }

        private static string GenerateColorHex(int min = 70, int max = 255)
        {
            min = Mathf.Clamp(min, 0, 255);
            max = Mathf.Clamp(max, 1, 255);

            int r = Random.Range(min, max + 1);
            int g = Random.Range(min, max + 1);
            int b = Random.Range(min, max + 1);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
