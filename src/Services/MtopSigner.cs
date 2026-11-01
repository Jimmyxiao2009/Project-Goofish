using System;
using System.Text;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace ProjectGoofish.Services
{
    /// <summary>
    /// 阿里巴巴 MTOP 移动网关 MD5 签名生成器
    /// 遵循算法：Sign = MD5(token + "&" + t + "&" + appKey + "&" + data)
    /// </summary>
    public static class MtopSigner
    {
        private static readonly HashAlgorithmProvider Md5Provider = 
            HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);

        /// <summary>
        /// 计算请求签名
        /// </summary>
        /// <param name="token">从 _m_h5_tk 中提取的前半段Token</param>
        /// <param name="timestamp">当前毫秒级时间戳 (t)</param>
        /// <param name="appKey">应用标识 (如 12574478)</param>
        /// <param name="data">请求业务 JSON 字符串</param>
        /// <returns>32位小写 MD5 签名字符串</returns>
        public static string ComputeSign(string token, string timestamp, string appKey, string data)
        {
            var tokenPart = token ?? string.Empty;
            var rawSignatureString = $"{tokenPart}&{timestamp}&{appKey}&{data}";

            var buffer = CryptographicBuffer.ConvertStringToBinary(rawSignatureString, BinaryStringEncoding.Utf8);
            var hashBuffer = Md5Provider.HashData(buffer);

            return CryptographicBuffer.EncodeToHexString(hashBuffer).ToLowerInvariant();
        }

        /// <summary>
        /// 从 Set-Cookie 标头或 Cookie 字符串中解析 _m_h5_tk 的有效 Token
        /// 例如 "_m_h5_tk=abcdef1234567890_1712349999999; path=/;" 提取为 "abcdef1234567890"
        /// </summary>
        public static string ExtractTokenFromCookie(string cookieHeader)
        {
            if (string.IsNullOrWhiteSpace(cookieHeader)) return string.Empty;

            var parts = cookieHeader.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Trim().Split('=');
                if (kv.Length >= 2 && kv[0].Trim() == "_m_h5_tk")
                {
                    var val = kv[1].Trim();
                    var underScoreIndex = val.IndexOf('_');
                    return underScoreIndex > 0 ? val.Substring(0, underScoreIndex) : val;
                }
            }

            return string.Empty;
        }
    }
}
