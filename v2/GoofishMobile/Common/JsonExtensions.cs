using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Goofish.Common
{
    /// <summary>
    /// 闲鱼接口没有公开文档，字段随版本漂移。这里的取值函数都按“多个候选路径取第一个有值的”来写，
    /// 取不到就返回默认值，不抛异常。
    /// </summary>
    public static class JsonExtensions
    {
        public static JToken At(this JToken token, string path)
        {
            if (token == null) return null;
            try
            {
                var found = token.SelectToken(path, false);
                return found == null || found.Type == JTokenType.Null || found.Type == JTokenType.Undefined ? null : found;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        public static string Str(this JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var value = token.At(path);
                if (value == null) continue;
                if (value.Type == JTokenType.Array)
                {
                    var joined = JoinSegments((JArray)value);
                    if (!string.IsNullOrWhiteSpace(joined)) return joined.Trim();
                    continue;
                }
                if (value.Type == JTokenType.Object) continue;
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            }
            return null;
        }

        public static long Long(this JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var text = token.Str(path);
                if (string.IsNullOrEmpty(text)) continue;
                long n;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
                double d;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return (long)d;
                var digits = new string(text.Where(char.IsDigit).ToArray());
                if (digits.Length > 0 && digits.Length < 18 && long.TryParse(digits, out n)) return n;
            }
            return 0;
        }

        public static int Int(this JToken token, params string[] paths)
        {
            var n = token.Long(paths);
            return n > int.MaxValue ? int.MaxValue : (int)n;
        }

        /// <summary>取浮点数，用于图片/视频的原始宽高。</summary>
        public static double Num(this JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var text = token.Str(path);
                if (string.IsNullOrEmpty(text)) continue;
                double d;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            return 0;
        }

        public static bool Bool(this JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var value = token.At(path);
                if (value == null) continue;
                if (value.Type == JTokenType.Boolean) return value.Value<bool>();
                var text = value.ToString();
                if (text == "1" || string.Equals(text, "true", System.StringComparison.OrdinalIgnoreCase)) return true;
                if (text == "0" || string.Equals(text, "false", System.StringComparison.OrdinalIgnoreCase)) return false;
            }
            return false;
        }

        public static IEnumerable<JToken> Items(this JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var array = token.At(path) as JArray;
                if (array != null && array.Count > 0) return array;
            }
            return Enumerable.Empty<JToken>();
        }

        /// <summary>
        /// 递归按键名找第一个非空标量值，忽略大小写，names 按优先级排列。
        /// 闲鱼的卡片消息没有公开文档、字段名还随版本漂移，固定路径取不到时靠它兜底。
        /// </summary>
        public static string Find(this JToken token, params string[] names)
        {
            if (token == null) return null;
            foreach (var name in names)
            {
                var found = FindProperty(token, name);
                if (!string.IsNullOrWhiteSpace(found)) return found.Trim();
            }
            return null;
        }

        private static string FindProperty(JToken token, string name)
        {
            var container = token as JContainer;
            if (container == null) return null;
            foreach (var property in container.Descendants().OfType<JProperty>())
            {
                if (!string.Equals(property.Name, name, System.StringComparison.OrdinalIgnoreCase)) continue;
                var value = property.Value;

                if (value.Type == JTokenType.Object)
                {
                    // 形如 {"title": {"text": "..."}}
                    var nested = value.Str("text", "value", "content", "title", "name");
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                    continue;
                }
                if (value.Type == JTokenType.Array)
                {
                    // 价格常被拆成片段：[{"text":"¥"},{"text":"161"}]
                    var joined = JoinSegments((JArray)value);
                    if (!string.IsNullOrWhiteSpace(joined)) return joined;
                    continue;
                }
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            return null;
        }

        /// <summary>价格常被拆成富文本片段：[{"text":"¥"},{"text":"161"}]。</summary>
        private static string JoinSegments(JArray array)
        {
            var sb = new StringBuilder();
            foreach (var part in array)
            {
                if (part.Type == JTokenType.String || part.Type == JTokenType.Integer || part.Type == JTokenType.Float)
                {
                    sb.Append(part);
                }
                else if (part.Type == JTokenType.Object)
                {
                    var text = part.Str("text", "price", "value", "content");
                    if (text != null) sb.Append(text);
                }
            }
            return sb.ToString();
        }
    }
}
