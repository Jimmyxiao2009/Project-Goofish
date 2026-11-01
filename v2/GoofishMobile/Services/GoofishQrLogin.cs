using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Goofish.Common;
using Newtonsoft.Json.Linq;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace Goofish.Services
{
    /// <summary>扫码状态。</summary>
    public enum QrLoginState
    {
        /// <summary>二维码已生成，等用户扫。</summary>
        Waiting,

        /// <summary>已经被扫，等用户在手机上点确认。</summary>
        Scanned,

        /// <summary>已经确认，可以换取登录态。</summary>
        Confirmed,

        /// <summary>服务端说二维码过期了。</summary>
        Expired,

        /// <summary>用户在手机上取消了这次登录。</summary>
        Canceled
    }

    /// <summary>一次轮询的结果。</summary>
    public sealed class QrPollResult
    {
        internal QrPollResult(QrLoginState state, string token)
        {
            State = state;
            Token = token;
        }

        public QrLoginState State { get; }

        /// <summary>确认后用于换取登录态的令牌；未确认时为空串。</summary>
        public string Token { get; }
    }

    /// <summary>
    /// 电脑版扫码登录。
    ///
    /// 手机上那个 WebView 打不开闲鱼的登录页：W10M 的 Edge 内核太老，验证码要走的人机校验直接报错。
    /// 这里改成纯 HTTP 的电脑版（PC Web）扫码链路：以 PC 版 UA 调 passport 的二维码接口，
    /// 把二维码画给用户，用户用另一台设备上的闲鱼 App 扫码确认，再把服务端下发的会话 Cookie 交给
    /// <see cref="MtopClient"/>。全程不启动浏览器，所以也不受 WebView 内核版本影响。
    ///
    /// 接口顺序（参数取自闲鱼网页版的实际请求，已实测）：
    /// <list type="number">
    /// <item>GET  /mini_login.htm 拿基础 Cookie（XSRF-TOKEN 等）</item>
    /// <item>GET  /newlogin/qrcode/generate.do 拿 codeContent / t / ck</item>
    /// <item>POST /newlogin/qrcode/query.do 轮询 qrCodeStatus，直到 CONFIRMED</item>
    /// <item>POST /login_token/login.do 用令牌正式建立会话，此时 Set-Cookie 才带 unb / cookie2</item>
    /// </list>
    ///
    /// 两个坑记在这里：轮询的 <c>t</c> 必须用 generate.do 返回的那个（用当前时间戳会被判成 EXPIRED）；
    /// query.do 必须带 <c>ck</c>，否则直接 500。
    /// </summary>
    public sealed class GoofishQrLogin : IDisposable
    {
        private const string PassportHost = "https://passport.goofish.com";
        private const string CommonQuery =
            "appName=xianyu&fromSite=77&appEntrance=web&mainPage=false&isMobile=false&lang=zh_CN&returnUrl=&umidTag=SERVER";

        /// <summary>登录后要搬进 mtop Cookie 容器的会话凭据。服务端把它们下在 .goofish.com 上，这里按同样的域搬。</summary>
        private static readonly string[] SessionCookies =
        {
            "unb", "cookie2", "cookie17", "munb", "sgcookie", "_tb_token_", "t", "tracknick",
            "_m_h5_tk", "_m_h5_tk_enc", "_samesite_flag_", "cna", "lgc", "isg", "xlly_s", "tfstk",
            "havana_lgc2_77", "uc1", "uc3", "uc4"
        };

        private static readonly Uri PassportUri = new Uri(PassportHost + "/");
        private static readonly Uri SiteUri = new Uri("https://www.goofish.com/");
        private static readonly Uri GatewayUri = new Uri("https://h5api.m.goofish.com/");

        private readonly MtopClient _mtop;
        private readonly HttpBaseProtocolFilter _filter;
        private readonly HttpClient _http;
        private readonly string _deviceId;
        private string _t;
        private string _ck;

        public GoofishQrLogin(MtopClient mtop)
        {
            _mtop = mtop;
            _filter = new HttpBaseProtocolFilter();
            _filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            _filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;
            _http = new HttpClient(_filter);
            _deviceId = NewDeviceId();
        }

        /// <summary>二维码里要放的内容（形如 https://passport.goofish.com/qrcodeCheck.htm?lgToken=...）。</summary>
        public string CodeContent { get; private set; }

        /// <summary>取一张新二维码。返回要编码成二维码的字符串。</summary>
        public async Task<string> StartAsync(CancellationToken cancel)
        {
            // 第 1 步：登录页会下发 XSRF-TOKEN、cookie2 等基础 Cookie
            var landing = PassportHost + "/mini_login.htm?lang=zh_cn&appName=xianyu&appEntrance=web" +
                          "&styleType=vertical&bizParams=&notLoadSsoView=false&notKeepLogin=false" +
                          "&isMobile=false&qrCodeFirst=false&site=77&rnd=" +
                          new Random().NextDouble().ToString("0.############", System.Globalization.CultureInfo.InvariantCulture);
            await SendAsync(HttpMethod.Get, landing, null, SiteUri, cancel);

            // 第 2 步：取二维码。_csrf_token / hsiz 实测可以不传，但按浏览器的真实请求带上，免得以后被收紧
            var csrf = GetCookie("XSRF-TOKEN", PassportUri);
            var cookie2 = GetCookie("cookie2", SiteUri) ?? GetCookie("cookie2", PassportUri);
            var query = CommonQuery
                        + (string.IsNullOrEmpty(csrf) ? string.Empty : "&_csrf_token=" + Uri.EscapeDataString(csrf))
                        + (string.IsNullOrEmpty(cookie2) ? string.Empty : "&hsiz=" + Uri.EscapeDataString(cookie2))
                        + "&umidToken=&bizParams=";

            var json = await SendAsync(HttpMethod.Get, PassportHost + "/newlogin/qrcode/generate.do?" + query,
                null, new Uri(PassportHost + "/mini_login.htm"), cancel);

            var data = ParseContentData(json, "生成二维码失败");
            _t = data.Str("t");
            _ck = data.Str("ck");
            CodeContent = data.Str("codeContent");

            if (string.IsNullOrEmpty(CodeContent) || string.IsNullOrEmpty(_t) || string.IsNullOrEmpty(_ck))
            {
                throw new MtopException(MtopErrorKind.Api, "QR_GENERATE", "闲鱼没有返回二维码，请稍后重试。");
            }
            return CodeContent;
        }

        /// <summary>轮询一次扫码状态。</summary>
        public async Task<QrPollResult> PollAsync(CancellationToken cancel)
        {
            if (CodeContent == null) throw new InvalidOperationException("还没有调用 StartAsync 取二维码。");

            var body = CommonQuery
                       + "&t=" + Uri.EscapeDataString(_t)
                       + "&ck=" + Uri.EscapeDataString(_ck)
                       + "&deviceId=" + Uri.EscapeDataString(_deviceId)
                       + "&navlanguage=zh-CN"
                       + "&navUserAgent=" + Uri.EscapeDataString(MtopClient.UserAgent)
                       + "&navPlatform=Win32&isIframe=true&defaultView=sms"
                       + "&documentReferer=" + Uri.EscapeDataString("https://www.goofish.com/");

            var json = await SendAsync(HttpMethod.Post,
                PassportHost + "/newlogin/qrcode/query.do?appName=xianyu&fromSite=77", body,
                new Uri(PassportHost + "/mini_login.htm"), cancel);

            var data = ParseContentData(json, "查询扫码状态失败");
            var status = data.Str("qrCodeStatus") ?? string.Empty;
            // 令牌字段名在各实现之间有漂移，四个都兜一遍
            var token = data.Str("token") ?? data.Str("lgToken") ?? data.Str("st") ?? data.Str("stEx") ?? string.Empty;

            // 状态值连拼写都不统一（SCANNED / SCANED），所以只认 CONFIRMED、EXPIRED、CANCELED，其余都当继续等
            if (status.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
                return new QrPollResult(QrLoginState.Confirmed, token);
            if (status.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase))
                return new QrPollResult(QrLoginState.Expired, string.Empty);
            if (status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase))
                return new QrPollResult(QrLoginState.Canceled, string.Empty);
            if (status.IndexOf("SCAN", StringComparison.OrdinalIgnoreCase) >= 0)
                return new QrPollResult(QrLoginState.Scanned, string.Empty);

            return new QrPollResult(QrLoginState.Waiting, string.Empty);
        }

        /// <summary>
        /// 用确认令牌正式建立会话，并把会话 Cookie 搬进 <see cref="MtopClient"/>。
        /// 返回是否拿到了登录必需的 unb 与 cookie2；失败时不会动已有的 Cookie。
        /// </summary>
        public async Task<bool> FinishAsync(string token, CancellationToken cancel)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var query = "token=" + Uri.EscapeDataString(token)
                            + "&subFlow=DIALOG_CHECK_LOGIN_RPC&nextCode=0018&bizScene=qrcode&confirm=true";
                await SendAsync(HttpMethod.Post, PassportHost + "/login_token/login.do?" + query,
                    "deviceId=" + Uri.EscapeDataString(_deviceId), new Uri(PassportHost + "/mini_login.htm"), cancel);
            }

            ImportSessionCookies();

            // cookie2 在登录页那一步就会给，只有 unb 出现才算真的登录成功
            return !string.IsNullOrEmpty(GetCookie("unb", SiteUri))
                   && !string.IsNullOrEmpty(GetCookie("cookie2", SiteUri));
        }

        public void Dispose()
        {
            _http.Dispose();
            _filter.Dispose();
        }

        /// <summary>把扫码会话里的会话 Cookie 按 .goofish.com 的域搬进 mtop 的容器。</summary>
        private void ImportSessionCookies()
        {
            var seen = new HashSet<string>();
            foreach (var scope in new[] { PassportUri, SiteUri, GatewayUri })
            {
                foreach (var cookie in _filter.CookieManager.GetCookies(scope))
                {
                    if (Array.IndexOf(SessionCookies, cookie.Name) < 0 || !seen.Add(cookie.Name)) continue;
                    try
                    {
                        _mtop.Cookies.SetCookie(new HttpCookie(cookie.Name, ".goofish.com", "/")
                        {
                            Value = cookie.Value,
                            HttpOnly = cookie.HttpOnly,
                            Secure = true,
                            Expires = DateTimeOffset.Now.AddDays(30)
                        }, false);
                    }
                    catch (Exception ex)
                    {
                        // 个别 Cookie 属性不被容器接受时跳过：真正要紧的 unb / cookie2 后面还会校验
                        System.Diagnostics.Debug.WriteLine("Cookie " + cookie.Name + " 写入失败：" + ex.Message);
                    }
                }
            }
        }

        private string GetCookie(string name, Uri scope)
        {
            return _filter.CookieManager.GetCookies(scope).FirstOrDefault(c => c.Name == name)?.Value;
        }

        private static JToken ParseContentData(string json, string errorTitle)
        {
            JObject payload;
            try
            {
                payload = JObject.Parse(json);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                throw new MtopException(MtopErrorKind.Api, "QR_PARSE", errorTitle + "：闲鱼返回了无法解析的内容。");
            }

            var data = payload.SelectToken("content.data");
            if (data == null || !(payload.SelectToken("content.success")?.Value<bool>() ?? false))
            {
                throw new MtopException(MtopErrorKind.Api, "QR_PARSE", errorTitle + "：闲鱼拒绝了这次请求。");
            }
            return data;
        }

        private async Task<string> SendAsync(HttpMethod method, string url, string body, Uri referer, CancellationToken cancel)
        {
            var request = new HttpRequestMessage(method, new Uri(url));
            if (body != null)
            {
                request.Content = new HttpStringContent(body, Windows.Storage.Streams.UnicodeEncoding.Utf8,
                    "application/x-www-form-urlencoded");
            }
            request.Headers.TryAppendWithoutValidation("User-Agent", MtopClient.UserAgent);
            request.Headers.TryAppendWithoutValidation("Accept", "application/json, text/plain, */*");
            request.Headers.TryAppendWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            request.Headers.TryAppendWithoutValidation("Referer", referer.ToString());
            request.Headers.TryAppendWithoutValidation("Origin", PassportHost);

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

                using (response)
                {
                    var text = await response.Content.ReadAsStringAsync().AsTask(timeout.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new MtopException(MtopErrorKind.Api, "QR_HTTP",
                            "闲鱼返回了 HTTP " + (int)response.StatusCode + "，请稍后重试。");
                    }
                    return text;
                }
            }
        }

        /// <summary>轮询用的设备标识：16 位随机十六进制 + 8 位时间戳，和网页版的 cna 格式一致。</summary>
        private static string NewDeviceId()
        {
            var random = new Random();
            var bytes = new byte[8];
            var chars = new char[24];
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = random.Next(256);
                chars[i * 2] = Hex(value >> 4);
                chars[i * 2 + 1] = Hex(value & 0xF);
            }
            var seconds = (uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFFFFFF);
            for (var i = 0; i < 8; i++) chars[16 + i] = Hex((int)(seconds >> (28 - i * 4)) & 0xF);
            return new string(chars);
        }

        private static char Hex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + value - 10);
        }
    }
}
