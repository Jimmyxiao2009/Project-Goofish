using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;
using Windows.UI.Core;

namespace Goofish.ViewModels
{
    /// <summary>打开私信页的参数：已有会话传 Cid；从宝贝详情发起时传宝贝与卖家；从个人主页发起时传 PeerUserId。</summary>
    public sealed class ChatArgs
    {
        public string Cid { get; set; }
        public Conversation Conversation { get; set; }
        public ItemDetail Item { get; set; }

        /// <summary>从个人主页发起：没有宝贝，只知道对方是谁。</summary>
        public string PeerUserId { get; set; }
        public string PeerNick { get; set; }
    }

    public sealed class ChatViewModel : BindableBase
    {
        private readonly CoreDispatcher _dispatcher;
        private string _draft = string.Empty;
        private string _error;
        private string _peerNick;
        private string _peerAvatar;
        private string _peerColor = "#FF767676";
        private string _itemTitle;
        private string _itemPrice;
        private string _itemCover;
        private string _itemColor = "#FF3A3A3A";
        private string _itemGlyph = "\uE7B8";
        private bool _isReady;
        private IChatService _chat;

        public ChatViewModel(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public string Cid { get; private set; }
        public string PeerUserId { get; private set; }
        public string ItemId { get; private set; }

        public string PeerNick { get { return _peerNick; } private set { if (SetProperty(ref _peerNick, value)) OnPropertyChanged(nameof(PeerInitial)); } }
        public string PeerInitial => string.IsNullOrEmpty(PeerNick) ? "?" : PeerNick.Substring(0, 1);
        public string PeerAvatar { get { return _peerAvatar; } private set { SetProperty(ref _peerAvatar, value); } }
        public string PeerColor { get { return _peerColor; } private set { SetProperty(ref _peerColor, value); } }
        public string ItemTitle { get { return _itemTitle; } private set { SetProperty(ref _itemTitle, value); } }
        public string ItemPrice { get { return _itemPrice; } private set { SetProperty(ref _itemPrice, value); } }
        public string ItemCover { get { return _itemCover; } private set { SetProperty(ref _itemCover, value); } }
        public string ItemColor { get { return _itemColor; } private set { SetProperty(ref _itemColor, value); } }
        public string ItemGlyph { get { return _itemGlyph; } private set { SetProperty(ref _itemGlyph, value); } }

        public string Draft
        {
            get { return _draft; }
            set
            {
                if (SetProperty(ref _draft, value ?? string.Empty)) OnPropertyChanged(nameof(CanSend));
            }
        }

        public bool CanSend => _isReady && !string.IsNullOrWhiteSpace(Draft);

        public bool IsReady
        {
            get { return _isReady; }
            private set
            {
                if (SetProperty(ref _isReady, value)) OnPropertyChanged(nameof(CanSend));
            }
        }

        public string Error
        {
            get { return _error; }
            private set { SetProperty(ref _error, value); }
        }

        public bool IsEmpty => Messages.Count == 0;

        public async Task LoadAsync(ChatArgs args)
        {
            _chat = AppServices.Chat;
            _chat.MessageReceived -= OnMessageReceived;
            _chat.MessageReceived += OnMessageReceived;
            Error = null;
            IsReady = false;

            try
            {
                await StatusTray.RunAsync("正在连接消息服务...", async () =>
                {
                    if (args.Item != null)
                    {
                        ApplyItem(args.Item);
                        PeerUserId = args.Item.Seller?.UserId ?? args.Item.SellerId;
                        PeerNick = args.Item.Seller?.Nick ?? args.Item.SellerNick;
                        PeerAvatar = args.Item.Seller?.AvatarUrl ?? args.Item.SellerAvatar;
                        PeerColor = args.Item.Seller?.AvatarColor ?? PeerColor;
                        if (string.IsNullOrEmpty(PeerUserId)) throw new MtopException(MtopErrorKind.Api, null, "拿不到卖家信息，暂时无法联系。");
                        Cid = await _chat.OpenConversationAsync(PeerUserId, ItemId);
                    }
                    else if (!string.IsNullOrEmpty(args.PeerUserId))
                    {
                        // 从个人主页发起：没有宝贝，按对方 ID 建会话
                        PeerUserId = args.PeerUserId;
                        if (!string.IsNullOrEmpty(args.PeerNick)) PeerNick = args.PeerNick;
                        Cid = await _chat.OpenConversationAsync(PeerUserId, null);
                    }
                    else
                    {
                        var conv = args.Conversation ?? await FindConversationAsync(args.Cid);
                        Cid = conv?.Cid ?? args.Cid;
                        if (conv != null) ApplyConversation(conv);
                    }

                    AppServices.ActiveConversationId = Cid;
                    var history = await _chat.GetMessagesAsync(Cid);
                    Messages.Clear();
                    foreach (var m in history) Messages.Add(m);
                    OnPropertyChanged(nameof(IsEmpty));
                    IsReady = true;
                });
            }
            catch (Exception ex)
            {
                Error = MtopException.Describe(ex);
            }
        }

        public void Unload()
        {
            if (_chat != null) _chat.MessageReceived -= OnMessageReceived;
            if (AppServices.ActiveConversationId == Cid) AppServices.ActiveConversationId = null;
        }

        public async Task SendAsync(string text = null)
        {
            text = (text ?? Draft ?? string.Empty).Trim();
            if (text.Length == 0 || !IsReady) return;
            if (text == Draft.Trim()) Draft = string.Empty;

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Cid = Cid,
                SenderId = _chat.SelfUserId,
                Text = text,
                IsMine = true,
                State = MessageState.Sending,
                Time = DateTimeOffset.Now
            };
            Messages.Add(message);
            OnPropertyChanged(nameof(IsEmpty));
            await DeliverAsync(message);
        }

        public Task RetryAsync(ChatMessage message)
        {
            if (message == null || message.State != MessageState.Failed) return Task.FromResult(0);
            message.State = MessageState.Sending;
            return DeliverAsync(message);
        }

        private async Task DeliverAsync(ChatMessage message)
        {
            try
            {
                await _chat.SendTextAsync(Cid, PeerUserId, message);
                message.State = MessageState.Sent;
                Error = null;
            }
            catch (Exception ex)
            {
                message.State = MessageState.Failed;
                Error = MtopException.Describe(ex);
            }
        }

        private async Task<Conversation> FindConversationAsync(string cid)
        {
            var list = await AppServices.Chat.GetConversationsAsync();
            var conv = list.FirstOrDefault(c => c.Cid == cid);
            if (conv != null && string.IsNullOrEmpty(conv.PeerNick) && !string.IsNullOrEmpty(conv.PeerUserId))
            {
                // session.sync 没给昵称时，单独补一次
                try
                {
                    var profile = await AppServices.Data.GetUserProfileAsync(conv.PeerUserId);
                    if (!string.IsNullOrEmpty(profile.Nick)) conv.PeerNick = profile.Nick;
                    if (string.IsNullOrEmpty(conv.PeerAvatar)) conv.PeerAvatar = profile.AvatarUrl;
                }
                catch (Exception)
                {
                }
            }
            return conv;
        }

        private void ApplyConversation(Conversation conv)
        {
            PeerUserId = conv.PeerUserId;
            PeerNick = conv.DisplayName;
            PeerAvatar = conv.PeerAvatar;
            PeerColor = conv.PeerColor;
            ItemId = conv.ItemId;
            ItemTitle = conv.ItemTitle;
            ItemPrice = conv.ItemPriceText;
            ItemCover = conv.ItemCover;
            conv.UnreadCount = 0;

            // 演示数据：用宝贝的占位色块
            var demoItem = AppServices.UseDemo ? AppServices.Demo.FindItem(conv.ItemId) : null;
            if (demoItem != null)
            {
                ItemColor = demoItem.PlaceholderColor;
                ItemGlyph = demoItem.PlaceholderGlyph;
            }
        }

        private void ApplyItem(ItemDetail item)
        {
            ItemId = item.ItemId;
            ItemTitle = item.Title;
            ItemPrice = item.PriceText;
            ItemCover = item.CoverUrl;
            ItemColor = item.PlaceholderColor;
            ItemGlyph = item.PlaceholderGlyph;
        }

        private async void OnMessageReceived(object sender, ChatMessage message)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (message.Cid != Cid) return;
                if (!string.IsNullOrEmpty(message.MessageId) && Messages.Any(m => m.MessageId == message.MessageId)) return;
                Messages.Add(message);
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
    }
}
