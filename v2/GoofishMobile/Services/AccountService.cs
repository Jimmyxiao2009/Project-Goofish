using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Windows.Web.Http;

namespace Goofish.Services
{
    /// <summary>
    /// 登录态完全由 Cookie 承载：unb 是用户 ID，cookie2 是会话，_m_h5_tk 是 mtop 签名令牌。
    /// </summary>
    public sealed class AccountService
    {
        private static readonly Uri[] CookieScopes =
        {
            new Uri("https://www.goofish.com/"),
            new Uri("https://h5api.m.goofish.com/"),
            new Uri("https://passport.goofish.com/"),
            new Uri("https://www.taobao.com/"),
            new Uri("https://h5api.m.taobao.com/")
        };

        private readonly MtopClient _mtop;
        private readonly AppSettings _settings;

        public AccountService(MtopClient mtop, AppSettings settings)
        {
            _mtop = mtop;
            _settings = settings;
        }

        public event EventHandler SignedInChanged;

        public string UserId => _mtop.GetCookie("unb", MtopClient.SiteUri);

        public bool IsSignedIn => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(_mtop.GetCookie("cookie2", MtopClient.SiteUri));

        public AccountInfo Info => new AccountInfo
        {
            UserId = UserId,
            Nick = _settings.AccountNick ?? _mtop.GetCookie("tracknick", MtopClient.SiteUri) ?? "闲鱼用户",
            AvatarUrl = _settings.AccountAvatar
        };

        /// <summary>IM 的设备 ID 与用户绑定并持久化，换设备 ID 会被服务端当成新设备。</summary>
        public string DeviceId
        {
            get
            {
                var uid = UserId ?? "0";
                var id = _settings.DeviceIdFor(uid);
                if (!string.IsNullOrEmpty(id)) return id;
                id = NewDeviceId(uid);
                _settings.SetDeviceId(uid, id);
                return id;
            }
        }

        public void NotifyChanged()
        {
            SignedInChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>导入从浏览器复制的 Cookie 串（name=value; name2=value2）。返回是否拿到了登录必需的 unb 与 cookie2。</summary>
        public bool ImportCookies(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var text = raw.Trim();
            if (text.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(7);

            foreach (var part in text.Split(';'))
            {
                var index = part.IndexOf('=');
                if (index <= 0) continue;
                var name = part.Substring(0, index).Trim();
                var value = part.Substring(index + 1).Trim();
                if (name.Length == 0) continue;
                var cookie = new HttpCookie(name, ".goofish.com", "/")
                {
                    Value = value,
                    Secure = false,
                    Expires = DateTimeOffset.Now.AddDays(30)
                };
                _mtop.Cookies.SetCookie(cookie, false);
            }
            var ok = IsSignedIn;
            if (ok) NotifyChanged();
            return ok;
        }

        public async Task<AccountInfo> RefreshProfileAsync()
        {
            var data = await _mtop.CallAsync("mtop.idle.web.user.page.nav", "1.0", new object());
            var nick = data.Str("module.base.displayName");
            var avatar = TextFormat.NormalizeUrl(data.Str("module.base.avatar"));
            if (!string.IsNullOrEmpty(nick)) _settings.AccountNick = nick;
            if (!string.IsNullOrEmpty(avatar)) _settings.AccountAvatar = avatar;
            return Info;
        }

        public void SignOut()
        {
            foreach (var scope in CookieScopes)
            {
                foreach (var cookie in _mtop.Cookies.GetCookies(scope).ToList())
                {
                    _mtop.Cookies.DeleteCookie(cookie);
                }
            }
            _settings.AccountNick = null;
            _settings.AccountAvatar = null;
            NotifyChanged();
        }

        private static string NewDeviceId(string userId)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var random = new Random();
            var sb = new StringBuilder();
            for (var i = 0; i < 36; i++)
            {
                if (i == 8 || i == 13 || i == 18 || i == 23) sb.Append('-');
                else if (i == 14) sb.Append('4');
                else if (i == 19) sb.Append(chars[(random.Next(16) & 0x3) | 0x8]);
                else sb.Append(chars[random.Next(16)]);
            }
            return sb + "-" + userId;
        }
    }
}
