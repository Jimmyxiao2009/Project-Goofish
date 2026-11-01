using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;

namespace Goofish.ViewModels
{
    public sealed class MainViewModel : BindableBase
    {
        private string _feedMessage;
        private string _conversationsMessage;
        private string _favoritesMessage;
        private string _feedFooter;
        private int _favoriteFilterIndex;
        private int _unreadCount;
        private bool _conversationsLoaded;
        private int _conversationGeneration;

        public MainViewModel()
        {
            Feed = new IncrementalCollection<ItemSummary>(page => AppServices.Data.GetHomeFeedAsync(page));
            Feed.LoadingChanged += (s, e) => OnFeedLoadingChanged();

            Favorites = new IncrementalCollection<ItemSummary>(page => AppServices.Data.GetFavoritesAsync((FavoriteFilter)_favoriteFilterIndex, page));
            Favorites.LoadingChanged += (s, e) => OnFavoritesLoadingChanged();
        }

        public IncrementalCollection<ItemSummary> Feed { get; }

        public IncrementalCollection<ItemSummary> Favorites { get; }

        public ObservableCollection<Conversation> Conversations { get; } = new ObservableCollection<Conversation>();

        public string[] FavoriteFilters { get; } = { "全部宝贝", "降价宝贝", "有效宝贝", "已失效" };

        public int FavoriteFilterIndex
        {
            get { return _favoriteFilterIndex; }
            set
            {
                if (value < 0 || !SetProperty(ref _favoriteFilterIndex, value)) return;
                var ignored = ReloadFavoritesAsync();
            }
        }

        /// <summary>推荐页中央的提示（加载失败、没有数据）。</summary>
        public string FeedMessage
        {
            get { return _feedMessage; }
            private set { SetProperty(ref _feedMessage, value); }
        }

        public string FeedFooter
        {
            get { return _feedFooter; }
            private set { SetProperty(ref _feedFooter, value); }
        }

        public string ConversationsMessage
        {
            get { return _conversationsMessage; }
            private set { SetProperty(ref _conversationsMessage, value); }
        }

        public string FavoritesMessage
        {
            get { return _favoritesMessage; }
            private set { SetProperty(ref _favoritesMessage, value); }
        }

        public int UnreadCount
        {
            get { return _unreadCount; }
            private set
            {
                if (SetProperty(ref _unreadCount, value)) OnPropertyChanged(nameof(UnreadText));
            }
        }

        public string UnreadText => UnreadCount > 0 ? UnreadCount.ToString() : string.Empty;

        /// <summary>未登录且没开演示数据：消息、收藏都要登录。</summary>
        public bool NeedsLogin => !AppServices.Account.IsSignedIn && !AppServices.UseDemo;

        public bool IsDemo => AppServices.UseDemo;

        public bool IsSignedIn => AppServices.Account.IsSignedIn;

        public void NotifyAccountChanged()
        {
            OnPropertyChanged(nameof(NeedsLogin));
            OnPropertyChanged(nameof(IsDemo));
            OnPropertyChanged(nameof(IsSignedIn));
            _conversationsLoaded = false;
        }

        public Task RefreshFeedAsync()
        {
            return Feed.ResetAsync();
        }

        /// <summary>
        /// 加载会话列表。
        ///
        /// 先用本地缓存把列表显示出来（不阻塞、不显示加载条），网络刷新放到后台，
        /// 这样切到消息页或从会话返回时不会每次都空等一次同步。缓存为空（首次运行）时才等网络。
        /// </summary>
        public async Task LoadConversationsAsync(bool force = false)
        {
            if (NeedsLogin)
            {
                Conversations.Clear();
                ConversationsMessage = "登录后可以和卖家聊天";
                return;
            }

            if (!_conversationsLoaded)
            {
                _conversationsLoaded = true;
                var cached = await ConversationsCache.LoadAsync(AppServices.Account.UserId);
                if (cached.Count > 0)
                {
                    Conversations.Clear();
                    foreach (var c in cached) Conversations.Add(c);
                    RecountUnread();
                    // 有缓存：先显示，再后台刷新
                    var ignored = RefreshConversationsAsync();
                    return;
                }
                // 没有缓存：等一次网络，用户能看到加载状态
                await RefreshConversationsAsync(true);
                return;
            }

            if (force)
            {
                // 从会话页返回：只后台刷新未读数，不阻塞、不清空
                var ignored = RefreshConversationsAsync();
                return;
            }

            RecountUnread();
        }

        /// <summary>从网络刷新会话列表，并写回缓存。showStatus 为 true 时显示系统托盘的加载状态。</summary>
        private async Task RefreshConversationsAsync(bool showStatus = false)
        {
            var generation = ++_conversationGeneration;
            try
            {
                var list = showStatus
                    ? await StatusTray.RunAsync("正在同步消息...", () => AppServices.Chat.GetConversationsAsync())
                    : await AppServices.Chat.GetConversationsAsync();
                if (generation != _conversationGeneration) return;

                // 接口只给会话和最后一条消息，没有对方昵称/头像，用已有列表（含缓存）按 cid 补上，
                // 否则刷新的一瞬间会整列闪回“闲鱼用户”
                MergePeers(list, Conversations);
                Conversations.Clear();
                foreach (var c in list) Conversations.Add(c);
                ConversationsMessage = list.Count == 0 ? "还没有会话。在宝贝详情页点“聊一聊”就能联系卖家。" : null;
                RecountUnread();

                await ConversationsCache.SaveAsync(AppServices.Account.UserId, list);
                await EnrichPeersAsync(list, generation);
            }
            catch (Exception ex)
            {
                // 有缓存显示时不要把列表清掉，只在确实没内容时提示
                if (Conversations.Count == 0) ConversationsMessage = MtopException.Describe(ex);
            }
            RecountUnread();
        }

        /// <summary>把旧列表（含本地缓存）里的昵称、头像、商品信息按 cid 合到新列表上。</summary>
        private static void MergePeers(IList<Conversation> fresh, IList<Conversation> known)
        {
            if (known == null || known.Count == 0) return;
            var byCid = new Dictionary<string, Conversation>();
            foreach (var c in known) byCid[c.Cid] = c;

            foreach (var conv in fresh)
            {
                Conversation old;
                if (!byCid.TryGetValue(conv.Cid, out old)) continue;
                if (string.IsNullOrEmpty(conv.PeerUserId)) conv.PeerUserId = old.PeerUserId;
                if (!HasPeerName(conv) && HasPeerName(old)) conv.PeerNick = old.PeerNick;
                if (string.IsNullOrEmpty(conv.PeerAvatar)) conv.PeerAvatar = old.PeerAvatar;
                if (!string.IsNullOrEmpty(old.PeerColor)) conv.PeerColor = old.PeerColor;
                if (string.IsNullOrEmpty(conv.ItemTitle)) conv.ItemTitle = old.ItemTitle;
                if (string.IsNullOrEmpty(conv.ItemCover)) conv.ItemCover = old.ItemCover;
                if (conv.ItemPrice == 0) conv.ItemPrice = old.ItemPrice;
            }
        }

        /// <summary>
        /// 补齐会话列表里对方的昵称和头像。
        ///
        /// 正常来源是 mtop 的 session.sync，但那条属于 idlemessage 命名空间，可能被风控单独拦掉，
        /// 表现就是整列都显示“闲鱼用户”、没有头像。这里对仍缺昵称或头像的会话，逐个用普通用户
        /// 接口补（GoofishService 有缓存，同一个对方只请求一次），补到就通过绑定刷新界面。
        /// </summary>
        private async Task EnrichPeersAsync(IList<Conversation> list, int generation)
        {
            // 每次最多补这么多个，避免 session.sync 整个失败时对着一长串会话狂发请求触发风控
            var budget = 20;
            var changed = false;
            foreach (var conv in list)
            {
                if (budget <= 0) break;
                if (generation != _conversationGeneration) return;
                if (string.IsNullOrEmpty(conv.PeerUserId)) continue;
                if (HasPeerName(conv) && !string.IsNullOrEmpty(conv.PeerAvatar)) continue;
                budget--;
                try
                {
                    var profile = await AppServices.Data.GetUserProfileAsync(conv.PeerUserId);
                    if (generation != _conversationGeneration) return;
                    if (!HasPeerName(conv) && !string.IsNullOrEmpty(profile.Nick)) { conv.PeerNick = profile.Nick; changed = true; }
                    if (string.IsNullOrEmpty(conv.PeerAvatar) && !string.IsNullOrEmpty(profile.AvatarUrl)) { conv.PeerAvatar = profile.AvatarUrl; changed = true; }
                    if (!string.IsNullOrEmpty(profile.AvatarColor)) conv.PeerColor = profile.AvatarColor;
                }
                catch (Exception ex)
                {
                    // 单个对方失败不影响其它会话
                    System.Diagnostics.Debug.WriteLine("补对方资料失败：" + conv.PeerUserId + " " + ex.Message);
                }
            }

            // 补齐的结果写回缓存，下次打开就不用再逐个查
            if (changed) await ConversationsCache.SaveAsync(AppServices.Account.UserId, list);
        }

        private static bool HasPeerName(Conversation conv)
        {
            return !string.IsNullOrEmpty(conv.PeerNick) && conv.PeerNick != "闲鱼用户";
        }

        public void RecountUnread()
        {
            var n = 0;
            foreach (var c in Conversations) n += c.UnreadCount;
            UnreadCount = n;
        }

        public async Task ReloadFavoritesAsync()
        {
            if (NeedsLogin)
            {
                Favorites.Clear();
                FavoritesMessage = "登录后同步你的闲鱼收藏";
                return;
            }
            FavoritesMessage = null;
            await Favorites.ResetAsync();
        }

        private void OnFeedLoadingChanged()
        {
            if (Feed.IsLoading)
            {
                StatusTray.Begin("正在获取推荐...");
                FeedMessage = null;
                FeedFooter = Feed.Count > 0 ? "正在加载..." : null;
                return;
            }
            StatusTray.End();
            if (Feed.LastError != null)
            {
                var text = MtopException.Describe(Feed.LastError);
                if (Feed.Count == 0) FeedMessage = text;
                FeedFooter = Feed.Count == 0 ? null : text;
            }
            else
            {
                FeedMessage = Feed.Count == 0 ? "暂时没有推荐" : null;
                FeedFooter = Feed.HasMoreItems ? null : "没有更多推荐了";
            }
        }

        private void OnFavoritesLoadingChanged()
        {
            if (Favorites.IsLoading)
            {
                StatusTray.Begin("正在获取收藏...");
                return;
            }
            StatusTray.End();
            if (Favorites.LastError != null) FavoritesMessage = MtopException.Describe(Favorites.LastError);
            else FavoritesMessage = Favorites.Count == 0 ? "这里还没有宝贝" : null;
        }
    }
}
