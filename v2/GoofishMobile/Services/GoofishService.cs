using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Goofish.Services
{
    /// <summary>
    /// 真实数据源。接口名与参数取自闲鱼网页版（www.goofish.com）实际发出的请求，详见 docs/API.md。
    /// </summary>
    public sealed class GoofishService : IGoofishDataSource
    {
        public const string FeedApi = "mtop.taobao.idlehome.home.webpc.feed";
        public const string SearchApi = "mtop.taobao.idlemtopsearch.pc.search";
        public const string DetailApi = "mtop.taobao.idle.pc.detail";
        public const string FavoriteListApi = "mtop.taobao.idle.web.favor.item.list";

        // 卖家 / 个人主页（docs/Capabilities.md #3，单一来源，字段解析容错）
        public const string UserHeadApi = "mtop.idle.web.user.page.head";

        // 订单：买家列表在 www.goofish.com，卖家列表在 seller.goofish.com（是两套接口），详情共用
        public const string BoughtOrderApi = "mtop.idle.web.trade.bought.list";
        public const string SoldOrderApi = "mtop.taobao.idle.trade.merchant.sold.get";
        public const string OrderDetailApi = "mtop.idle.web.trade.order.detail";

        // 收藏/取消收藏的接口在公开资料里没有可靠记录，这两个名字需要在 goofish.com 上
        // 点一次“收藏”用开发者工具核对。调用失败时只保存在本机，并告诉用户没有同步。
        public const string FavoriteAddApi = "mtop.taobao.idle.collect.item";
        public const string FavoriteRemoveApi = "mtop.taobao.idle.collect.item.cancel";

        private const int SearchPageSize = 30;
        private const int FavoritePageSize = 20;
        private const int SoldPageSize = 30;

        private readonly MtopClient _mtop;
        private readonly FavoritesStore _favorites;
        private readonly Dictionary<string, UserProfile> _profileCache = new Dictionary<string, UserProfile>();

        public GoofishService(MtopClient mtop, FavoritesStore favorites)
        {
            _mtop = mtop;
            _favorites = favorites;
        }

        public async Task<PageResult<ItemSummary>> GetHomeFeedAsync(int page)
        {
            var data = await _mtop.CallAsync(FeedApi, "1.0", new { itemId = "", pageSize = 30, pageNumber = page, machId = "" });
            return GoofishParser.ParseFeed(data);
        }

        public async Task<PageResult<ItemSummary>> SearchAsync(string keyword, SearchSort sort, int page)
        {
            string sortField = "", sortValue = "";
            switch (sort)
            {
                case SearchSort.Newest: sortField = "create"; sortValue = "desc"; break;
                case SearchSort.PriceAscending: sortField = "price"; sortValue = "asc"; break;
                case SearchSort.PriceDescending: sortField = "price"; sortValue = "desc"; break;
            }

            // 字段布局与网页版搜索页发出的请求体保持一致
            var body = new Dictionary<string, object>
            {
                { "pageNumber", page },
                { "keyword", keyword },
                { "fromFilter", false },
                { "rowsPerPage", SearchPageSize },
                { "sortValue", sortValue },
                { "sortField", sortField },
                { "customDistance", "" },
                { "gps", "" },
                { "propValueStr", new { searchFilter = "" } },
                { "customGps", "" },
                { "searchReqFromPage", "pcSearch" },
                { "extraFilterValue", "{}" },
                { "userPositionJson", "{}" }
            };
            var data = await _mtop.CallAsync(SearchApi, "1.0", JsonConvert.SerializeObject(body));
            return GoofishParser.ParseSearch(data, SearchPageSize);
        }

        public async Task<ItemDetail> GetItemDetailAsync(string itemId)
        {
            var data = await _mtop.CallAsync(DetailApi, "1.0", new { itemId });
            var detail = GoofishParser.ParseDetail(data);
            if (string.IsNullOrEmpty(detail.ItemId)) detail.ItemId = itemId;
            return detail;
        }

        public async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) throw new MtopException(MtopErrorKind.Api, null, "没有用户 ID。");

            // 会话列表补昵称/头像会对每个对方调一次，缓存起来避免重复请求（也少碰风控）
            lock (_profileCache)
            {
                UserProfile hit;
                if (_profileCache.TryGetValue(userId, out hit)) return hit;
            }

            var data = await _mtop.CallAsync(UserHeadApi, "1.0", new { userId });
            var profile = GoofishParser.ParseUserProfile(data);
            if (string.IsNullOrEmpty(profile.UserId)) profile.UserId = userId;

            // 什么都没解析出来就别缓存，免得以后一直拿到空结果
            if (!string.IsNullOrEmpty(profile.Nick) || !string.IsNullOrEmpty(profile.AvatarUrl))
            {
                lock (_profileCache) _profileCache[userId] = profile;
            }
            return profile;
        }

        public async Task<PageResult<ItemSummary>> GetFavoritesAsync(FavoriteFilter filter, int page)
        {
            var type = filter == FavoriteFilter.Reduced ? "REDUCE"
                : filter == FavoriteFilter.Valid ? "VALID"
                : filter == FavoriteFilter.Invalid ? "INVALID"
                : "DEFAULT";
            var data = await _mtop.CallAsync(FavoriteListApi, "1.0", new { pageNumber = page, rowsPerPage = FavoritePageSize, type });
            var result = GoofishParser.ParseFavorites(data);
            _favorites.RememberRemote(result.Items.Select(i => i.ItemId));

            // 第一页里补上只存在于本机的收藏（远端同步失败的那些）
            if (page == 1 && filter == FavoriteFilter.All)
            {
                var remote = new HashSet<string>(result.Items.Select(i => i.ItemId));
                var localOnly = _favorites.All.Where(i => !remote.Contains(i.ItemId)).ToList();
                if (localOnly.Count > 0)
                    return new PageResult<ItemSummary>(localOnly.Concat(result.Items).ToList(), result.HasMore, result.Total);
            }
            return result;
        }

        public bool IsFavorite(string itemId)
        {
            return _favorites.Contains(itemId);
        }

        public async Task<bool> SetFavoriteAsync(ItemSummary item, bool favorite)
        {
            var synced = true;
            try
            {
                await _mtop.CallAsync(favorite ? FavoriteAddApi : FavoriteRemoveApi, "1.0", new { itemId = item.ItemId });
            }
            catch (MtopException ex) when (ex.Kind == MtopErrorKind.Api)
            {
                synced = false;
            }
            await _favorites.SetAsync(item, favorite);
            return synced;
        }

        public async Task<PageResult<OrderSummary>> GetBoughtOrdersAsync(BoughtOrderStatus status, int page)
        {
            var data = await _mtop.CallAsync(BoughtOrderApi, "1.0", new { pageNumber = page, orderStatus = StatusValue(status) });
            return OrderParser.ParseBought(data);
        }

        public async Task<PageResult<OrderSummary>> GetSoldOrdersAsync(int page)
        {
            // 卖家端接口对 Origin / Referer 与 idle_site_biz_code 敏感，缺了会被判成没有权限
            var headers = new Dictionary<string, string>
            {
                { "Origin", "https://seller.goofish.com" },
                { "Referer", "https://seller.goofish.com/" },
                { "idle_site_biz_code", "COMMONPRO" }
            };
            var body = new Dictionary<string, object>
            {
                { "pageNumber", page },
                { "rowsPerPage", SoldPageSize },
                { "orderIds", "" },
                { "queryCode", "ALL" },
                { "orderSearchParam", "{}" }
            };
            var data = await _mtop.CallAsync(SoldOrderApi, "1.0", JsonConvert.SerializeObject(body), headers);
            return OrderParser.ParseSold(data);
        }

        public async Task<OrderDetail> GetOrderDetailAsync(string orderId, bool isSold)
        {
            var data = await _mtop.CallAsync(OrderDetailApi, "1.0", new { tid = orderId });
            var detail = OrderParser.ParseDetail(data, orderId, isSold);
            if (string.IsNullOrEmpty(detail.OrderId)) detail.OrderId = orderId;
            return detail;
        }

        private static string StatusValue(BoughtOrderStatus status)
        {
            switch (status)
            {
                case BoughtOrderStatus.NotPay: return "NOT_PAY";
                case BoughtOrderStatus.NotShip: return "NOT_SHIP";
                case BoughtOrderStatus.Shipped: return "SHIPPED";
                case BoughtOrderStatus.NotRate: return "NOT_RATE";
                case BoughtOrderStatus.Refund: return "REFUND";
                default: return "ALL";
            }
        }
    }
}
