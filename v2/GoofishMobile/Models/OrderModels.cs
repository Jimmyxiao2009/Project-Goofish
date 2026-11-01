using System.Collections.Generic;
using Goofish.Common;

namespace Goofish.Models
{
    /// <summary>订单页签：我买到的 / 我卖出的。</summary>
    public enum OrderTab
    {
        Bought,
        Sold
    }

    /// <summary>买家订单的状态筛选，取值与 mtop.idle.web.trade.bought.list 的 orderStatus 一一对应。</summary>
    public enum BoughtOrderStatus
    {
        All,
        NotPay,
        NotShip,
        Shipped,
        NotRate,
        Refund
    }

    /// <summary>订单列表里的一条。</summary>
    public sealed class OrderSummary : BindableBase
    {
        public string OrderId { get; set; }
        public string ItemId { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        /// <summary>对方（买家视角是卖家，卖家视角是买家）。</summary>
        public string PeerUserId { get; set; }
        public string PeerNick { get; set; }
        public string PeerAvatar { get; set; }

        /// <summary>状态文案，如「等待买家付款」。</summary>
        public string StatusText { get; set; }
        public string CreateTime { get; set; }

        /// <summary>true 表示这是我卖出的订单（决定详情链接的 role 与文案）。</summary>
        public bool IsSold { get; set; }

        /// <summary>演示数据没有网络图片，用纯色块 + 图标代替。</summary>
        public string PlaceholderColor { get; set; } = "#FF3A3A3A";
        public string PlaceholderGlyph { get; set; } = "\uE7B8";

        public string PriceText => TextFormat.Price(Price);

        public string QuantityText => Quantity > 1 ? "×" + Quantity : null;

        public string PeerLine => Join(PeerNick, CreateTime);

        public string WebUrl => "https://www.goofish.com/order-detail?orderId=" + OrderId + "&role=" + (IsSold ? "seller" : "buyer");

        public override string ToString()
        {
            return (Title ?? "订单") + "，" + StatusText;
        }

        private static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
            if (string.IsNullOrEmpty(b)) return a;
            return a + " · " + b;
        }
    }

    /// <summary>
    /// 订单详情。接口返回的是「组件数组」，不同订单的组件组合会变，
    /// 这里只把稳定可见的几项抽出来，其余键值对原样列在 <see cref="InfoItems"/>。
    /// </summary>
    public sealed class OrderDetail
    {
        public string OrderId { get; set; }
        public string ItemId { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public string SkuText { get; set; }
        public string StatusText { get; set; }
        public string CreateTime { get; set; }
        public string BuyerId { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverPhone { get; set; }
        public string AddressText { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public bool IsSold { get; set; }
        public string PlaceholderColor { get; set; } = "#FF3A3A3A";
        public string PlaceholderGlyph { get; set; } = "\uE7B8";

        public List<KeyValuePair<string, string>> InfoItems { get; } = new List<KeyValuePair<string, string>>();

        public string PriceText => TextFormat.Price(Price);

        public string QuantityText => Quantity > 1 ? "×" + Quantity : null;

        public string ReceiverLine
        {
            get
            {
                var name = ReceiverName ?? string.Empty;
                var phone = ReceiverPhone ?? string.Empty;
                if (name.Length == 0) return phone;
                return phone.Length == 0 ? name : name + "  " + phone;
            }
        }

        public string AmountLine => Quantity > 0 ? "共 " + Quantity + " 件，合计 " + PriceText : PriceText;

        public string WebUrl => "https://www.goofish.com/order-detail?orderId=" + OrderId + "&role=" + (IsSold ? "seller" : "buyer");
    }
}
