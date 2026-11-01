using System;
using System.Text.RegularExpressions;

namespace Goofish.Services
{
    public enum MtopErrorKind
    {
        /// <summary>未登录或登录过期，需要重新登录。</summary>
        Auth,
        /// <summary>风控要求先过人机验证（滑块）。重登不一定能解，通常要去网页版过一次。</summary>
        Verify,
        /// <summary>RGV587 / 被挤爆：请求太频繁，或风控要求过滑块。</summary>
        Throttled,
        /// <summary>风控直接拒绝（punish 页 action=deny），重试无效。</summary>
        Denied,
        /// <summary>令牌握手两次都失败。</summary>
        Token,
        Network,
        Api
    }

    public sealed class MtopException : Exception
    {
        public MtopException(MtopErrorKind kind, string ret, string message, Exception inner = null)
            : base(message, inner)
        {
            Kind = kind;
            Ret = ret;
        }

        public MtopErrorKind Kind { get; }

        /// <summary>网关原始 ret[0]，如 "FAIL_SYS_SESSION_EXPIRED::Session过期"。</summary>
        public string Ret { get; }

        public bool NeedsLogin => Kind == MtopErrorKind.Auth || Kind == MtopErrorKind.Token;

        /// <summary>
        /// 按 ret 与 data.url 分类。判断顺序：punish 直接拒绝 → FAIL_SYS_USER_VALIDATE（风控人机验证）
        /// → RGV587（限流）→ SESSION_EXPIRED（真失效）→ TOKEN。FAIL_SYS_USER_VALIDATE 常和 RGV587_ERROR
        /// 一起返回（“被挤爆”），它其实是风控要过滑块，不是登录过期，所以归到 Verify。
        /// </summary>
        public static MtopException FromRet(string ret, string redirectUrl)
        {
            ret = ret ?? string.Empty;
            redirectUrl = redirectUrl ?? string.Empty;
            var message = ret.Contains("::") ? ret.Substring(ret.IndexOf("::", StringComparison.Ordinal) + 2) : ret;

            if (Regex.IsMatch(redirectUrl, "/punish/", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(redirectUrl, "[?&]action=deny(&|$)", RegexOptions.IgnoreCase))
                return new MtopException(MtopErrorKind.Denied, ret, "闲鱼拒绝了这次访问。请在浏览器打开 goofish.com 完成验证后再试。");

            if (Regex.IsMatch(ret, "FAIL_SYS_USER_VALIDATE|USER_VALIDATE", RegexOptions.IgnoreCase))
                return new MtopException(MtopErrorKind.Verify, ret,
                    "闲鱼要求先完成人机验证（风控）。请用电脑浏览器登录 goofish.com、打开消息页过掉滑块，再用「粘贴 Cookie」重新登录。服务端返回：" + ret);

            if (Regex.IsMatch(ret, "RGV587|被挤爆|TRAFFIC_LIMIT|访问过于频繁|请求过于频繁|FAIL_SYS_TRAFFIC", RegexOptions.IgnoreCase))
                return new MtopException(MtopErrorKind.Throttled, ret, "闲鱼说请求太频繁了，请稍等一会儿再试。服务端返回：" + ret);

            if (Regex.IsMatch(ret, "SESSION_EXPIRED|SESSION_INVALID|未登录|请先登录", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(redirectUrl, @"passport\.|mini_login", RegexOptions.IgnoreCase))
                return new MtopException(MtopErrorKind.Auth, ret, "登录已失效，请重新登录闲鱼账号。服务端返回：" + ret);

            if (Regex.IsMatch(ret, "FAIL_SYS_TOKEN", RegexOptions.IgnoreCase))
                return new MtopException(MtopErrorKind.Token, ret, "无法获取闲鱼访问令牌，请重新登录。");

            return new MtopException(MtopErrorKind.Api, ret, string.IsNullOrEmpty(message) ? "闲鱼返回了无法识别的结果。" : message);
        }

        /// <summary>给界面用的一句话错误说明。</summary>
        public static string Describe(Exception ex)
        {
            var mtop = ex as MtopException;
            if (mtop != null) return mtop.Message;
            return "出错了：" + ex.Message;
        }
    }
}
