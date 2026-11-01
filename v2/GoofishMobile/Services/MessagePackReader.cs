using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Goofish.Services
{
    /// <summary>
    /// 最小 MessagePack 解码器。闲鱼 IM 的部分 /s/sync 推送是 base64(MessagePack)，
    /// 解出来是以 "1"、"2"... 为键的嵌套结构，转成 JToken 后与 JSON 推送走同一套取值逻辑。
    /// </summary>
    public sealed class MessagePackReader
    {
        private const int MaxDepth = 32;
        private readonly byte[] _data;
        private int _pos;

        private MessagePackReader(byte[] data)
        {
            _data = data;
        }

        public static JToken Decode(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return new MessagePackReader(data).Read(0);
        }

        private JToken Read(int depth)
        {
            if (depth > MaxDepth) throw new FormatException("MessagePack 嵌套过深");
            var b = Byte();

            if (b <= 0x7f) return new JValue(b);
            if (b >= 0x80 && b <= 0x8f) return Map(b & 0x0f, depth);
            if (b >= 0x90 && b <= 0x9f) return Array(b & 0x0f, depth);
            if (b >= 0xa0 && b <= 0xbf) return new JValue(String(b & 0x1f));
            if (b >= 0xe0) return new JValue((sbyte)b);

            switch (b)
            {
                case 0xc0: return JValue.CreateNull();
                case 0xc2: return new JValue(false);
                case 0xc3: return new JValue(true);
                case 0xc4: return Binary(Byte());
                case 0xc5: return Binary(UInt16());
                case 0xc6: return Binary(checked((int)UInt32()));
                case 0xca: return new JValue(BitConverter.ToSingle(BigEndian(4), 0));
                case 0xcb: return new JValue(BitConverter.ToDouble(BigEndian(8), 0));
                case 0xcc: return new JValue(Byte());
                case 0xcd: return new JValue(UInt16());
                case 0xce: return new JValue(UInt32());
                case 0xcf: return new JValue(BitConverter.ToUInt64(BigEndian(8), 0));
                case 0xd0: return new JValue((sbyte)Byte());
                case 0xd1: return new JValue(BitConverter.ToInt16(BigEndian(2), 0));
                case 0xd2: return new JValue(BitConverter.ToInt32(BigEndian(4), 0));
                case 0xd3: return new JValue(BitConverter.ToInt64(BigEndian(8), 0));
                case 0xd9: return new JValue(String(Byte()));
                case 0xda: return new JValue(String(UInt16()));
                case 0xdb: return new JValue(String(checked((int)UInt32())));
                case 0xdc: return Array(UInt16(), depth);
                case 0xdd: return Array(checked((int)UInt32()), depth);
                case 0xde: return Map(UInt16(), depth);
                case 0xdf: return Map(checked((int)UInt32()), depth);
                default: throw new FormatException("未知的 MessagePack 类型 0x" + b.ToString("x2"));
            }
        }

        private JObject Map(int count, int depth)
        {
            var obj = new JObject();
            for (var i = 0; i < count; i++)
            {
                var key = Read(depth + 1);
                var value = Read(depth + 1);
                obj[key?.ToString() ?? string.Empty] = value;
            }
            return obj;
        }

        private JArray Array(int count, int depth)
        {
            var array = new JArray();
            for (var i = 0; i < count; i++) array.Add(Read(depth + 1));
            return array;
        }

        // 二进制段常常其实是 UTF-8 文本或嵌套 JSON，能当文本读就当文本读
        private JToken Binary(int length)
        {
            var bytes = Take(length);
            try
            {
                return new JValue(new UTF8Encoding(false, true).GetString(bytes, 0, bytes.Length));
            }
            catch (ArgumentException)
            {
                return new JValue(Convert.ToBase64String(bytes));
            }
        }

        private string String(int length)
        {
            var bytes = Take(length);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private byte Byte()
        {
            if (_pos >= _data.Length) throw new FormatException("MessagePack 数据提前结束");
            return _data[_pos++];
        }

        private int UInt16()
        {
            var b = Take(2);
            return (b[0] << 8) | b[1];
        }

        private uint UInt32()
        {
            var b = Take(4);
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        private byte[] Take(int count)
        {
            if (count < 0 || _pos + count > _data.Length) throw new FormatException("MessagePack 数据提前结束");
            var result = new byte[count];
            System.Array.Copy(_data, _pos, result, 0, count);
            _pos += count;
            return result;
        }

        private byte[] BigEndian(int count)
        {
            var bytes = Take(count);
            if (BitConverter.IsLittleEndian) System.Array.Reverse(bytes);
            return bytes;
        }
    }
}
