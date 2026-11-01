using System;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Goofish.Services
{
    public static class ToastService
    {
        /// <summary>新私信横幅。点击后以 cid=... 参数启动并打开对应会话。</summary>
        public static void ShowMessage(string title, string body, string cid)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(
                    "<toast><visual><binding template=\"ToastGeneric\"><text/><text/></binding></visual>" +
                    "<audio src=\"ms-winsoundevent:Notification.IM\"/></toast>");
                // 用 DOM 写入文本与属性，免去手工转义昵称、消息里的特殊字符
                doc.DocumentElement.SetAttribute("launch", "cid=" + cid);
                var texts = doc.GetElementsByTagName("text");
                texts[0].AppendChild(doc.CreateTextNode(title ?? "闲鱼"));
                texts[1].AppendChild(doc.CreateTextNode(body ?? string.Empty));

                var toast = new ToastNotification(doc) { Tag = Shorten(cid), Group = "chat" };
                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Toast 显示失败：" + ex.Message);
            }
        }

        // Toast 的 Tag 在 10586 上最长 16 个字符
        private static string Shorten(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= 16 ? value : value.Substring(value.Length - 16);
        }
    }
}
