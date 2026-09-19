using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 精英的**视觉状态**：体型缩放 + 显隐。**它不属于任何一只 AI 的专有字段，
    /// 而是随"精英条目"一起走的**（见 <see cref="CoopWire.ProtocolVersion"/> 的 v7 说明）。
    ///
    /// <para><c>Has == false</c> 表示"这一条没带视觉信息"——接收方据此**不要覆盖**已知值。
    /// 之所以要这个"有没有"，是因为 <c>Vector3.one</c> / <c>false</c> 本身就是**合法状态**
    /// （未巨大化、未隐身），拿默认值当"没有"就再也分不开了。</para>
    /// </summary>
    internal struct EliteVisualState
    {
        public bool Has;

        /// <summary>
        /// **绝对缩放**（直接就是 <c>transform.localScale</c>），**不是倍率**——
        /// 史莱姆写的是 <c>_originalScale * 倍率</c>，按倍率重建会错。
        /// </summary>
        public Vector3 Scale;

        public bool Hidden;
    }

    /// <summary>解出来的一条消息。<see cref="Kind"/> 决定哪些字段有意义。</summary>
    internal sealed class EliteMessage
    {
        public CoopWire.Kind Kind;
        public int AiId;

        /// <summary>combo 精英的 combo id；非 combo 精英为空串。</summary>
        public string ComboId;

        public List<string> Affixes;

        /// <summary>仅「玩家效果」类报文用：效果作用在哪个玩家（网络 id）。</summary>
        public string TargetPlayerId;

        /// <summary>仅「玩家效果」类报文用：效果种类（见 <c>CoopPlayerEffect.Kind</c>）。</summary>
        public byte Effect;

        /// <summary>仅「玩家效果」类报文用：三个浮点参数（含义随 <see cref="Effect"/> 而定）。</summary>
        public float Fx, Fy, Fz;

        /// <summary>仅「玩家效果」类报文用：第一个整数参数（通常是"这件事关于哪只精英"）。</summary>
        public int Ei;

        /// <summary>仅「玩家效果」类报文用：第二个整数参数（偷窃回报时是物品的 typeId）。</summary>
        public int Ei2;

        /// <summary>弹字报文用：本地化键（玩家效果报文里也用它装键）。</summary>
        public string Text;

        /// <summary>弹字报文用：**格式参数**（语言无关的那部分，例：百分比数字）。可为空。</summary>
        public string TextArg;

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：全量快照的 id 列表。</summary>
        public readonly List<int> BatchIds = new List<int>();

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：与 <see cref="BatchIds"/> 一一对应。</summary>
        public readonly List<string> BatchComboIds = new List<string>();

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：与 <see cref="BatchIds"/> 一一对应的词条表。</summary>
        public readonly List<List<string>> BatchAffixes = new List<List<string>>();

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：与 <see cref="BatchIds"/> 一一对应的视觉状态。</summary>
        public readonly List<EliteVisualState> BatchVisuals = new List<EliteVisualState>();

        /// <summary>词条 / 全量条目带回来的视觉状态（v7）。<c>Has=false</c> = 主机那边也没有。</summary>
        public EliteVisualState Visual;
    }

    /// <summary>
    /// 联机自定义频道的报文编解码。
    ///
    /// <para><b>为什么要自带魔数与版本号</b>：这个频道是本模块独有的，但对端模组版本可能与本端不同。
    /// 魔数让"这压根不是我们的报文"能被<b>认出来并显式报错</b>；版本号让格式变更能给出可区分的提示。</para>
    ///
    /// <para>⚠️ <b>改格式必须同时递增 <see cref="ProtocolVersion"/></b>，
    /// 并在下面补一条"该版本变了什么"的说明——否则两端不一致时只会得到一句
    /// "版本不一致"，没人知道差在哪。</para>
    ///
    /// <para><b>版本历史</b></para>
    /// <list type="bullet">
    /// <item><b>v1</b>：只有一种报文（`[aiId][词条清单]`），且**所有精英共用同一个频道**。</item>
    /// <item><b>v2</b>：新增「查询 / 全量」两种报文。
    /// 起因是一处**实测发现的缺陷**：联机模组的补发机制只缓存**每个频道的最后一条**
    /// （<c>ModNetworkApi.cs:303-331</c> `CacheBroadcast` 是"赋值"不是"入队"），
    /// 所以共用一个频道时，迟到的客户端<b>只能补到最后一只精英</b>，前面全丢——
    /// 实测 59 条广播只到了 40 条。现在客户端可以主动要一份全量。</item>
    ///
    /// <item><b>v3</b>：每条多加一个 <c>comboId</c> 字符串（非 combo 精英为空串）。
    /// <b>传的是 id 不是称号串</b>——称号由 combo 定义现算，客户端拿到 id 自己查回定义，
    /// 才会跟着客户端那门语言走（传渲染好的字符串等于把语言焊死，本工程在本地化上栽过）。</item>
    ///
    /// <item><b>v4</b>：新增「玩家效果」与「玩家效果结果」两种报文。
    /// 起因是**架构性的**：作用于玩家的效果（击退/换位/偷窃/咬枪）在联机下
    /// <b>不能由主机替远端玩家做</b>——主机上那个只是复制体，改了到不了真人。
    /// 只能把效果<b>带参数</b>转交给受害者那台机器执行。
    /// （试过复用联机模组的 buff 转发，但 <c>PlayerBuffBroadcastRpc</c> 只有
    /// <c>PlayerId/WeaponTypeId/BuffId</c> 三个字段，<b>传不了参数</b>。）</item>
    ///
    /// <item><b>v5</b>：新增「精英视觉」与「AI 弹字」两种报文，并给玩家效果报文加了一个文本字段。
    /// 起因：<b>联机模组的通用 <c>PopText</c> 补丁被注释掉了</b>
    /// （它自己的 <c>Patch/Character/AIAwarenessPatch.cs:10-19</c>），
    /// 所以<b>第三方调 <c>cmc.PopText()</c> 一律不过网</b>；而体型（<c>localScale</c>）
    /// 也不在 <c>AISyncEntry</c> 里。两者都只能由本模块自己补。</item>
    ///
    /// <item><b>v6</b>：弹字报文改传**本地化键 + 兜底 + 格式参数**，不再传渲染好的译文——
    /// 传译文会把**主机那门语言**焊死到客机身上（<c>AGENT.md §3.5</c>）。
    /// 格式参数只放**语言无关**的那部分（数字等）；参数里若含本地化文本，那条只能退到传译文。</item>
    ///
    /// <item><b>v7</b>：**视觉状态并入"精英条目"本身**——词条报文与全量快照都带上
    /// 「体型缩放 + 显隐」（可缺省，<c>Has=false</c> 只占 1 字节）。
    ///
    /// 起因是一处**实测缺陷**：视觉状态原先是独立的一条**一次性**消息
    /// （<see cref="Kind.EliteVisual"/>），于是<b>错过的就永远错过</b>——
    /// 中途加入的客机、以及复制体被销毁重建过的客机（联机模组在离开
    /// <c>DeactivationRadius</c> 时整个销毁复制体）都拿不到，
    /// 而词条标签走全量快照**补得回来**。症状正是「**标签看得到、体型看不到**」。
    ///
    /// 联机模组自己的文档给过同一条纪律（<c>01-third-party-api.md</c> §3.4：
    /// 补发只缓存"最后一条"）⇒ **按"最新状态覆盖"设计，不要按逐条事件流设计**。
    /// <see cref="Kind.EliteVisual"/> 保留，但只承担"后续变化"（史莱姆每帧重算、
    /// 隐身来回切），**首次状态一律由条目携带**。</item>
    ///
    /// <item><b>v8</b>：新增「玩家弹字」报文（<c>[玩家id][本地化键][兜底][格式参数]</c>）。
    ///
    /// 起因是一处**从未接通的通道**：<c>PlayerEffectRelay.PlayerPopTextHandler</c> 定义了、
    /// <c>TryRelayPlayerPopText</c> 也在调，但<b>全库没有任何一处给它赋值</b>，
    /// 于是恒不接管 ⇒ 主机把"你的武器掉了"这类提示弹在**客机玩家的复制体**上，
    /// 真人永远看不到（弹匣诅咒 / 粘性两个词条都中招）。
    ///
    /// 它**没有**并进「玩家效果」报文：那条只有一个文本字段，
    /// 装不下"键 + 兜底"两份（而兜底必须带，理由同 v6）。</item>
    ///
    /// </list>
    /// </summary>
    internal static class CoopWire
    {
        /// <summary>报文格式版本。**改格式就 +1，并在类注释的版本历史里补一条。**</summary>
        public const byte ProtocolVersion = 8;

        /// <summary>魔数：ASCII "EECP"（EliteEnemies CooP）的小端序。</summary>
        private const uint Magic = 0x50434545;

        /// <summary>单个词条清单的长度上限（防御畸形报文，不是业务上限）。</summary>
        private const int MaxAffixesPerEntry = 64;

        /// <summary>全量快照里的条目数上限。</summary>
        private const int MaxBatchEntries = 4096;

        private const int HeaderSize = sizeof(uint) + sizeof(byte) + sizeof(byte);

        public enum Kind : byte
        {
            /// <summary>主机 → 客户端：某只 AI 是精英，带它的词条清单。</summary>
            Affix = 1,

            /// <summary>客户端 → 主机：请把你当前知道的全部精英发我（补全量）。</summary>
            Query = 2,

            /// <summary>主机 → 客户端：全量快照，回应 <see cref="Query"/>。</summary>
            Batch = 3,

            /// <summary>
            /// 主机 → 全体：请**某个玩家**在他那台机器上执行一个效果。
            /// 收到的一方比对自己的网络 id，是给自己才执行。
            /// </summary>
            PlayerEffect = 4,

            /// <summary>
            /// 客户端 → 主机：某个玩家效果执行完了，回报结果（目前只有偷窃需要）。
            /// </summary>
            PlayerEffectResult = 5,

            /// <summary>
            /// 主机 → 全体：某只 AI 的**视觉状态变了**（体型缩放 / 显隐）。
            /// 起因是这些**不在** <c>AISyncEntry</c> 里（见分析档 §5.4 G4），
            /// 客机因此看不到巨大化/迷你/隐身。
            ///
            /// <para>⚠ v7 起它只承担**变化**：首次状态搭在词条/全量条目的
            /// <see cref="EliteVisualState"/> 上——一次性消息错过就没有了，
            /// 而客机可能还没进图、或复制体已被销毁重建。</para>
            /// </summary>
            EliteVisual = 6,

            /// <summary>主机 → 全体：在某只 AI 头顶弹一行字（<c>AiId</c> + <c>Text</c>）。</summary>
            AiPopText = 7,

            /// <summary>
            /// 主机 → 全体：在**某个玩家**头顶弹一行字。收到的一方比对自己的网络 id，
            /// 是给自己才弹（弹在自己的真人角色上）。
            /// </summary>
            PlayerPopText = 8,
        }

        // ==================== 编码 ====================

        public static byte[] EncodeAffix(int aiId, string comboId, IReadOnlyList<string> affixes,
                                         EliteVisualState visual)
        {
            using (var stream = new MemoryStream(64))
            using (var writer = NewWriter(stream, Kind.Affix))
            {
                WriteEntry(writer, aiId, comboId, affixes, visual);
                return Finish(stream, writer);
            }
        }

        public static byte[] EncodeQuery()
        {
            using (var stream = new MemoryStream(HeaderSize))
            using (var writer = NewWriter(stream, Kind.Query))
            {
                return Finish(stream, writer);
            }
        }

        /// <summary>全量快照。四个列表必须等长。</summary>
        public static byte[] EncodeBatch(IReadOnlyList<int> ids, IReadOnlyList<string> comboIds,
                                         IReadOnlyList<List<string>> affixes,
                                         IReadOnlyList<EliteVisualState> visuals)
        {
            int count = Math.Min(ids?.Count ?? 0,
                        Math.Min(comboIds?.Count ?? 0,
                        Math.Min(affixes?.Count ?? 0, visuals?.Count ?? 0)));

            using (var stream = new MemoryStream(256))
            using (var writer = NewWriter(stream, Kind.Batch))
            {
                writer.Write(count);
                for (int i = 0; i < count; i++)
                    WriteEntry(writer, ids[i], comboIds[i], affixes[i], visuals[i]);

                return Finish(stream, writer);
            }
        }

        /// <summary>
        /// 玩家效果报文。<paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/> 与
        /// <paramref name="i"/> 的含义随 <paramref name="effect"/> 而定（见 <c>CoopPlayerEffect.Kind</c>）。
        ///
        /// <para>「效果」与「效果结果」两种报文**共用同一个形状**——结果只是
        /// 反向发送的同一张表，没必要再定义一套。</para>
        /// </summary>
        public static byte[] EncodePlayerEffect(Kind kind, string targetPlayerId, byte effect,
                                                float x, float y, float z, int i, int i2 = 0,
                                                string text = null)
        {
            using (var stream = new MemoryStream(48))
            using (var writer = NewWriter(stream, kind))
            {
                writer.Write(targetPlayerId ?? string.Empty);
                writer.Write(effect);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                writer.Write(i);
                writer.Write(i2);
                writer.Write(text ?? string.Empty);
                return Finish(stream, writer);
            }
        }

        /// <summary>
        /// AI 弹字：<c>[aiId][本地化键][兜底文本]</c>。
        ///
        /// <para>⚠ <b>传的是键，不是译文。</b>传渲染好的文本等于把<b>主机那门语言</b>
        /// 焊死到客机身上——正是 <c>AGENT.md §3.5</c> 反复强调的那类问题。
        /// 兜底文本也一并传：它是**代码里的常量**，两端本是同一份，
        /// 但接收方是通用路径、不知道是哪一条，所以带上最省事。</para>
        /// </summary>
        public static byte[] EncodeAiPopText(int aiId, string key, string fallback, string arg)
        {
            using (var stream = new MemoryStream(160))
            using (var writer = NewWriter(stream, Kind.AiPopText))
            {
                writer.Write(aiId);
                writer.Write(key ?? string.Empty);
                writer.Write(fallback ?? string.Empty);
                writer.Write(arg ?? string.Empty);
                return Finish(stream, writer);
            }
        }

        /// <summary>
        /// 精英视觉的**变化**：<c>[aiId][缩放向量][是否隐藏]</c>。
        ///
        /// <para>⚠ <b>首次状态不走这里</b>，而是搭在词条报文 / 全量快照的条目上
        /// （v7，见 <see cref="ProtocolVersion"/>）——这条只负责"之后的变化"，
        /// 因为一次性消息错过就没了，而客机可能还没进图、或复制体已被销毁重建。</para>
        ///
        /// <para>⚠ <b>传的是**绝对缩放**（三个分量），不是一个标量倍率。</b>
        /// 因为史莱姆是 <c>_originalScale * 倍率</c>——原模型 scale 不是 1 时，
        /// 对端按 <c>Vector3.one * 倍率</c> 重建就错了。</para>
        /// </summary>
        public static byte[] EncodeEliteVisual(int aiId, Vector3 scale, bool hidden)
        {
            using (var stream = new MemoryStream(24))
            using (var writer = NewWriter(stream, Kind.EliteVisual))
            {
                writer.Write(aiId);
                writer.Write(scale.x);
                writer.Write(scale.y);
                writer.Write(scale.z);
                writer.Write(hidden);
                return Finish(stream, writer);
            }
        }

        /// <summary>
        /// 玩家弹字：<c>[玩家网络 id][本地化键][兜底文本][格式参数]</c>。
        ///
        /// <para>与 <see cref="EncodeAiPopText"/> 同形，只是把 <c>aiId</c> 换成玩家 id
        /// （玩家在网络上是**字符串** id，见 <c>CoopApi.SelfPlayerId</c>）。
        /// 传键不传译文、兜底一起带，理由见 v6/v8 的版本说明。</para>
        /// </summary>
        public static byte[] EncodePlayerPopText(string playerId, string key, string fallback, string arg)
        {
            using (var stream = new MemoryStream(160))
            using (var writer = NewWriter(stream, Kind.PlayerPopText))
            {
                writer.Write(playerId ?? string.Empty);
                writer.Write(key ?? string.Empty);
                writer.Write(fallback ?? string.Empty);
                writer.Write(arg ?? string.Empty);
                return Finish(stream, writer);
            }
        }

        private static BinaryWriter NewWriter(Stream stream, Kind kind)
        {
            var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic);
            writer.Write(ProtocolVersion);
            writer.Write((byte)kind);
            return writer;
        }

        private static void WriteEntry(BinaryWriter writer, int aiId, string comboId,
                                       IReadOnlyList<string> affixes, EliteVisualState visual)
        {
            writer.Write(aiId);
            writer.Write(comboId ?? string.Empty);

            int count = affixes?.Count ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
                writer.Write(affixes[i] ?? string.Empty);

            // v7：视觉状态搭在条目里。绝大多数精英没有（Has=false ⇒ 只多 1 字节）。
            writer.Write(visual.Has);
            if (!visual.Has) return;

            writer.Write(visual.Scale.x);
            writer.Write(visual.Scale.y);
            writer.Write(visual.Scale.z);
            writer.Write(visual.Hidden);
        }

        private static byte[] Finish(MemoryStream stream, BinaryWriter writer)
        {
            writer.Flush();
            return stream.ToArray();
        }

        // ==================== 解码 ====================

        /// <summary>
        /// 解码。任何一处不合法都返回 false 并**不产生副作用**，
        /// 失败原因由 <paramref name="failure"/> 带出，便于日志说清是"版本不一致"还是"数据坏了"。
        /// </summary>
        public static bool TryDecode(ReadOnlySpan<byte> payload, out EliteMessage message, out string failure)
        {
            message = null;
            failure = null;

            if (payload.Length < HeaderSize)
            {
                failure = $"报文过短（{payload.Length} 字节，头就要 {HeaderSize}）";
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(payload.ToArray(), false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadUInt32() != Magic)
                    {
                        failure = "魔数不匹配（不是本模块的报文）";
                        return false;
                    }

                    byte version = reader.ReadByte();
                    if (version != ProtocolVersion)
                    {
                        failure = $"格式版本不一致（本端 {ProtocolVersion}，报文 {version}）" +
                                  "——两端模组版本不同，请统一";
                        return false;
                    }

                    var kind = (Kind)reader.ReadByte();
                    var result = new EliteMessage { Kind = kind };

                    switch (kind)
                    {
                        case Kind.Affix:
                            if (!ReadEntry(reader, out int aiId, out string comboId,
                                           out var affixes, out var visual, out failure)) return false;
                            result.AiId = aiId;
                            result.ComboId = comboId;
                            result.Affixes = affixes;
                            result.Visual = visual;
                            break;

                        case Kind.Query:
                            break;   // 无载荷

                        case Kind.PlayerEffect:
                        case Kind.PlayerEffectResult:
                            result.TargetPlayerId = reader.ReadString();
                            result.Effect = reader.ReadByte();
                            result.Fx = reader.ReadSingle();
                            result.Fy = reader.ReadSingle();
                            result.Fz = reader.ReadSingle();
                            result.Ei = reader.ReadInt32();
                            result.Ei2 = reader.ReadInt32();
                            result.Text = reader.ReadString();
                            break;

                        case Kind.EliteVisual:
                            result.AiId = reader.ReadInt32();
                            result.Fx = reader.ReadSingle();     // 缩放 x
                            result.Fy = reader.ReadSingle();     // 缩放 y
                            result.Fz = reader.ReadSingle();     // 缩放 z
                            result.Ei = reader.ReadBoolean() ? 1 : 0;   // 是否隐藏
                            break;

                        case Kind.AiPopText:
                            result.AiId = reader.ReadInt32();
                            result.Text = reader.ReadString();       // 本地化键
                            result.ComboId = reader.ReadString();    // 兜底文本（复用这个字符串字段）
                            result.TextArg = reader.ReadString();    // 格式参数（可为空）
                            break;

                        case Kind.PlayerPopText:
                            result.TargetPlayerId = reader.ReadString();   // 玩家的网络 id
                            result.Text = reader.ReadString();             // 本地化键
                            result.ComboId = reader.ReadString();          // 兜底文本
                            result.TextArg = reader.ReadString();          // 格式参数（可为空）
                            break;

                        case Kind.Batch:
                            int entries = reader.ReadInt32();
                            if (entries < 0 || entries > MaxBatchEntries)
                            {
                                failure = $"全量条目数不合理（{entries}）";
                                return false;
                            }

                            for (int i = 0; i < entries; i++)
                            {
                                if (!ReadEntry(reader, out int batchId, out string batchCombo,
                                               out var batchAffixes, out var batchVisual,
                                               out failure)) return false;
                                result.BatchIds.Add(batchId);
                                result.BatchComboIds.Add(batchCombo);
                                result.BatchAffixes.Add(batchAffixes);
                                result.BatchVisuals.Add(batchVisual);
                            }
                            break;

                        default:
                            failure = $"未知的报文种类（{(byte)kind}）——两端模组版本可能不同";
                            return false;
                    }

                    message = result;
                    return true;
                }
            }
            catch (Exception ex)
            {
                failure = $"解析失败：{ex.Message}";
                return false;
            }
        }

        private static bool ReadEntry(BinaryReader reader, out int aiId, out string comboId,
                                      out List<string> affixes, out EliteVisualState visual,
                                      out string failure)
        {
            aiId = 0;
            comboId = null;
            affixes = null;
            visual = default(EliteVisualState);
            failure = null;

            aiId = reader.ReadInt32();
            comboId = reader.ReadString();

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxAffixesPerEntry)
            {
                failure = $"词条数量不合理（{count}）";
                return false;
            }

            var list = new List<string>(count);
            for (int i = 0; i < count; i++)
                list.Add(reader.ReadString());

            affixes = list;

            // v7：视觉状态。缺省（Has=false）是合法且常见的情形。
            if (reader.ReadBoolean())
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                visual = new EliteVisualState
                {
                    Has = true,
                    Scale = new Vector3(x, y, z),
                    Hidden = reader.ReadBoolean()
                };
            }

            return true;
        }
    }
}
