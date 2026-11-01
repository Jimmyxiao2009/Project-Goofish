using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjectGoofish.Models
{
    /// <summary>
    /// 即时通讯会话条目 (用于消息列表展示)
    /// </summary>
    public class Conversation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sellerId")]
        public string SellerId { get; set; }

        [JsonProperty("sellerNick")]
        public string SellerNick { get; set; }

        [JsonProperty("sellerAvatar")]
        public string SellerAvatar { get; set; }

        [JsonProperty("itemId")]
        public string ItemId { get; set; }

        [JsonProperty("itemTitle")]
        public string ItemTitle { get; set; }

        [JsonProperty("itemPrice")]
        public decimal ItemPrice { get; set; }

        [JsonProperty("itemThumb")]
        public string ItemThumb { get; set; }

        [JsonProperty("lastMessage")]
        public string LastMessage { get; set; }

        [JsonProperty("lastTime")]
        public string LastTime { get; set; }

        [JsonProperty("unreadCount")]
        public int UnreadCount { get; set; }

        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    /// <summary>
    /// 单条对话消息
    /// </summary>
    public class ChatMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("sender")]
        public string Sender { get; set; } // "me" 或 "other"

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm");

        [JsonIgnore]
        public bool IsOutgoing => Sender == "me";
    }

    /// <summary>
    /// 钉钉/闲鱼 WebSocket 注册数据包
    /// </summary>
    public class ImRegisterPacket
    {
        [JsonProperty("uri")]
        public string Uri { get; set; } = "/reg";

        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonProperty("body")]
        public ImRegisterBody Body { get; set; } = new ImRegisterBody();
    }

    public class ImRegisterBody
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("appKey")]
        public string AppKey { get; set; } = "12574478";
    }
}
