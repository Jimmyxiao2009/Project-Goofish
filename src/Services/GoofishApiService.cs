using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Newtonsoft.Json;
using ProjectGoofish.Models;

namespace ProjectGoofish.Services
{
    /// <summary>
    /// 闲鱼 MTOP 核心服务客户端
    /// </summary>
    public class GoofishApiService
    {
        private static GoofishApiService _instance;
        public static GoofishApiService Instance => _instance ?? (_instance = new GoofishApiService());

        private readonly HttpClient _httpClient;
        private readonly HttpBaseProtocolFilter _protocolFilter;

        private const string AppKey = "12574478";
        private const string GatewayUrl = "https://h5api.m.goofish.com/h5/";
        private string _mtopToken = string.Empty;

        public bool UseMockSandbox { get; set; } = true;
        private readonly List<ItemDetail> _sandboxItems;
        private readonly List<Conversation> _sandboxConversations;

        private GoofishApiService()
        {
            _protocolFilter = new HttpBaseProtocolFilter();
            _httpClient = new HttpClient(_protocolFilter);
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows Phone 10.0; Android 6.0.1; Microsoft; Lumia 950 XL) AppleWebKit/537.36 Chrome/51.0 Mobile Safari/537.36 Edge/14.14393");

            // 初始化演示数据
            _sandboxItems = CreateSampleItems();
            _sandboxConversations = CreateSampleConversations();
        }

        /// <summary>
        /// 1. 获取首页推荐 Feed
        /// </summary>
        public async Task<List<ItemDetail>> GetHomeFeedAsync(int pageNumber = 1, int pageSize = 20, string category = "all")
        {
            if (UseMockSandbox)
            {
                await Task.Delay(200);
                if (category == "all") return _sandboxItems;
                return _sandboxItems.Where(i => i.Category == category).ToList();
            }

            var api = "mtop.taobao.idlehome.home.webpc.feed";
            var dataObj = new { pageNumber, pageSize, itemId = "" };
            var result = await SendMtopRequestAsync<HomeFeedResponse>(api, "1.0", dataObj);
            
            return result?.Data?.CardList?.Select(c => c.CardData).Where(i => i != null).ToList() 
                   ?? new List<ItemDetail>();
        }

        /// <summary>
        /// 2. 搜索闲置商品
        /// </summary>
        public async Task<List<ItemDetail>> SearchAsync(string keyword, int pageNumber = 1, string sortType = "default")
        {
            if (UseMockSandbox)
            {
                await Task.Delay(250);
                var kw = (keyword ?? string.Empty).ToLowerInvariant().Trim();
                var query = _sandboxItems.Where(i => 
                    i.Title.ToLowerInvariant().Contains(kw) || 
                    i.Description.ToLowerInvariant().Contains(kw) || 
                    i.City.ToLowerInvariant().Contains(kw));

                if (sortType == "price_asc") query = query.OrderBy(i => i.Price);
                else if (sortType == "price_desc") query = query.OrderByDescending(i => i.Price);

                return query.ToList();
            }

            var api = "mtop.taobao.idle.web.item.search";
            string sortField = "", sortValue = "";
            if (sortType == "price_asc") { sortField = "price"; sortValue = "asc"; }
            else if (sortType == "price_desc") { sortField = "price"; sortValue = "desc"; }

            var dataObj = new { keyword, pageNumber, rowsPerPage = 20, sortField, sortValue };
            var result = await SendMtopRequestAsync<SearchResponse>(api, "1.0", dataObj);

            return result?.Data?.ResultList ?? new List<ItemDetail>();
        }

        /// <summary>
        /// 3. 获取商品详细信息
        /// </summary>
        public async Task<ItemDetail> GetItemDetailAsync(string itemId)
        {
            if (UseMockSandbox)
            {
                await Task.Delay(150);
                var item = _sandboxItems.FirstOrDefault(i => i.ItemId == itemId);
                if (item == null) throw new InvalidOperationException("宝贝已下架或不存在");
                return item;
            }

            var api = "mtop.taobao.idle.pc.detail";
            var dataObj = new { itemId };
            var result = await SendMtopRequestAsync<ItemDetail>(api, "1.0", dataObj);

            return result?.Data;
        }

        /// <summary>
        /// 4. 切换商品收藏状态
        /// </summary>
        public async Task<bool> ToggleFavoriteAsync(string itemId)
        {
            var item = _sandboxItems.FirstOrDefault(i => i.ItemId == itemId);
            if (item != null)
            {
                item.IsFavorite = !item.IsFavorite;
                item.CollectCount += item.IsFavorite ? 1 : -1;
                return item.IsFavorite;
            }

            // 真实网关添加/取消收藏接口
            var api = "mtop.taobao.idle.favor.item.add";
            var dataObj = new { itemId };
            await SendMtopRequestAsync<object>(api, "1.0", dataObj);
            return true;
        }

        /// <summary>
        /// 5. 获取我的收藏列表
        /// </summary>
        public async Task<List<ItemDetail>> GetFavoritesAsync()
        {
            await Task.Delay(150);
            return _sandboxItems.Where(i => i.IsFavorite).ToList();
        }

        /// <summary>
        /// 6. 获取私聊会话列表
        /// </summary>
        public async Task<List<Conversation>> GetConversationsAsync()
        {
            await Task.Delay(150);
            return _sandboxConversations;
        }

        /// <summary>
        /// 7. 发送私信
        /// </summary>
        public async Task<ChatMessage> SendChatMessageAsync(string conversationId, string text)
        {
            var conv = _sandboxConversations.FirstOrDefault(c => c.Id == conversationId);
            if (conv == null) return null;

            var msg = new ChatMessage
            {
                Sender = "me",
                Text = text,
                Time = DateTime.Now.ToString("HH:mm")
            };

            conv.Messages.Add(msg);
            conv.LastMessage = text;
            conv.LastTime = msg.Time;

            // 仿真卖家智能回复
            Task.Run(async () =>
            {
                await Task.Delay(1200);
                var replies = new[]
                {
                    "宝贝在的！拍下当天就可以走顺丰发货~",
                    "诚心要的话可以小刀个邮费哈！",
                    "原装箱说齐全，配件都在，功能完全正常。",
                    "支持视频验机，您看需要看哪部分细节？"
                };
                var replyMsg = new ChatMessage
                {
                    Sender = "other",
                    Text = replies[new Random().Next(replies.Length)],
                    Time = DateTime.Now.ToString("HH:mm")
                };
                conv.Messages.Add(replyMsg);
                conv.LastMessage = replyMsg.Text;
            });

            return msg;
        }

        /// <summary>
        /// 发送标准 MTOP 网关请求（含自动握手防重放逻辑）
        /// </summary>
        private async Task<MtopResponse<T>> SendMtopRequestAsync<T>(string api, string version, object dataObj)
        {
            var dataJson = JsonConvert.SerializeObject(dataObj);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = MtopSigner.ComputeSign(_mtopToken, timestamp, AppKey, dataJson);

            var url = $"{GatewayUrl}{api}/{version}/?jsv=2.7.2&appKey={AppKey}&t={timestamp}&sign={sign}&api={api}&v={version}&type=originaljson&dataType=json&data={Uri.EscapeDataString(dataJson)}";

            var response = await _httpClient.GetAsync(new Uri(url));
            var content = await response.Content.ReadAsStringAsync();

            // 检查是否需要更新或获取 Token
            if (content.Contains("FAIL_SYS_TOKEN_EMPTY") || content.Contains("TOKEN_EXPIRED"))
            {
                var cookieManager = _protocolFilter.CookieManager;
                var cookies = cookieManager.GetCookies(new Uri("https://goofish.com"));
                var h5TokenCookie = cookies.FirstOrDefault(c => c.Name == "_m_h5_tk");
                if (h5TokenCookie != null)
                {
                    var val = h5TokenCookie.Value;
                    var idx = val.IndexOf('_');
                    _mtopToken = idx > 0 ? val.Substring(0, idx) : val;

                    // 重试请求
                    return await SendMtopRequestAsync<T>(api, version, dataObj);
                }
            }

            return JsonConvert.DeserializeObject<MtopResponse<T>>(content);
        }

        #region 沙盒样本数据初始化
        private static List<ItemDetail> CreateSampleItems()
        {
            return new List<ItemDetail>
            {
                new ItemDetail
                {
                    ItemId = "item_1001",
                    Title = "【传家宝】微软 Lumia 950 XL 港版双卡 黑色 成色极新 已刷Win11 ARM",
                    Description = "收藏级成色！屏幕完美无老化，原装电池待机正常。机器已经折腾刷入 Windows 11 on ARM，能开机进桌面跑 exe，附送 Continuum 扩展坞和原装充电头。退烧出给同好，不刀。",
                    Price = 499.00m,
                    OriginalPrice = 3999.00m,
                    City = "北京 · 海淀",
                    Category = "digital",
                    BrowseCount = 1829,
                    CollectCount = 142,
                    WantCount = 38,
                    IsFavorite = true,
                    Images = new List<string> { "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=600" },
                    Seller = new SellerInfo { Nick = "微粉老大哥", CreditScore = "芝麻信用 极好", City = "北京", Avatar = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=100" }
                },
                new ItemDetail
                {
                    ItemId = "item_1002",
                    Title = "ThinkPad X1 Carbon 2021 商务本 i7-1165G7 16G 512G 4K屏",
                    Description = "自用主力机，碳纤维A面质感无敌，小红点灵敏，键盘手感极佳。箱说全，带原装65W电源。支持当面试机，顺丰到付。",
                    Price = 3200.00m,
                    OriginalPrice = 11999.00m,
                    City = "上海 · 浦东",
                    Category = "digital",
                    BrowseCount = 3410,
                    CollectCount = 215,
                    WantCount = 62,
                    IsFavorite = false,
                    Images = new List<string> { "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?w=600" },
                    Seller = new SellerInfo { Nick = "陆家嘴打工人", CreditScore = "芝麻信用 优秀", City = "上海", Avatar = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=100" }
                }
            };
        }

        private static List<Conversation> CreateSampleConversations()
        {
            return new List<Conversation>
            {
                new Conversation
                {
                    Id = "conv_1",
                    SellerId = "seller_201",
                    SellerNick = "微粉老大哥",
                    SellerAvatar = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=100",
                    ItemId = "item_1001",
                    ItemTitle = "微软 Lumia 950 XL 港版双卡 黑色",
                    ItemPrice = 499.00m,
                    ItemThumb = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=600",
                    LastMessage = "机器支持 Continuum 扩展坞直接连显示器。",
                    LastTime = "18:24",
                    UnreadCount = 1,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Sender = "other", Text = "你好！看上这款 Lumia 950 XL 了吗？成色非常棒！", Time = "18:20" },
                        new ChatMessage { Sender = "me", Text = "请问刷好的 Win11 ARM 可以直接运行 x86 小程序吗？", Time = "18:22" },
                        new ChatMessage { Sender = "other", Text = "机器支持 Continuum 扩展坞直接连显示器，日常轻度运行工具完全没问题。", Time = "18:24" }
                    }
                }
            };
        }
        #endregion
    }
}
