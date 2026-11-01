using System;
using System.Collections.Generic;
using Goofish.Common;

namespace Goofish.Models
{
    public sealed class PageResult<T>
    {
        public PageResult(IList<T> items, bool hasMore, int total = -1)
        {
            Items = items ?? new List<T>();
            HasMore = hasMore;
            Total = total;
        }

        public IList<T> Items { get; }
        public bool HasMore { get; }
        public int Total { get; }
    }

    public enum SearchSort
    {
        Default,
        Newest,
        PriceAscending,
        PriceDescending
    }

    /// <summary>与 mtop.taobao.idle.web.favor.item.list 的 type 参数一一对应。</summary>
    public enum FavoriteFilter
    {
        All,
        Reduced,
        Valid,
        Invalid
    }

    /// <summary>列表里的一件宝贝：推荐、搜索结果、收藏夹共用。</summary>
    public class ItemSummary : BindableBase
    {
        public string ItemId { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string CoverUrl { get; set; }
        public string City { get; set; }
        public string SellerId { get; set; }
        public string SellerNick { get; set; }
        public string SellerAvatar { get; set; }
        public int WantCount { get; set; }
        public bool FreeShipping { get; set; }
        public string Condition { get; set; }

        /// <summary>收藏夹专用：降价金额与是否已下架。</summary>
        public decimal ReducedBy { get; set; }
        public bool IsInvalid { get; set; }

        /// <summary>演示数据没有网络图片，用纯色块 + 图标代替。</summary>
        public string PlaceholderColor { get; set; } = "#FF3A3A3A";
        public string PlaceholderGlyph { get; set; } = "\uE7B8";

        public string PriceText => TextFormat.Price(Price);

        public string OriginalPriceText => OriginalPrice > Price ? TextFormat.Price(OriginalPrice) : null;

        public string WantText => WantCount > 0 ? WantCount + "人想要" : null;

        public string TileMeta => Join(WantText, City);

        public string RowMeta => Join(City, SellerNick);

        public string ReducedText => ReducedBy > 0 ? "降 " + TextFormat.Price(ReducedBy) : null;

        public string InvalidText => IsInvalid ? "已失效" : null;

        public string ShippingBadge => FreeShipping ? "包邮" : null;

        public double DimOpacity => IsInvalid ? 0.45 : 1.0;

        public string WebUrl => "https://www.goofish.com/item?id=" + ItemId;

        /// <summary>列表项的无障碍名称（讲述人朗读的内容）默认取 ToString()。</summary>
        public override string ToString()
        {
            return Title + "，" + PriceText;
        }

        private static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
            if (string.IsNullOrEmpty(b)) return a;
            return a + " · " + b;
        }
    }

    public sealed class SellerInfo
    {
        public string UserId { get; set; }
        public string Nick { get; set; }
        public string AvatarUrl { get; set; }
        public string City { get; set; }
        public string Credit { get; set; }
        public int SoldCount { get; set; }
        public string ReplyRatio { get; set; }
        public string LastVisit { get; set; }
        public string AvatarColor { get; set; } = "#FF767676";

        public string Initial => string.IsNullOrEmpty(Nick) ? "?" : Nick.Substring(0, 1);

        public string CreditLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Credit)) parts.Add("卖家信用" + Credit);
                if (SoldCount > 0) parts.Add("已卖出 " + SoldCount + " 件");
                return parts.Count == 0 ? (City ?? string.Empty) : string.Join(" · ", parts);
            }
        }

        public string ActivityLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(LastVisit)) parts.Add(LastVisit);
                if (!string.IsNullOrEmpty(ReplyRatio)) parts.Add("24 小时回复率 " + ReplyRatio);
                return string.Join(" · ", parts);
            }
        }
    }

    /// <summary>
    /// 卖家 / 个人主页。接口是 mtop.idle.web.user.page.head（单一来源，字段名以实测为准），
    /// 解析全部容错，取不到的字段留空，页面按“有多少显示多少”渲染。
    /// </summary>
    public sealed class UserProfile
    {
        public string UserId { get; set; }
        public string Nick { get; set; }
        public string AvatarUrl { get; set; }
        public string City { get; set; }
        public string Signature { get; set; }
        public string Credit { get; set; }
        public int SoldCount { get; set; }
        public int ItemCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public int RateCount { get; set; }
        public string ReplyRatio { get; set; }
        public string LastVisit { get; set; }
        public bool Verified { get; set; }
        public string AvatarColor { get; set; } = "#FF767676";

        public string Initial => string.IsNullOrEmpty(Nick) ? "?" : Nick.Substring(0, 1);

        public string DisplayName => string.IsNullOrEmpty(Nick) ? "闲鱼用户" : Nick;

        public string VerifiedText => Verified ? "已实名认证" : null;

        public string StatsText
        {
            get
            {
                var parts = new List<string>();
                if (ItemCount > 0) parts.Add("在售 " + ItemCount);
                if (SoldCount > 0) parts.Add("已卖出 " + SoldCount);
                if (FollowerCount > 0) parts.Add("粉丝 " + FollowerCount);
                if (FollowingCount > 0) parts.Add("关注 " + FollowingCount);
                if (RateCount > 0) parts.Add("评价 " + RateCount);
                return parts.Count == 0 ? null : string.Join(" · ", parts);
            }
        }

        public string CreditLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Credit)) parts.Add("卖家信用" + Credit);
                if (!string.IsNullOrEmpty(City)) parts.Add(City);
                return parts.Count == 0 ? null : string.Join(" · ", parts);
            }
        }

        public string ActivityLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(LastVisit)) parts.Add(LastVisit);
                if (!string.IsNullOrEmpty(ReplyRatio)) parts.Add("24 小时回复率 " + ReplyRatio);
                return parts.Count == 0 ? null : string.Join(" · ", parts);
            }
        }
    }

    public sealed class ItemDetail : ItemSummary
    {
        public List<string> Images { get; set; } = new List<string>();
        public string Description { get; set; }
        public int BrowseCount { get; set; }
        public int CollectCount { get; set; }
        public string TransportFee { get; set; }
        /// <summary>接口不一定给出议价信息，null 时详情页不显示这一行。</summary>
        public bool? Bargain { get; set; }
        public DateTimeOffset PublishTime { get; set; } = DateTimeOffset.MinValue;
        public SellerInfo Seller { get; set; } = new SellerInfo();

        public string StatsText
        {
            get
            {
                var parts = new List<string>();
                if (WantCount > 0) parts.Add(WantCount + " 人想要");
                if (BrowseCount > 0) parts.Add(BrowseCount + " 次浏览");
                if (CollectCount > 0) parts.Add(CollectCount + " 人收藏");
                return string.Join(" · ", parts);
            }
        }

        public string ShippingText
        {
            get
            {
                if (FreeShipping) return "包邮";
                decimal fee;
                if (decimal.TryParse(TransportFee, out fee) && fee > 0) return "运费 " + TextFormat.Price(fee);
                return string.IsNullOrEmpty(TransportFee) ? null : TransportFee;
            }
        }

        public string BargainText => Bargain == null ? null : (Bargain.Value ? "可小刀" : "不议价");

        public string PublishText => TextFormat.Relative(PublishTime);

        public string IdText => "闲鱼商品 ID " + ItemId;
    }
}
