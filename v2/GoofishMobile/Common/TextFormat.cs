using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Goofish.Common
{
    public static class TextFormat
    {
        private static readonly CultureInfo Zh = new CultureInfo("zh-CN");

        public static string Price(decimal value)
        {
            if (value == decimal.Truncate(value)) return "¥" + value.ToString("#,0", CultureInfo.InvariantCulture);
            return "¥" + value.ToString("#,0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 从“¥1,299”“1299元”“1.2万”这类文本里解析价格。
        /// 先认货币符号后的数字；否则要求至少两位数且两侧不紧挨字母，避免把“M2”“1440P”当成价格。
        /// </summary>
        public static decimal ParsePrice(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var compact = Regex.Replace(text, @"[,，\s]", "");
            decimal value;

            var wan = Regex.Match(compact, @"(\d+(?:\.\d+)?)万");
            if (wan.Success && decimal.TryParse(wan.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return value * 10000;

            var anchored = Regex.Match(compact, @"[¥￥](\d+(?:\.\d+)?)");
            if (anchored.Success && decimal.TryParse(anchored.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return value;

            if (decimal.TryParse(compact, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return value;

            var bare = Regex.Match(compact, @"(?:^|[^\dA-Za-z])(\d{2,}(?:\.\d+)?)(?![A-Za-z\d])");
            if (bare.Success && decimal.TryParse(bare.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return value;
            return 0;
        }

        public static DateTimeOffset FromUnix(long value)
        {
            if (value <= 0) return DateTimeOffset.MinValue;
            // 秒级时间戳与毫秒级时间戳都会出现
            return value < 100000000000L ? DateTimeOffset.FromUnixTimeSeconds(value) : DateTimeOffset.FromUnixTimeMilliseconds(value);
        }

        /// <summary>W10M 消息应用的时间写法：今天只显示时刻，昨天、本周显示星期，更早显示日期。</summary>
        public static string ConversationTime(DateTimeOffset time)
        {
            if (time == DateTimeOffset.MinValue) return string.Empty;
            var local = time.ToLocalTime();
            var today = DateTimeOffset.Now.Date;
            if (local.Date == today) return local.ToString("HH:mm", Zh);
            if (local.Date == today.AddDays(-1)) return "昨天";
            if (local.Date > today.AddDays(-7)) return Zh.DateTimeFormat.GetAbbreviatedDayName(local.DayOfWeek);
            return local.Year == today.Year ? local.ToString("M月d日", Zh) : local.ToString("yyyy/M/d", Zh);
        }

        public static string MessageTime(DateTimeOffset time)
        {
            if (time == DateTimeOffset.MinValue) return string.Empty;
            var local = time.ToLocalTime();
            var today = DateTimeOffset.Now.Date;
            if (local.Date == today) return local.ToString("HH:mm", Zh);
            if (local.Date == today.AddDays(-1)) return "昨天 " + local.ToString("HH:mm", Zh);
            return local.ToString("M月d日 HH:mm", Zh);
        }

        public static string Relative(DateTimeOffset time)
        {
            if (time == DateTimeOffset.MinValue) return string.Empty;
            var span = DateTimeOffset.Now - time;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + "分钟前";
            if (span.TotalDays < 1) return (int)span.TotalHours + "小时前";
            if (span.TotalDays < 30) return (int)span.TotalDays + "天前";
            return time.ToLocalTime().ToString("yyyy年M月d日", Zh);
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (url.StartsWith("//")) return "https:" + url;
            if (url.StartsWith("http://")) return "https://" + url.Substring(7);
            return url;
        }

        /// <summary>
        /// 阿里 CDN 支持在原图地址后追加 "_宽x高.jpg" 取缩略图。省流量模式下列表只拉小图。
        /// </summary>
        public static string Thumbnail(string url, int size)
        {
            url = NormalizeUrl(url);
            if (url == null || size <= 0) return url;
            if (url.IndexOf("alicdn.com", StringComparison.OrdinalIgnoreCase) < 0) return url;
            if (Regex.IsMatch(url, @"_\d+x\d+[^/]*\.(jpg|webp|png)$", RegexOptions.IgnoreCase)) return url;
            return url + "_" + size + "x" + size + ".jpg";
        }
    }
}
