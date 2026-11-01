using System;
using System.Collections.Generic;
using System.Text;

namespace Goofish.Services
{
    /// <summary>编码好的二维码符号。</summary>
    public sealed class QrSymbol
    {
        internal QrSymbol(int version, int mask, bool[,] modules)
        {
            Version = version;
            Mask = mask;
            Modules = modules;
        }

        /// <summary>符号版本，1-40。</summary>
        public int Version { get; }

        /// <summary>实际使用的掩码编号，0-7。</summary>
        public int Mask { get; }

        /// <summary>模块矩阵，下标为 <c>[行, 列]</c>（即 [y, x]），true 为深色。不含静区。</summary>
        public bool[,] Modules { get; }

        /// <summary>边长，单位是模块数。</summary>
        public int Size => Version * 4 + 17;
    }

    /// <summary>
    /// 自带的 QR Code（Model 2）编码器：字节模式、纠错等级 M、版本 1-40 自动选择、掩码自动优选。
    /// </summary>
    /// <remarks>
    /// 为什么不引现成的二维码库、也不调在线生成接口：扫码登录的二维码内容就是登录令牌，
    /// 等同于账号凭据，发给在线接口等于把令牌交给第三方；而本项目要跑在
    /// Windows 10 Mobile（15063 + .NET Native）上，为一张表引一个库也不划算。
    /// 实现按 ISO/IEC 18004 写，正确性靠工具逐掩码与三套参考实现比对（见仓库说明）。
    /// </remarks>
    public static class QrEncoder
    {
        private const int MinVersion = 1;
        private const int MaxVersion = 40;

        /// <summary>纠错等级 M 的格式信息编码（2 位），画格式信息时用。</summary>
        private const int EccFormatBitsM = 0;

        /// <summary>掩码优选时用的四个权重，出自标准里那张罚分表。</summary>
        private const int PenaltyN1 = 3;
        private const int PenaltyN2 = 3;
        private const int PenaltyN3 = 40;
        private const int PenaltyN4 = 10;

        /// <summary>
        /// ISO/IEC 18004 每个 RS 块里的纠错码字数（纠错等级 M），下标是版本号，0 号位占位。
        /// </summary>
        private static readonly int[] EccCodewordsPerBlock =
        {
            -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
            26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28
        };

        /// <summary>ISO/IEC 18004 里纠错等级 M 的 RS 块数，下标是版本号，0 号位占位。</summary>
        private static readonly int[] BlockCounts =
        {
            -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
            17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49
        };

        /// <summary>编码一段文本，自动选择版本与掩码。</summary>
        /// <param name="text">要编码的内容，按 UTF-8 转成字节。</param>
        /// <returns>编码结果。</returns>
        public static QrSymbol Encode(string text)
        {
            return Encode(text, -1);
        }

        /// <summary>编码一段文本。</summary>
        /// <param name="text">要编码的内容，按 UTF-8 转成字节。</param>
        /// <param name="mask">指定掩码 0-7；传 -1 表示按标准算罚分自动优选。</param>
        /// <returns>编码结果。</returns>
        public static QrSymbol Encode(string text, int mask)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (mask < -1 || mask > 7) throw new ArgumentOutOfRangeException(nameof(mask));

            var data = Encoding.UTF8.GetBytes(text);
            var version = ChooseVersion(data.Length);
            var size = version * 4 + 17;

            var modules = new bool[size, size];
            var isFunction = new bool[size, size];

            DrawFunctionPatterns(modules, isFunction, version);
            DrawCodewords(modules, isFunction, version, AddErrorCorrection(BuildDataCodewords(data, version), version));

            if (mask < 0) mask = ChooseMask(modules, isFunction, version);
            ApplyMask(modules, isFunction, mask);
            DrawFormatBits(modules, isFunction, version, mask);

            return new QrSymbol(version, mask, modules);
        }

        // ---- 数据编码 ----

        private static int ByteCountBits(int version)
        {
            // 字节模式的字符计数指示符：1-9 版 8 位，10-40 版 16 位
            return version < 10 ? 8 : 16;
        }

        private static int ChooseVersion(int byteLength)
        {
            for (var version = MinVersion; version <= MaxVersion; version++)
            {
                var needed = 4 + ByteCountBits(version) + byteLength * 8;
                if (needed <= DataCodewords(version) * 8) return version;
            }
            throw new ArgumentException("内容太长，超出二维码容量。", nameof(byteLength));
        }

        /// <summary>排除功能图形后剩下的模块数（含 0-7 个剩余位）。</summary>
        private static int RawDataModules(int version)
        {
            var result = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                var align = version / 7 + 2;
                result -= (25 * align - 10) * align - 55;
                if (version >= 7) result -= 36;
            }
            return result;
        }

        private static int DataCodewords(int version)
        {
            return RawDataModules(version) / 8 - EccCodewordsPerBlock[version] * BlockCounts[version];
        }

        /// <summary>拼出数据码字：模式指示符、计数、内容、结束符、补位。</summary>
        private static byte[] BuildDataCodewords(byte[] data, int version)
        {
            var capacity = DataCodewords(version);
            var totalBits = capacity * 8;
            var bits = new List<bool>(totalBits);

            AppendBits(bits, 0x4, 4);                       // 0100：字节模式
            AppendBits(bits, data.Length, ByteCountBits(version));
            foreach (var b in data) AppendBits(bits, b, 8);

            AppendBits(bits, 0, Math.Min(4, totalBits - bits.Count));   // 结束符
            while (bits.Count % 8 != 0) bits.Add(false);                // 补齐到整字节

            for (var i = 0; bits.Count < totalBits; i++)                // 交替填充字节
            {
                AppendBits(bits, (i & 1) == 0 ? 0xEC : 0x11, 8);
            }

            var result = new byte[capacity];
            for (var i = 0; i < bits.Count; i++)
            {
                if (bits[i]) result[i >> 3] |= (byte)(0x80 >> (i & 7));
            }
            return result;
        }

        private static void AppendBits(List<bool> bits, int value, int count)
        {
            for (var i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        // ---- 纠错码与交织 ----

        /// <summary>按版本把纠错码字加到数据后面，再按标准交织成最终码字序列。</summary>
        private static byte[] AddErrorCorrection(byte[] data, int version)
        {
            var numBlocks = BlockCounts[version];
            var blockEccLength = EccCodewordsPerBlock[version];
            var rawCodewords = RawDataModules(version) / 8;
            var numShortBlocks = numBlocks - rawCodewords % numBlocks;
            var shortBlockLength = rawCodewords / numBlocks;

            var divisor = ReedSolomonDivisor(blockEccLength);
            var blocks = new byte[numBlocks][];
            var offset = 0;

            for (var i = 0; i < numBlocks; i++)
            {
                var isShort = i < numShortBlocks;
                var dataLength = shortBlockLength - blockEccLength + (isShort ? 0 : 1);
                var blockData = new byte[dataLength];
                Array.Copy(data, offset, blockData, 0, dataLength);
                offset += dataLength;

                // 短块补一个占位字节，让所有块等长，交织时再跳过这个位置
                var block = new byte[shortBlockLength + 1];
                Array.Copy(blockData, 0, block, 0, dataLength);
                var ecc = ReedSolomonRemainder(blockData, divisor);
                Array.Copy(ecc, 0, block, isShort ? dataLength + 1 : dataLength, blockEccLength);
                blocks[i] = block;
            }

            var result = new byte[rawCodewords];
            var position = 0;
            var placeholder = shortBlockLength - blockEccLength;
            for (var i = 0; i <= shortBlockLength; i++)
            {
                for (var j = 0; j < numBlocks; j++)
                {
                    if (i == placeholder && j < numShortBlocks) continue;
                    result[position++] = blocks[j][i];
                }
            }
            return result;
        }

        /// <summary>生成 degree 次的 RS 生成多项式，系数从高次到低次，去掉最高次项 1。</summary>
        private static byte[] ReedSolomonDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;

            var root = 1;
            for (var i = 0; i < degree; i++)
            {
                for (var j = 0; j < degree; j++)
                {
                    result[j] = Multiply(result[j], (byte)root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = Multiply((byte)root, 0x02);
            }
            return result;
        }

        private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (var b in data)
            {
                var factor = (byte)(b ^ result[0]);
                for (var i = 0; i < result.Length - 1; i++) result[i] = result[i + 1];
                result[result.Length - 1] = 0;
                for (var i = 0; i < divisor.Length; i++) result[i] ^= Multiply(divisor[i], factor);
            }
            return result;
        }

        /// <summary>GF(2^8)（模 0x11D）上的乘法。</summary>
        private static byte Multiply(byte x, byte y)
        {
            var z = 0;
            for (var i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        // ---- 画功能图形 ----

        private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction, int version)
        {
            var size = version * 4 + 17;

            for (var i = 0; i < size; i++)
            {
                SetFunction(modules, isFunction, 6, i, i % 2 == 0);
                SetFunction(modules, isFunction, i, 6, i % 2 == 0);
            }

            DrawFinderPattern(modules, isFunction, version, 3, 3);
            DrawFinderPattern(modules, isFunction, version, size - 4, 3);
            DrawFinderPattern(modules, isFunction, version, 3, size - 4);

            var positions = AlignmentPositions(version);
            var last = positions.Length - 1;
            for (var i = 0; i < positions.Length; i++)
            {
                for (var j = 0; j < positions.Length; j++)
                {
                    // 三个角已经被定位图形占了，跳过
                    if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0)) continue;
                    DrawAlignmentPattern(modules, isFunction, positions[i], positions[j]);
                }
            }

            // 格式信息先占位标记成功能模块，选出掩码后再画真正的内容
            DrawFormatBits(modules, isFunction, version, 0);
            DrawVersionBits(modules, isFunction, version);
        }

        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int version, int cx, int cy)
        {
            var size = version * 4 + 17;
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var x = cx + dx;
                    var y = cy + dy;
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;
                    var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFunction(modules, isFunction, x, y, distance != 2 && distance != 4);
                }
            }
        }

        private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int cx, int cy)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    SetFunction(modules, isFunction, cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            }
        }

        private static void SetFunction(bool[,] modules, bool[,] isFunction, int x, int y, bool dark)
        {
            modules[y, x] = dark;
            isFunction[y, x] = true;
        }

        /// <summary>校正图形的中心坐标，横纵一样，升序。</summary>
        private static int[] AlignmentPositions(int version)
        {
            if (version == 1) return new int[0];

            var size = version * 4 + 17;
            var count = version / 7 + 2;
            var step = (version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;

            var result = new int[count];
            for (var i = 0; i < count - 1; i++) result[i] = size - 7 - i * step;
            result[count - 1] = 6;
            Array.Reverse(result);
            return result;
        }

        private static void DrawCodewords(bool[,] modules, bool[,] isFunction, int version, byte[] codewords)
        {
            var size = version * 4 + 17;
            var bit = 0;
            var totalBits = codewords.Length * 8;

            for (var right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;                  // 跳过竖直定时图形那一列
                for (var vert = 0; vert < size; vert++)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        var x = right - j;
                        var upward = ((right + 1) & 2) == 0;
                        var y = upward ? size - 1 - vert : vert;
                        if (isFunction[y, x] || bit >= totalBits) continue;
                        modules[y, x] = ((codewords[bit >> 3] >> (7 - (bit & 7))) & 1) != 0;
                        bit++;
                    }
                }
            }
        }

        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, int version, int mask)
        {
            var size = version * 4 + 17;

            var data = EccFormatBitsM << 3 | mask;
            var remainder = data;
            for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
            var bits = (data << 10 | remainder) ^ 0x5412;

            for (var i = 0; i < 6; i++) SetFunction(modules, isFunction, 8, i, GetBit(bits, i));
            SetFunction(modules, isFunction, 8, 7, GetBit(bits, 6));
            SetFunction(modules, isFunction, 8, 8, GetBit(bits, 7));
            SetFunction(modules, isFunction, 7, 8, GetBit(bits, 8));
            for (var i = 9; i < 15; i++) SetFunction(modules, isFunction, 14 - i, 8, GetBit(bits, i));

            for (var i = 0; i < 8; i++) SetFunction(modules, isFunction, size - 1 - i, 8, GetBit(bits, i));
            for (var i = 8; i < 15; i++) SetFunction(modules, isFunction, 8, size - 15 + i, GetBit(bits, i));
            SetFunction(modules, isFunction, 8, size - 8, true);
        }

        private static void DrawVersionBits(bool[,] modules, bool[,] isFunction, int version)
        {
            if (version < 7) return;

            var size = version * 4 + 17;
            var remainder = version;
            for (var i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
            var bits = version << 12 | remainder;

            for (var i = 0; i < 18; i++)
            {
                var bit = GetBit(bits, i);
                var a = size - 11 + i % 3;
                var b = i / 3;
                SetFunction(modules, isFunction, a, b, bit);
                SetFunction(modules, isFunction, b, a, bit);
            }
        }

        private static bool GetBit(int value, int index)
        {
            return ((value >> index) & 1) != 0;
        }

        // ---- 掩码 ----

        private static int ChooseMask(bool[,] modules, bool[,] isFunction, int version)
        {
            var best = 0;
            var lowest = int.MaxValue;
            for (var mask = 0; mask < 8; mask++)
            {
                ApplyMask(modules, isFunction, mask);
                DrawFormatBits(modules, isFunction, version, mask);
                var penalty = PenaltyScore(modules, version);
                if (penalty < lowest)
                {
                    lowest = penalty;
                    best = mask;
                }
                ApplyMask(modules, isFunction, mask);       // 异或两次即还原
            }
            return best;
        }

        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            var size = modules.GetLength(0);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (!isFunction[y, x] && MaskBit(mask, x, y)) modules[y, x] = !modules[y, x];
                }
            }
        }

        private static bool MaskBit(int mask, int x, int y)
        {
            switch (mask)
            {
                case 0: return (x + y) % 2 == 0;
                case 1: return y % 2 == 0;
                case 2: return x % 3 == 0;
                case 3: return (x + y) % 3 == 0;
                case 4: return (x / 3 + y / 2) % 2 == 0;
                case 5: return x * y % 2 + x * y % 3 == 0;
                case 6: return (x * y % 2 + x * y % 3) % 2 == 0;
                default: return ((x + y) % 2 + x * y % 3) % 2 == 0;
            }
        }

        // ---- 掩码罚分 ----

        private static int PenaltyScore(bool[,] modules, int version)
        {
            var size = version * 4 + 17;
            var result = 0;

            for (var y = 0; y < size; y++)
            {
                result += LinePenalty(modules, size, true, y);
            }
            for (var x = 0; x < size; x++)
            {
                result += LinePenalty(modules, size, false, x);
            }

            for (var y = 0; y < size - 1; y++)
            {
                for (var x = 0; x < size - 1; x++)
                {
                    var c = modules[y, x];
                    if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1])
                    {
                        result += PenaltyN2;
                    }
                }
            }

            var dark = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (modules[y, x]) dark++;
                }
            }
            var total = size * size;
            var steps = Math.Max(0, (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1);
            return result + steps * PenaltyN4;
        }

        /// <summary>一行或一列的连续同色罚分与类定位图形罚分。</summary>
        private static int LinePenalty(bool[,] modules, int size, bool horizontal, int index)
        {
            var result = 0;
            var runColor = false;
            var runLength = 0;
            var history = new int[7];

            for (var i = 0; i < size; i++)
            {
                var dark = horizontal ? modules[index, i] : modules[i, index];
                if (dark == runColor)
                {
                    runLength++;
                    if (runLength == 5) result += PenaltyN1;
                    else if (runLength > 5) result += 1;
                }
                else
                {
                    AddHistory(history, runLength, size);
                    if (!runColor) result += CountFinderPatterns(history) * PenaltyN3;
                    runColor = dark;
                    runLength = 1;
                }
            }
            return result + TerminateLine(history, runColor, runLength, size) * PenaltyN3;
        }

        /// <summary>把刚结束的一段长度记进历史（长度不足 7 段时，用静区补足首段的光边界）。</summary>
        private static void AddHistory(int[] history, int runLength, int size)
        {
            if (history[0] == 0) runLength += size;
            for (var i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = runLength;
        }

        /// <summary>1:1:3:1:1 的比例，两侧还要各有 4 倍长度的浅色区，返回命中次数（0-2）。</summary>
        private static int CountFinderPatterns(int[] history)
        {
            var n = history[1];
            var core = n > 0 && history[2] == n && history[4] == n && history[5] == n && history[3] == n * 3;
            var count = 0;
            if (core && history[0] >= n * 4 && history[6] >= n) count++;
            if (core && history[6] >= n * 4 && history[0] >= n) count++;
            return count;
        }

        private static int TerminateLine(int[] history, bool runColor, int runLength, int size)
        {
            if (runColor)
            {
                AddHistory(history, runLength, size);
                runLength = 0;
            }
            runLength += size;                              // 末尾也按浅色边界处理
            AddHistory(history, runLength, size);
            return CountFinderPatterns(history);
        }
    }
}
