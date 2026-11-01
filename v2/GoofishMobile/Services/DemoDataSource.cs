using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Models;

namespace Goofish.Services
{
    /// <summary>
    /// 未登录时的演示数据，与 prototype/js/data.js 中的样例保持一致。
    /// 私信部分会模拟卖家回复，用来演示消息推送与 Toast。
    /// </summary>
    public sealed class DemoDataSource : IGoofishDataSource, IChatService
    {
        private const int PageSize = 6;
        private readonly List<ItemDetail> _items = new List<ItemDetail>();
        private readonly Dictionary<string, SellerInfo> _sellers = new Dictionary<string, SellerInfo>();
        private readonly List<Conversation> _conversations = new List<Conversation>();
        private readonly Dictionary<string, List<ChatMessage>> _messages = new Dictionary<string, List<ChatMessage>>();
        private readonly List<OrderSummary> _orders = new List<OrderSummary>();
        private readonly Dictionary<string, BoughtOrderStatus> _orderStatus = new Dictionary<string, BoughtOrderStatus>();
        private readonly FavoritesStore _favorites;
        private readonly Random _random = new Random();

        private static readonly string[] Replies =
        {
            "在的，诚心要可以小刀",
            "包邮的，今天拍下今天发顺丰",
            "可以视频看实物，你方便的时候说一声",
            "成色描述都是真实的，放心",
            "最低价了哈，已经很便宜了"
        };

        public DemoDataSource(FavoritesStore favorites)
        {
            _favorites = favorites;
            Seed();
        }

        public event EventHandler<ChatMessage> MessageReceived;

        public string SelfUserId => "demo";

        public async Task<PageResult<ItemSummary>> GetHomeFeedAsync(int page)
        {
            await Task.Delay(500);
            var items = Enumerable.Range((page - 1) * PageSize, PageSize)
                .Where(i => i < _items.Count * 2)
                .Select(i => (ItemSummary)_items[i % _items.Count])
                .ToList();
            return new PageResult<ItemSummary>(items, page < 3);
        }

        public async Task<PageResult<ItemSummary>> SearchAsync(string keyword, SearchSort sort, int page)
        {
            await Task.Delay(500);
            var kw = (keyword ?? string.Empty).Trim();
            IEnumerable<ItemDetail> query = _items.Where(i =>
                kw.Length == 0 ||
                i.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (i.City ?? string.Empty).Contains(kw) ||
                (i.SellerNick ?? string.Empty).IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);

            switch (sort)
            {
                case SearchSort.PriceAscending: query = query.OrderBy(i => i.Price); break;
                case SearchSort.PriceDescending: query = query.OrderByDescending(i => i.Price); break;
                case SearchSort.Newest: query = query.OrderByDescending(i => i.PublishTime); break;
            }
            var list = query.Cast<ItemSummary>().ToList();
            return new PageResult<ItemSummary>(page == 1 ? list : new List<ItemSummary>(), false, list.Count);
        }

        public ItemDetail FindItem(string itemId)
        {
            return itemId == null ? null : _items.FirstOrDefault(i => i.ItemId == itemId);
        }

        public async Task<ItemDetail> GetItemDetailAsync(string itemId)
        {
            await Task.Delay(350);
            var item = _items.FirstOrDefault(i => i.ItemId == itemId);
            if (item == null) throw new MtopException(MtopErrorKind.Api, null, "宝贝不存在或已下架。");
            return item;
        }

        public async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            await Task.Delay(300);
            SellerInfo seller;
            if (!_sellers.TryGetValue(userId ?? string.Empty, out seller))
                throw new MtopException(MtopErrorKind.Api, null, "找不到这个用户。");
            return new UserProfile
            {
                UserId = seller.UserId,
                Nick = seller.Nick,
                City = seller.City,
                Credit = seller.Credit,
                SoldCount = seller.SoldCount,
                ReplyRatio = seller.ReplyRatio,
                LastVisit = seller.LastVisit,
                AvatarColor = seller.AvatarColor,
                ItemCount = _items.Count(i => i.SellerId == seller.UserId),
                FollowerCount = 12,
                FollowingCount = 3,
                RateCount = 8,
                Verified = true,
                Signature = "演示数据：这里是卖家的个人主页。"
            };
        }

        public async Task<PageResult<ItemSummary>> GetFavoritesAsync(FavoriteFilter filter, int page)
        {
            await Task.Delay(300);
            if (page > 1) return new PageResult<ItemSummary>(new List<ItemSummary>(), false);
            IEnumerable<ItemSummary> list = _favorites.All;
            if (filter == FavoriteFilter.Reduced) list = list.Where(i => i.ReducedBy > 0);
            if (filter == FavoriteFilter.Valid) list = list.Where(i => !i.IsInvalid);
            if (filter == FavoriteFilter.Invalid) list = list.Where(i => i.IsInvalid);
            return new PageResult<ItemSummary>(list.ToList(), false);
        }

        public bool IsFavorite(string itemId)
        {
            return _favorites.Contains(itemId);
        }

        public async Task<bool> SetFavoriteAsync(ItemSummary item, bool favorite)
        {
            await Task.Delay(250);
            await _favorites.SetAsync(item, favorite);
            return true;
        }

        public async Task<IList<Conversation>> GetConversationsAsync()
        {
            await Task.Delay(300);
            return _conversations.OrderByDescending(c => c.Time).ToList();
        }

        public async Task<PageResult<OrderSummary>> GetBoughtOrdersAsync(BoughtOrderStatus status, int page)
        {
            await Task.Delay(300);
            var items = _orders
                .Where(o => !o.IsSold)
                .Where(o => status == BoughtOrderStatus.All || _orderStatus[o.OrderId] == status)
                .ToList();
            return new PageResult<OrderSummary>(items, false);
        }

        public async Task<PageResult<OrderSummary>> GetSoldOrdersAsync(int page)
        {
            await Task.Delay(300);
            return new PageResult<OrderSummary>(_orders.Where(o => o.IsSold).ToList(), false);
        }

        public async Task<OrderDetail> GetOrderDetailAsync(string orderId, bool isSold)
        {
            await Task.Delay(250);
            var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
            var detail = new OrderDetail
            {
                OrderId = orderId,
                ItemId = order?.ItemId,
                Title = order?.Title,
                Price = order?.Price ?? 0,
                Quantity = order?.Quantity ?? 0,
                StatusText = order?.StatusText,
                CreateTime = order?.CreateTime,
                IsSold = isSold,
                PlaceholderColor = order?.PlaceholderColor ?? "#FF3A3A3A",
                PlaceholderGlyph = order?.PlaceholderGlyph ?? "\uE7B8",
                BuyerId = order?.PeerUserId,
                ReceiverName = isSold ? "示例买家" : "示例收件人",
                ReceiverPhone = "138****0000",
                AddressText = "浙江省杭州市余杭区示例路 1 号"
            };
            detail.InfoItems.Add(new KeyValuePair<string, string>("订单编号", orderId));
            if (!string.IsNullOrEmpty(detail.CreateTime))
                detail.InfoItems.Add(new KeyValuePair<string, string>("下单时间", detail.CreateTime));
            return detail;
        }

        public async Task<IList<ChatMessage>> GetMessagesAsync(string cid)
        {
            await Task.Delay(300);
            var conv = _conversations.FirstOrDefault(c => c.Cid == cid);
            if (conv != null) conv.UnreadCount = 0;
            List<ChatMessage> list;
            return _messages.TryGetValue(cid, out list) ? list.ToList() : new List<ChatMessage>();
        }

        public async Task<string> OpenConversationAsync(string peerUserId, string itemId)
        {
            await Task.Delay(250);
            var existing = _conversations.FirstOrDefault(c => c.ItemId == itemId);
            if (existing != null) return existing.Cid;

            // itemId 为空表示从个人主页发起：没有宝贝，只有对方
            var item = string.IsNullOrEmpty(itemId) ? null : _items.FirstOrDefault(i => i.ItemId == itemId);
            SellerInfo seller;
            if (item != null)
            {
                seller = item.Seller;
            }
            else if (!_sellers.TryGetValue(peerUserId ?? string.Empty, out seller))
            {
                throw new MtopException(MtopErrorKind.Api, null, "找不到这个用户。");
            }

            var conv = NewConversation("4882" + DateTimeOffset.Now.ToUnixTimeMilliseconds() % 10000000, item, seller);
            conv.LastMessage = string.Empty;
            conv.Time = DateTimeOffset.Now;
            _conversations.Add(conv);
            _messages[conv.Cid] = new List<ChatMessage>();
            return conv.Cid;
        }

        public async Task SendTextAsync(string cid, string peerUserId, ChatMessage message)
        {
            await Task.Delay(400);
            message.Time = DateTimeOffset.Now;
            _messages[cid].Add(message);
            var conv = _conversations.First(c => c.Cid == cid);
            conv.LastMessage = "我: " + message.Text;
            conv.Time = message.Time;

            var reply = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Cid = cid,
                SenderId = conv.PeerUserId,
                SenderName = conv.PeerNick,
                Text = Replies[_random.Next(Replies.Length)]
            };
            SimulateReply(conv, reply);
        }

        // async void + await 会回到调用方（UI 线程）的上下文，绑定在界面上的 Conversation 可以直接改
        private async void SimulateReply(Conversation conv, ChatMessage reply)
        {
            await Task.Delay(2200);
            reply.Time = DateTimeOffset.Now;
            _messages[conv.Cid].Add(reply);
            conv.LastMessage = reply.Text;
            conv.Time = reply.Time;
            if (conv.Cid != AppServices.ActiveConversationId) conv.UnreadCount++;
            MessageReceived?.Invoke(this, reply);
        }

        public void Disconnect()
        {
        }

        private void Seed()
        {
            AddSeller("2200731", "柠檬汽水不加冰", "深圳", "极好", 86, "98%", "刚刚来过", "#FF00ABA9");
            AddSeller("2200845", "老王的数码仓库", "杭州", "优秀", 412, "92%", "1小时前来过", "#FF6A00FF");
            AddSeller("2201137", "Lumia 不死", "北京", "极好", 23, "100%", "刚刚来过", "#FFE51400");
            AddSeller("2201580", "半夏微凉", "上海", "良好", 7, "75%", "3天前来过", "#FFD80073");
            AddSeller("2202016", "阿杰搞机", "成都", "极好", 158, "96%", "10分钟前来过", "#FFFA6800");

            AddItem("735020881256", "Lumia 950 XL 双卡 3+32G 黑色 带原装 Continuum Dock，屏幕无划痕", 680, 4999, "2201137", 37, 1203, 52, true, "95新", true, "#FF1BA1E2", "\uE8EA", 3,
                "自用备机，一直戴壳贴膜使用，屏幕完美。\n系统 Windows 10 Mobile 1709 (15254.603)，已重置。\n配件：原装 Dock、Type-C 线、充电头、原盒。\n电池健康，正常一天一充。\n情怀出，不刀太狠都可以聊。");
            AddItem("735020881311", "Surface Pro 4 i5 8G 256G 送键盘盖和触控笔", 1650, 8888, "2200845", 64, 2410, 88, true, "9成新", false, "#FF647687", "\uE7F8", 0.2,
                "公司淘汰机器，屏幕无坏点无黄斑，电池损耗 18%。\n键盘盖边缘轻微磨损，笔正常使用。\n验货宝支持，支持当面交易。");
            AddItem("735020881402", "Xbox One S 1TB 白色 双手柄 + 极限竞速地平线 4 光盘", 999, 2599, "2202016", 21, 860, 30, false, "95新", true, "#FF60A917", "\uE7FC", 1,
                "国行，没怎么玩，吃灰两年。手柄摇杆无漂移。运费到付。");
            AddItem("735020881577", "Sony WH-1000XM3 降噪耳机 黑色 国行在保", 860, 2899, "2200731", 45, 1522, 61, true, "99新", false, "#FF76608A", "\uE95B", 2,
                "买来通勤用，后来换了新款。耳罩无脱皮，盒子配件都在，保修到明年 3 月。");
            AddItem("735020881690", "Canon EF 50mm f/1.8 STM 小痰盂 无霉无雾", 420, 899, "2201580", 18, 640, 27, true, "9成新", true, "#FFA20025", "\uE722", 7,
                "入门定焦，对焦安静。前后盖都有，送一个 UV 镜。");
            AddItem("735020881744", "《深入理解计算机系统》第三版 中文 几乎全新", 58, 139, "2201580", 9, 210, 14, false, "全新", false, "#FF825A2C", "\uE82D", 4,
                "只翻过前两章，无笔记无折角。可以和其它几本一起打包。");
            AddItem("735020881839", "IKBC C87 机械键盘 樱桃红轴 87 键 有线", 199, 429, "2202016", 12, 377, 19, true, "95新", true, "#FF6D8764", "\uE765", 0.25,
                "红轴手感很轻，打字游戏两用。键帽有轻微打油，可以送一套新键帽。");
            AddItem("735020881902", "Dell U2414H 24 寸 IPS 显示器 窄边框 可旋转升降", 520, 1699, "2200845", 29, 990, 33, false, "9成新", true, "#FF0050EF", "\uE7F4", 1,
                "无亮点无坏点，支架完好。只限杭州自提，顺便可以看看其它闲置。");
            AddItem("735020882015", "迪卡侬 RC100 公路车 M 码 骑行不到 300 公里", 1100, 1999, "2200731", 33, 1104, 40, false, "95新", true, "#FF008A00", "\uE806", 2,
                "搬家出，送码表支架和水壶架。仅限深圳南山自提。");
            AddItem("735020882188", "Microsoft Band 2 智能手环 M 码 带原装充电线", 150, 1688, "2201137", 15, 498, 22, true, "8成新", false, "#FFF0A30A", "\uE916", 5,
                "表带有一处小裂口，不影响佩戴。可以连接 Windows 10 Mobile 上的 Microsoft Health。");
            AddItem("735020882231", "Kindle Paperwhite 3 8G 白色 带保护套", 260, 958, "2201580", 51, 1840, 70, true, "9成新", true, "#FF87794E", "\uE70A", 0.12,
                "屏幕无划痕，电池耐用。已注销账号。");
            AddItem("735020882390", "雅马哈 F310 民谣吉他 41 寸 送琴包变调夹", 380, 899, "2202016", 8, 260, 11, false, "9成新", true, "#FFD80073", "\uE8D6", 7,
                "入门琴，音准正常，琴颈无弯曲。");

            var now = DateTimeOffset.Now;
            AddConversation("48820017331", "735020881256", 2, new[]
            {
                Msg(true, "你好，Lumia 950 XL 还在吗？", now.AddMinutes(-21)),
                Msg(false, "在的，今天刚擦干净拍的照片", now.AddMinutes(-18)),
                Media(false, MessageKind.Image, "https://picsum.photos/seed/lumia950/600/400", null, 600, 400, now.AddMinutes(-17)),
                Msg(false, "Dock 也是原装的，盒子说明书都在", now.AddMinutes(-14)),
                Media(false, MessageKind.Video, "https://www.w3schools.com/html/mov_bbb.mp4",
                    "https://picsum.photos/seed/lumia-video/600/400", 600, 400, now.AddMinutes(-13)),
                Card(_items.First(i => i.ItemId == "735020881256"), now.AddMinutes(-12))
            });
            AddConversation("48820017402", "735020881577", 0, new[]
            {
                Msg(true, "耳机支持验货吗？", now.AddDays(-1).AddMinutes(-47)),
                Msg(false, "可以走验货宝，运费我出", now.AddDays(-1).AddMinutes(-40)),
                Msg(true, "好的，我考虑一下", now.AddDays(-1).AddMinutes(-39))
            });
            AddConversation("48820017588", "735020881902", 0, new[]
            {
                Msg(true, "显示器能便宜点吗", now.AddDays(-3).AddHours(-2)),
                Msg(false, "最低 500，自提的话再送一根 DP 线", now.AddDays(-3).AddHours(-1))
            });

            AddOrders();
        }

        /// <summary>演示订单：几笔我买到的，两笔我卖出的。</summary>
        private void AddOrders()
        {
            var statuses = new[]
            {
                BoughtOrderStatus.NotShip, BoughtOrderStatus.Shipped, BoughtOrderStatus.NotRate,
                BoughtOrderStatus.NotPay, BoughtOrderStatus.Refund, BoughtOrderStatus.All
            };
            var texts = new[] { "等待卖家发货", "卖家已发货", "交易成功", "等待买家付款", "退款中", "交易成功" };

            for (var i = 0; i < _items.Count && i < 6; i++)
            {
                var item = _items[i];
                var order = new OrderSummary
                {
                    OrderId = "2856024697612" + (814000 + i),
                    ItemId = item.ItemId,
                    Title = item.Title,
                    Price = item.Price,
                    Quantity = 1,
                    PeerUserId = item.Seller.UserId,
                    PeerNick = item.Seller.Nick,
                    StatusText = texts[i],
                    CreateTime = DateTimeOffset.Now.AddDays(-i - 1).ToString("yyyy-MM-dd HH:mm:ss"),
                    IsSold = false,
                    PlaceholderColor = item.PlaceholderColor,
                    PlaceholderGlyph = item.PlaceholderGlyph
                };
                _orders.Add(order);
                _orderStatus[order.OrderId] = statuses[i];
            }

            var soldTexts = new[] { "待发货", "交易成功" };
            for (var i = 0; i < 2 && i < _items.Count; i++)
            {
                var item = _items[i];
                _orders.Add(new OrderSummary
                {
                    OrderId = "2856024697613" + (921000 + i),
                    ItemId = item.ItemId,
                    Title = item.Title,
                    Price = item.Price,
                    Quantity = 1,
                    PeerUserId = "3300" + i,
                    PeerNick = i == 0 ? "山高水长" : "晚风轻拂",
                    StatusText = soldTexts[i],
                    CreateTime = DateTimeOffset.Now.AddDays(-i - 2).ToString("yyyy-MM-dd HH:mm:ss"),
                    IsSold = true,
                    PlaceholderColor = item.PlaceholderColor,
                    PlaceholderGlyph = item.PlaceholderGlyph
                });
            }
        }

        private void AddSeller(string id, string nick, string city, string credit, int sold, string reply, string lastSeen, string color)
        {
            _sellers[id] = new SellerInfo
            {
                UserId = id, Nick = nick, City = city, Credit = credit, SoldCount = sold,
                ReplyRatio = reply, LastVisit = lastSeen, AvatarColor = color
            };
        }

        private void AddItem(string id, string title, decimal price, decimal original, string sellerId, int want, int browse, int fav,
            bool freeShip, string condition, bool bargain, string color, string glyph, double daysAgo, string desc)
        {
            var seller = _sellers[sellerId];
            _items.Add(new ItemDetail
            {
                ItemId = id, Title = title, Price = price, OriginalPrice = original,
                City = seller.City, SellerId = seller.UserId, SellerNick = seller.Nick, Seller = seller,
                WantCount = want, BrowseCount = browse, CollectCount = fav,
                FreeShipping = freeShip, Condition = condition, Bargain = bargain,
                PlaceholderColor = color, PlaceholderGlyph = glyph,
                PublishTime = DateTimeOffset.Now.AddDays(-daysAgo),
                Description = desc,
                Images = new List<string>()
            });
        }

        private void AddConversation(string cid, string itemId, int unread, ChatMessage[] messages)
        {
            var item = _items.First(i => i.ItemId == itemId);
            var conv = NewConversation(cid, item, item.Seller);
            foreach (var m in messages)
            {
                m.Cid = cid;
                m.SenderId = m.IsMine ? SelfUserId : item.Seller.UserId;
            }
            var last = messages.Last();
            conv.LastMessage = (last.IsMine ? "我: " : string.Empty) + last.Text;
            conv.Time = last.Time;
            conv.UnreadCount = unread;
            _conversations.Add(conv);
            _messages[cid] = messages.ToList();
        }

        private static Conversation NewConversation(string cid, ItemDetail item, SellerInfo seller)
        {
            return new Conversation
            {
                Cid = cid,
                PeerUserId = seller.UserId,
                PeerNick = seller.Nick,
                PeerColor = seller.AvatarColor,
                ItemId = item?.ItemId,
                ItemTitle = item?.Title,
                ItemPrice = item != null ? item.Price : 0
            };
        }

        private static ChatMessage Msg(bool mine, string text, DateTimeOffset time)
        {
            return new ChatMessage { MessageId = Guid.NewGuid().ToString("N"), IsMine = mine, Text = text, Time = time };
        }

        /// <summary>演示用的图片/视频消息。地址是公开样例，播放与显示需要联网。</summary>
        private static ChatMessage Media(bool mine, MessageKind kind, string url, string cover, double width, double height, DateTimeOffset time)
        {
            return new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                IsMine = mine,
                Kind = kind,
                MediaUrl = url,
                MediaCover = cover,
                MediaWidth = width,
                MediaHeight = height,
                Text = kind == MessageKind.Video ? "[视频]" : "[图片]",
                Time = time
            };
        }

        /// <summary>演示用的卡片消息（宝贝卡片）。</summary>
        private static ChatMessage Card(ItemDetail item, DateTimeOffset time)
        {
            return new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                IsMine = false,
                Kind = MessageKind.Card,
                CardTitle = item.Title,
                CardDesc = item.City + " · " + (item.Condition ?? "闲置"),
                CardPrice = item.PriceText,
                CardItemId = item.ItemId,
                CardUrl = item.WebUrl,
                MediaUrl = item.CoverUrl,
                Text = item.Title,
                Time = time
            };
        }
    }
}
