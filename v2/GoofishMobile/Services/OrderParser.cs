using System;
using System.Collections.Generic;
using System.Globalization;
using Goofish.Common;
using Goofish.Models;
using Newtonsoft.Json.Linq;

namespace Goofish.Services
{
    /// <summary>
    /// 订单返回体的解析。买家列表、卖家列表、订单详情的字段路径差别很大，集中放在这里。
    /// 字段名来自公开项目的记录（见 docs/API.md），取不到就留空，不抛异常。
    /// </summary>
    public static class OrderParser
    {
        /// <summary>我买到的：data.items[]（mtop.idle.web.trade.bought.list）。</summary>
        public static PageResult<OrderSummary> ParseBought(JToken data)
        {
            var list = new List<OrderSummary>();
            foreach (var item in data.Items("items"))
            {
                list.Add(new OrderSummary
                {
                    OrderId = item.Str("commonData.orderId", "commonData.orderIdStr"),
                    ItemId = item.Str("commonData.itemId"),
                    Title = item.Str("content.data.detailInfo.auctionTitle"),
                    CoverUrl = item.Str("content.data.detailInfo.auctionPic"),
                    Price = Money(item.Str("content.data.priceInfo.price", "content.data.priceInfo.auctionPrice")),
                    Quantity = item.Int("content.data.priceInfo.buyAmount"),
                    PeerUserId = item.Str("head.data.userInfo.userId", "commonData.peerUserId"),
                    PeerNick = item.Str("head.data.userInfo.userNick"),
                    PeerAvatar = item.Str("head.data.userInfo.userIcon"),
                    StatusText = item.Str("head.data.statusViewMsg", "commonData.tradeStatusEnum"),
                    CreateTime = item.Str("head.data.createTime"),
                    IsSold = false
                });
            }
            return new PageResult<OrderSummary>(list, HasMore(data.Str("nextPage")));
        }

        /// <summary>我卖出的：data.module.items[]（mtop.taobao.idle.trade.merchant.sold.get）。</summary>
        public static PageResult<OrderSummary> ParseSold(JToken data)
        {
            var module = data.At("module") ?? data;
            var list = new List<OrderSummary>();
            foreach (var item in module.Items("items"))
            {
                list.Add(new OrderSummary
                {
                    OrderId = item.Str("commonData.orderId"),
                    ItemId = item.Str("commonData.itemId"),
                    Title = item.Str("itemInfoVO.title", "commonData.title", "content.data.detailInfo.auctionTitle"),
                    CoverUrl = item.Str("itemInfoVO.picUrl", "itemInfoVO.auctionPic", "commonData.picUrl"),
                    Price = Money(item.Str("priceVO.totalPrice", "priceVO.confirmFee", "priceVO.auctionPrice")),
                    Quantity = item.Int("priceVO.buyNum"),
                    PeerUserId = item.Str("buyerInfoVO.buyerId"),
                    PeerNick = item.Str("buyerInfoVO.userNick"),
                    StatusText = item.Str("commonData.orderStatus"),
                    CreateTime = item.Str("commonData.createTime"),
                    IsSold = true
                });
            }
            return new PageResult<OrderSummary>(list, HasMore(module.Str("nextPage")));
        }

        /// <summary>
        /// 订单详情：data.components[]（mtop.idle.web.trade.order.detail）。
        /// 组件按 render 分类，orderInfoVO / addressInfoVO / orderStatusVO 三种已知，其余忽略。
        /// </summary>
        public static OrderDetail ParseDetail(JToken data, string orderId, bool isSold)
        {
            var detail = new OrderDetail
            {
                OrderId = orderId,
                IsSold = isSold,
                ItemId = data.Str("itemId")
            };

            foreach (var component in data.Items("components"))
            {
                var render = component.Str("render") ?? string.Empty;
                var info = component.At("data") ?? component;

                if (render.Equals("orderInfoVO", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(detail.ItemId)) detail.ItemId = info.Str("itemInfo.itemId");
                    detail.Title = info.Str("itemInfo.title", "auctionTitle");
                    detail.CoverUrl = info.Str("itemInfo.picUrl", "itemInfo.auctionPic");
                    detail.SkuText = info.Str("itemInfo.skuInfo");
                    detail.Price = Money(info.Str("itemInfo.price", "priceInfo.amount.value", "priceInfo.amount"));
                    detail.Quantity = info.Int("itemInfo.buyAmount", "priceInfo.buyAmount");
                    detail.BuyerId = info.Str("buyerUserId", "buyerId");

                    foreach (var row in info.Items("orderInfoList"))
                    {
                        var label = row.Str("label", "key", "name");
                        var value = row.Str("value", "text");
                        if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(value)) continue;
                        detail.InfoItems.Add(new KeyValuePair<string, string>(label ?? string.Empty, value ?? string.Empty));
                        if (string.IsNullOrEmpty(detail.CreateTime) && label != null && label.Contains("时间"))
                            detail.CreateTime = value;
                    }
                }
                else if (render.Equals("addressInfoVO", StringComparison.OrdinalIgnoreCase))
                {
                    detail.ReceiverName = info.Str("name");
                    detail.ReceiverPhone = info.Str("phoneNumber", "phone");
                    detail.AddressText = info.Str("address");
                }
                else if (render.Equals("orderStatusVO", StringComparison.OrdinalIgnoreCase))
                {
                    detail.StatusText = info.Str("orderStatusNodeList[0].title", "statusText", "statusViewMsg");
                }
            }

            if (string.IsNullOrEmpty(detail.StatusText))
                detail.StatusText = StatusFromCode(data.Str("utArgs.orderStatus"));
            return detail;
        }

        /// <summary>详情接口顶层 data.utArgs.orderStatus 是数字状态码。</summary>
        private static string StatusFromCode(string code)
        {
            switch (code)
            {
                case "1": return "处理中";
                case "2": return "待发货";
                case "3": return "已发货";
                case "4": return "交易成功";
                case "11": return "交易成功";
                case "7":
                case "9": return "退款中";
                case "8":
                case "10":
                case "12": return "交易关闭";
                default: return null;
            }
        }

        private static bool HasMore(string nextPage)
        {
            return string.Equals(nextPage, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>价格可能带货币符号或千分位，尽量抽出数值。</summary>
        private static decimal Money(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            decimal value;
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
            var digits = new System.Text.StringBuilder();
            foreach (var c in text)
            {
                if (char.IsDigit(c) || c == '.') digits.Append(c);
            }
            return decimal.TryParse(digits.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
        }
    }
}
