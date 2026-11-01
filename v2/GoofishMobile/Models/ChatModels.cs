using System;
using Goofish.Common;

namespace Goofish.Models
{
    public sealed class Conversation : BindableBase
    {
        private string _lastMessage;
        private int _unread;
        private DateTimeOffset _time = DateTimeOffset.MinValue;

        /// <summary>会话 ID，不带 "@goofish" 后缀。</summary>
        public string Cid { get; set; }
        public string PeerUserId { get; set; }

        private string _peerNick;
        private string _peerAvatar;
        private string _peerColor = "#FF767676";

        /// <summary>对方昵称。接口可能拿不到，此时界面显示“闲鱼用户”；补齐后会自动刷新。</summary>
        public string PeerNick
        {
            get { return _peerNick; }
            set
            {
                if (!SetProperty(ref _peerNick, value)) return;
                OnPropertyChanged(nameof(Initial));
                // 界面绑定的是 DisplayName，昵称补齐后必须一并通知它，否则头像会变、昵称不变
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>对方头像。接口可能拿不到，为空时界面显示昵称首字。</summary>
        public string PeerAvatar
        {
            get { return _peerAvatar; }
            set { SetProperty(ref _peerAvatar, value); }
        }

        /// <summary>拿不到头像时的底色（演示数据用）。</summary>
        public string PeerColor
        {
            get { return _peerColor; }
            set { SetProperty(ref _peerColor, value); }
        }
        public string ItemId { get; set; }
        public string ItemTitle { get; set; }
        public string ItemCover { get; set; }
        public decimal ItemPrice { get; set; }

        public string LastMessage
        {
            get { return _lastMessage; }
            set { SetProperty(ref _lastMessage, value); }
        }

        public int UnreadCount
        {
            get { return _unread; }
            set
            {
                if (SetProperty(ref _unread, value)) OnPropertyChanged(nameof(IsUnread));
            }
        }

        public DateTimeOffset Time
        {
            get { return _time; }
            set
            {
                if (SetProperty(ref _time, value)) OnPropertyChanged(nameof(TimeText));
            }
        }

        public bool IsUnread => UnreadCount > 0;

        public string TimeText => TextFormat.ConversationTime(Time);

        public string Initial => string.IsNullOrEmpty(PeerNick) ? "?" : PeerNick.Substring(0, 1);

        /// <summary>界面上显示的对方名称：系统会话显示“闲鱼官方”，其余拿不到昵称时用占位。</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PeerNick)) return PeerNick;
                return string.IsNullOrEmpty(PeerUserId) ? "闲鱼官方" : "闲鱼用户";
            }
        }

        public string ItemPriceText => ItemPrice > 0 ? TextFormat.Price(ItemPrice) : null;

        public override string ToString()
        {
            return PeerNick + "：" + LastMessage;
        }
    }

    public enum MessageState
    {
        Sending,
        Sent,
        Failed
    }

    /// <summary>消息内容类型，对应闲鱼 IM 的 contentType：1 文本、2 图片、3 语音、4 视频，5 起是卡片类。</summary>
    public enum MessageKind
    {
        Text,
        Image,
        Video,
        Audio,
        /// <summary>卡片类消息（宝贝卡片、订单卡片、链接、文件、引用、合并转发等）。</summary>
        Card
    }

    public sealed class ChatMessage : BindableBase
    {
        private MessageState _state = MessageState.Sent;
        private MessageKind _kind = MessageKind.Text;

        public string MessageId { get; set; }
        public string Cid { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Text { get; set; }
        public bool IsMine { get; set; }
        public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;

        public MessageKind Kind
        {
            get { return _kind; }
            set
            {
                if (SetProperty(ref _kind, value))
                {
                    OnPropertyChanged(nameof(IsImage));
                    OnPropertyChanged(nameof(IsVideo));
                    OnPropertyChanged(nameof(IsCard));
                    OnPropertyChanged(nameof(HasMedia));
                }
            }
        }

        /// <summary>卡片消息的标题、描述、价格、跳转链接与宝贝 ID（取不到的留空）。</summary>
        public string CardTitle { get; set; }
        public string CardDesc { get; set; }
        public string CardPrice { get; set; }
        public string CardUrl { get; set; }
        public string CardItemId { get; set; }

        public bool IsCard => Kind == MessageKind.Card;

        /// <summary>图片或视频的地址；视频是可播放的直链。</summary>
        public string MediaUrl { get; set; }

        /// <summary>视频封面（闲鱼不一定给，没有就显示深色底加播放按钮）。</summary>
        public string MediaCover { get; set; }

        /// <summary>原始像素尺寸，用来按比例摆放，避免拉伸变形。</summary>
        public double MediaWidth { get; set; }

        public double MediaHeight { get; set; }

        public bool IsImage => Kind == MessageKind.Image;

        public bool IsVideo => Kind == MessageKind.Video;

        public bool HasMedia => Kind == MessageKind.Image || Kind == MessageKind.Video;

        /// <summary>系统提示（如“交易关闭”）居中显示，不画气泡。</summary>
        public bool IsSystem { get; set; }

        public MessageState State
        {
            get { return _state; }
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(BubbleOpacity));
                }
            }
        }

        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case MessageState.Sending: return "正在发送...";
                    case MessageState.Failed: return "发送失败，点击重试";
                    default: return TextFormat.MessageTime(Time);
                }
            }
        }

        public double BubbleOpacity => State == MessageState.Sending ? 0.6 : 1.0;

        public override string ToString()
        {
            return (IsMine ? "我：" : (SenderName ?? "对方") + "：") + Text;
        }
    }

    public sealed class AccountInfo
    {
        public string UserId { get; set; }
        public string Nick { get; set; }
        public string AvatarUrl { get; set; }
    }
}
