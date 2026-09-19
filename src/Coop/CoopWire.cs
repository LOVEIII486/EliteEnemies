using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 联机自定义频道的报文编解码。
    ///
    /// <para><b>为什么要自带魔数与版本号</b>：这个频道是<b>本模组独有</b>的
    /// （<c>ModNetworkApi</c> 按频道名分发），但对端模组的版本可能与本端不同——
    /// 联机模组本身会校验版本，本模组不会。魔数让"这压根不是我们的报文"能被
    /// <b>认出来并显式报错</b>，而不是当成破损数据静默丢弃；版本号则让日后改格式时
    /// 能给出可区分的提示。</para>
    ///
    /// <para>⚠ <b>改格式必须同时递增 <see cref="ProtocolVersion"/></b>——
    /// 两端版本不一致时说得出"对端格式过旧/过新"，而不是解出一堆垃圾。</para>
    /// </summary>
    internal static class CoopWire
    {
        /// <summary>报文格式版本。**改格式就 +1。**</summary>
        public const byte ProtocolVersion = 1;

        /// <summary>魔数：ASCII "EECP"（EliteEnemies CooP）的小端序。</summary>
        private const uint Magic = 0x50434545;

        /// <summary>
        /// 编码「某只 AI 是精英 + 它的词条清单」。
        /// 第 0 期用不到 combo，故不设该字段——等要用时改格式并递增版本号。
        /// </summary>
        public static byte[] EncodeEliteAffixes(int aiId, IReadOnlyList<string> affixes)
        {
            using (var stream = new MemoryStream(64))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(ProtocolVersion);
                writer.Write(aiId);

                int count = affixes?.Count ?? 0;
                writer.Write(count);
                for (int i = 0; i < count; i++)
                    writer.Write(affixes[i] ?? string.Empty);

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 解码。任何一处不合法都返回 false 并<b>不产生副作用</b>——
        /// 调用方据此给出可区分的提示（见 <see cref="CoopEliteSync.OnNetworkMessage"/>）。
        ///
        /// <para>失败原因用 <paramref name="failure"/> 带出，便于日志说清是"格式版本不一致"
        /// 还是"数据本身坏了"。</para>
        /// </summary>
        public static bool TryDecodeEliteAffixes(ReadOnlySpan<byte> payload, out int aiId,
                                                 out List<string> affixes, out string failure)
        {
            aiId = 0;
            affixes = null;
            failure = null;

            if (payload.Length < sizeof(uint) + sizeof(byte) + sizeof(int) + sizeof(int))
            {
                failure = $"报文过短（{payload.Length} 字节）";
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(payload.ToArray(), false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadUInt32() != Magic)
                    {
                        failure = "魔数不匹配（不是本模组的报文）";
                        return false;
                    }

                    byte version = reader.ReadByte();
                    if (version != ProtocolVersion)
                    {
                        failure = $"格式版本不一致（本端 {ProtocolVersion}，报文 {version}）" +
                                  "——两端模组版本不同，请统一";
                        return false;
                    }

                    aiId = reader.ReadInt32();

                    int count = reader.ReadInt32();
                    if (count < 0 || count > 64)
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
            catch (Exception ex)
            {
                failure = $"解析失败：{ex.Message}";
                return false;
            }
        }
    }
}
