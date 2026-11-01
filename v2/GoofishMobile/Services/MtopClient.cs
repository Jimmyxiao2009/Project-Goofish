using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace Goofish.Services
{
    /// <summary>
    /// 闲鱼网页版使用的 mtop H5 网关客户端。
    ///
    /// 签名：sign = md5(token + "&amp;" + t + "&amp;" + appKey + "&amp;" + data)，token 取 Cookie _m_h5_tk 第一个 "_" 之前的部分。
    /// 首次请求没有 _m_h5_tk 时网关返回 FAIL_SYS_TOKEN_EMPTY 并通过 Set-Cookie 下发令牌，重签一次即可。
    ///
    /// HttpBaseProtocolFilter 与 WebView 共用同一个 Cookie 存储，所以登录页里拿到的 Cookie 这里直接可用。
    /// </summary>
    public sealed class MtopClient
    {
        public const string AppKey = "34839810";
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";

        private const string Gateway = "https://h5api.m.goofish.com/h5/";
        private const string Site = "https://www.goofish.com";

        // 实测短时间连发 3 次搜索就可能触发 RGV587，所有调用串行并保持最小间隔
        private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(600);

        private static readonly HashAlgorithmProvider Md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);

        private readonly HttpBaseProtocolFilter _filter;
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private DateTimeOffset _lastCall = DateTimeOffset.MinValue;

        public MtopClient()
        {
            _filter = new HttpBaseProtocolFilter();
            _filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            _filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;
            _http = new HttpClient(_filter);
        }

        public HttpCookieManager Cookies => _filter.CookieManager;

        public static readonly Uri GatewayUri = new Uri("https://h5api.m.goofish.com/");
        public static readonly Uri SiteUri = new Uri("https://www.goofish.com/");

        public string GetCookie(string name, Uri scope = null)
        {
            var cookie = Cookies.GetCookies(scope ?? GatewayUri).FirstOrDefault(c => c.Name == name);
            return cookie?.Value;
        }

        public string GetCookieHeader(Uri scope)
        {
            var seen = new HashSet<string>();
            var parts = new List<string>();
            foreach (var c in Cookies.GetCookies(scope))
            {
                if (seen.Add(c.Name)) parts.Add(c.Name + "=" + c.Value);
            }
            return string.Join("; ", parts);
        }

        private static string Header(IDictionary<string, string> headers, string name)
        {
            if (headers == null) return null;
            string value;
            return headers.TryGetValue(name, out value) ? value : null;
        }

        public Task<JToken> CallAsync(string api, string version, object data, CancellationToken cancel = default(CancellationToken))
        {
            return CallAsync(api, version, data, null, cancel);
        }

        /// <summary>
        /// extraHeaders 用来覆盖 Origin / Referer 或补自定义请求头。
        /// 卖家端的订单接口要求 Origin 是 seller.goofish.com，还要带 idle_site_biz_code。
        /// </summary>
        public Task<JToken> CallAsync(string api, string version, object data, IDictionary<string, string> extraHeaders, CancellationToken cancel = default(CancellationToken))
        {
            var json = data as string ?? JsonConvert.SerializeObject(data ?? new object());
            return CallRawAsync(api, version, json, extraHeaders, cancel);
        }

        private async Task<JToken> CallRawAsync(string api, string version, string dataJson, IDictionary<string, string> extraHeaders, CancellationToken cancel)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var payload = await SendAsync(api, version, dataJson, extraHeaders, cancel);
                // ret 是数组（如 ["FAIL_SYS_USER_VALIDATE","RGV587_ERROR::..."]），只看第一项会漏掉
                // 第二项里的风控信息，所以拼起来再交给 MtopException 分类。
                var retArray = payload["ret"] as JArray;
                var ret = retArray == null
                    ? string.Empty
                    : string.Join(" ", retArray.Select(t => (string)t).Where(s => !string.IsNullOrEmpty(s)));

                if (ret.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    return payload["data"] ?? new JObject();

                // 令牌为空或过期属于握手：响应已经通过 Set-Cookie 带回新令牌，重签一次。
                // 服务端会把 EXPIRED 拼成 EXOIRED，所以只按前缀判断。
                if (ret.IndexOf("FAIL_SYS_TOKEN", StringComparison.OrdinalIgnoreCase) >= 0 && attempt == 1)
                    continue;

                var url = payload.SelectToken("data.url") as JValue;
                throw MtopException.FromRet(ret, url?.ToString());
            }
            throw new MtopException(MtopErrorKind.Token, "FAIL_SYS_TOKEN", "无法获取闲鱼访问令牌，请重新登录。");
        }

        private async Task<JObject> SendAsync(string api, string version, string dataJson, IDictionary<string, string> extraHeaders, CancellationToken cancel)
        {
            await _gate.WaitAsync(cancel);
            try
            {
                var wait = _lastCall + MinGap - DateTimeOffset.Now;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancel);

                var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var token = TokenOf(GetCookie("_m_h5_tk"));
                var sign = Sign(token, t, AppKey, dataJson);

                var query = new Dictionary<string, string>
                {
                    { "jsv", "2.7.2" },
                    { "appKey", AppKey },
                    { "t", t },
                    { "sign", sign },
                    { "v", version },
                    { "type", "originaljson" },
                    { "accountSite", "xianyu" },
                    { "dataType", "json" },
                    { "timeout", "20000" },
                    { "api", api },
                    { "sessionOption", "AutoLoginOnly" }
                };
                var uri = new Uri(Gateway + api + "/" + version + "/?" +
                                  string.Join("&", query.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value))));

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new HttpFormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", dataJson) })
                };
                request.Headers.TryAppendWithoutValidation("User-Agent", UserAgent);
                request.Headers.TryAppendWithoutValidation("Accept", "application/json");
                var origin = Header(extraHeaders, "Origin") ?? Site;
                var referer = Header(extraHeaders, "Referer") ?? Site;
                request.Headers.TryAppendWithoutValidation("Origin", origin);
                request.Headers.Referer = new Uri(referer);
                if (extraHeaders != null)
                {
                    foreach (var header in extraHeaders)
                    {
                        if (string.Equals(header.Key, "Origin", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(header.Key, "Referer", StringComparison.OrdinalIgnoreCase)) continue;
                        request.Headers.TryAppendWithoutValidation(header.Key, header.Value);
                    }
                }

                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    HttpResponseMessage response;
                    try
                    {
                        response = await _http.SendRequestAsync(request).AsTask(timeout.Token);
                    }
                    catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                    {
                        throw new MtopException(MtopErrorKind.Network, null, "连接闲鱼超时，请检查网络。");
                    }
                    catch (COMException ex)
                    {
                        throw new MtopException(MtopErrorKind.Network, null, "无法连接到闲鱼，请检查网络。", ex);
                    }
                    finally
                    {
                        _lastCall = DateTimeOffset.Now;
                    }

                    using (response)
                    {
                        var text = await response.Content.ReadAsStringAsync().AsTask(timeout.Token);
                        try
                        {
                            return JObject.Parse(text);
                        }
                        catch (JsonException)
                        {
                            throw new MtopException(MtopErrorKind.Api, null,
                                "闲鱼返回了无法解析的内容（HTTP " + (int)response.StatusCode + "）。");
                        }
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public static string Sign(string token, string t, string appKey, string data)
        {
            var raw = (token ?? string.Empty) + "&" + t + "&" + appKey + "&" + data;
            var buffer = CryptographicBuffer.ConvertStringToBinary(raw, BinaryStringEncoding.Utf8);
            return CryptographicBuffer.EncodeToHexString(Md5.HashData(buffer)).ToLowerInvariant();
        }

        public static string TokenOf(string cookieValue)
        {
            if (string.IsNullOrEmpty(cookieValue)) return string.Empty;
            var index = cookieValue.IndexOf('_');
            return index > 0 ? cookieValue.Substring(0, index) : cookieValue;
        }
    }
}
