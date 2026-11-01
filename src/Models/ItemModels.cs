using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjectGoofish.Models
{
    /// <summary>
    /// 阿里巴巴 MTOP 统一外层响应包裹体
    /// </summary>
    public class MtopResponse<T>
    {
        [JsonProperty("api")]
        public string Api { get; set; }

        [JsonProperty("v")]
        public string Version { get; set; }

        [JsonProperty("ret")]
        public List<string> Ret { get; set; } = new List<string>();

        [JsonProperty("data")]
        public T Data { get; set; }

        /// <summary>
        /// 是否调用成功 (以 SUCCESS:: 开头)
        /// </summary>
        [JsonIgnore]
        public bool IsSuccess => Ret != null && Ret.Count > 0 && Ret[0].StartsWith("SUCCESS");

        /// <summary>
        /// 错误原因或详细消息
        /// </summary>
        [JsonIgnore]
        public string ErrorMessage => Ret != null && Ret.Count > 0 ? Ret[0] : "未知错误";
    }

    /// <summary>
    /// 首页推荐 Feed 响应结果
    /// </summary>
    public class HomeFeedResponse
    {
        [JsonProperty("cardList")]
        public List<FeedCardItem> CardList { get; set; } = new List<FeedCardItem>();

        [JsonProperty("feedsCount")]
        public int FeedsCount { get; set; }

        [JsonProperty("nextPage")]
        public bool NextPage { get; set; }

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; }
    }

    public class FeedCardItem
    {
        [JsonProperty("cardType")]
        public int CardType { get; set; }

        [JsonProperty("cardData")]
        public ItemDetail CardData { get; set; }
    }

    /// <summary>
    /// 商品完整信息实体 (对应 itemDO / cardData)
    /// </summary>
    public class ItemDetail
    {
        [JsonProperty("itemId")]
        public string ItemId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("originalPrice")]
        public decimal? OriginalPrice { get; set; }

        [JsonProperty("transportFee")]
        public string TransportFee { get; set; } = "包邮";

        [JsonProperty("images")]
        public List<string> Images { get; set; } = new List<string>();

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("browseCnt")]
        public int BrowseCount { get; set; }

        [JsonProperty("collectCnt")]
        public int CollectCount { get; set; }

        [JsonProperty("wantCnt")]
        public int WantCount { get; set; }

        [JsonProperty("isFav")]
        public bool IsFavorite { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; } = "digital";

        [JsonProperty("seller")]
        public SellerInfo Seller { get; set; }

        [JsonIgnore]
        public string PrimaryImageUrl => Images != null && Images.Count > 0 ? Images[0] : string.Empty;

        [JsonIgnore]
        public string FormattedPrice => $"¥ {Price:F2}";
    }

    /// <summary>
    /// 卖家画像与信用等级实体
    /// </summary>
    public class SellerInfo
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("nick")]
        public string Nick { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("creditScore")]
        public string CreditScore { get; set; } = "芝麻信用 极好";

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }

        [JsonProperty("soldCount")]
        public int SoldCount { get; set; }

        [JsonProperty("rating")]
        public string Rating { get; set; } = "100% 好评";
    }

    /// <summary>
    /// 商品搜索响应
    /// </summary>
    public class SearchResponse
    {
        [JsonProperty("resultList")]
        public List<ItemDetail> ResultList { get; set; } = new List<ItemDetail>();

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }
    }
}
