using Goofish.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Goofish.Views
{
    /// <summary>
    /// 按「谁的 + 什么类型」挑气泡模板：文本、图片、视频、卡片各一套，系统提示单独一套。
    /// </summary>
    public sealed class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Incoming { get; set; }
        public DataTemplate Outgoing { get; set; }
        public DataTemplate System { get; set; }
        public DataTemplate IncomingImage { get; set; }
        public DataTemplate OutgoingImage { get; set; }
        public DataTemplate IncomingVideo { get; set; }
        public DataTemplate OutgoingVideo { get; set; }
        public DataTemplate IncomingCard { get; set; }
        public DataTemplate OutgoingCard { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var message = item as ChatMessage;
            if (message == null) return Incoming;
            if (message.IsSystem) return System;

            switch (message.Kind)
            {
                case MessageKind.Image:
                    return (message.IsMine ? OutgoingImage : IncomingImage) ?? (message.IsMine ? Outgoing : Incoming);
                case MessageKind.Video:
                    return (message.IsMine ? OutgoingVideo : IncomingVideo) ?? (message.IsMine ? Outgoing : Incoming);
                case MessageKind.Card:
                    return (message.IsMine ? OutgoingCard : IncomingCard) ?? (message.IsMine ? Outgoing : Incoming);
                default:
                    return message.IsMine ? Outgoing : Incoming;
            }
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
