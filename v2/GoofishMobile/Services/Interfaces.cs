using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Goofish.Models;

namespace Goofish.Services
{
    /// <summary>商品数据来源。真实实现走 mtop，演示实现用本地样例。</summary>
    public interface IGoofishDataSource
    {
        Task<PageResult<ItemSummary>> GetHomeFeedAsync(int page);

        Task<PageResult<ItemSummary>> SearchAsync(string keyword, SearchSort sort, int page);

        Task<ItemDetail> GetItemDetailAsync(string itemId);

        /// <summary>卖家 / 个人主页（昵称、头像、粉丝、关注、在售、评价、信用）。</summary>
        Task<UserProfile> GetUserProfileAsync(string userId);

        Task<PageResult<ItemSummary>> GetFavoritesAsync(FavoriteFilter filter, int page);

        bool IsFavorite(string itemId);

        /// <summary>返回 false 表示只保存在本机、没能同步到闲鱼。</summary>
        Task<bool> SetFavoriteAsync(ItemSummary item, bool favorite);

        /// <summary>我买到的订单（按状态筛选）。</summary>
        Task<PageResult<OrderSummary>> GetBoughtOrdersAsync(BoughtOrderStatus status, int page);

        /// <summary>我卖出的订单。</summary>
        Task<PageResult<OrderSummary>> GetSoldOrdersAsync(int page);

        /// <summary>订单详情。role 决定视角，卖家侧与买家侧共用同一个接口。</summary>
        Task<OrderDetail> GetOrderDetailAsync(string orderId, bool isSold);
    }

    /// <summary>私信通道。真实实现是钉钉 IM 的 WebSocket 长连接。</summary>
    public interface IChatService
    {
        /// <summary>收到别人发来的消息。可能在后台线程触发。</summary>
        event EventHandler<ChatMessage> MessageReceived;

        string SelfUserId { get; }

        /// <summary>
        /// 会话列表。真实实现走 WebSocket 的最新分页接口（网页版左栏的数据源），
        /// 因为 mtop 的 session.sync 只返回活跃 Top N，会漏掉最新会话。
        /// </summary>
        Task<IList<Conversation>> GetConversationsAsync();

        Task<IList<ChatMessage>> GetMessagesAsync(string cid);

        /// <summary>针对某件宝贝与卖家建立（或复用）会话，返回 cid。</summary>
        Task<string> OpenConversationAsync(string peerUserId, string itemId);

        Task SendTextAsync(string cid, string peerUserId, ChatMessage message);

        void Disconnect();
    }
}
