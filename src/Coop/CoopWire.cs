using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EliteEnemies.Coop
{
    /// <summary>解出来的一条消息。<see cref="Kind"/> 决定哪些字段有意义。</summary>
    internal sealed class EliteMessage
    {
        public CoopWire.Kind Kind;
        public int AiId;
        public List<string> Affixes;

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：全量快照的 id 列表。</summary>
        public readonly List<int> BatchIds = new List<int>();

        /// <summary>仅 <see cref="CoopWire.Kind.Batch"/> 用：与 <see cref="BatchIds"/> 一一对应的词条表。</summary>
        public readonly List<List<string>> BatchAffixes = new List<List<string>>();
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
    /// </list>
    /// </summary>
    internal static class CoopWire
    {
        /// <summary>报文格式版本。**改格式就 +1，并在类注释的版本历史里补一条。**</summary>
        public const byte ProtocolVersion = 2;

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
        }

        // ==================== 编码 ====================

        public static byte[] EncodeAffix(int aiId, IReadOnlyList<string> affixes)
        {
            using (var stream = new MemoryStream(64))
            using (var writer = NewWriter(stream, Kind.Affix))
            {
                WriteAffixes(writer, aiId, affixes);
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

        /// <summary>全量快照。<paramref name="ids"/> 与 <paramref name="affixes"/> 必须等长。</summary>
        public static byte[] EncodeBatch(IReadOnlyList<int> ids, IReadOnlyList<List<string>> affixes)
        {
            int count = Math.Min(ids?.Count ?? 0, affixes?.Count ?? 0);

            using (var stream = new MemoryStream(256))
            using (var writer = NewWriter(stream, Kind.Batch))
            {
                writer.Write(count);
                for (int i = 0; i < count; i++)
                    WriteAffixes(writer, ids[i], affixes[i]);

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

        private static void WriteAffixes(BinaryWriter writer, int aiId, IReadOnlyList<string> affixes)
        {
            writer.Write(aiId);

            int count = affixes?.Count ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
                writer.Write(affixes[i] ?? string.Empty);
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
                            if (!ReadAffixes(reader, out int aiId, out var affixes, out failure)) return false;
                            result.AiId = aiId;
                            result.Affixes = affixes;
                            break;

                        case Kind.Query:
                            break;   // 无载荷

                        case Kind.Batch:
                            int entries = reader.ReadInt32();
                            if (entries < 0 || entries > MaxBatchEntries)
                            {
                                failure = $"全量条目数不合理（{entries}）";
                                return false;
                            }

                            for (int i = 0; i < entries; i++)
                            {
                                if (!ReadAffixes(reader, out int batchId, out var batchAffixes, out failure)) return false;
                                result.BatchIds.Add(batchId);
                                result.BatchAffixes.Add(batchAffixes);
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

        private static bool ReadAffixes(BinaryReader reader, out int aiId, out List<string> affixes, out string failure)
        {
            aiId = 0;
            affixes = null;
            failure = null;

            aiId = reader.ReadInt32();

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
            return true;
        }
    }
}
