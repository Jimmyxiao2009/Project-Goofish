using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Goofish.Common;
using Goofish.Models;
using Newtonsoft.Json.Linq;

namespace Goofish.Services
{
    /// <summary>
    /// mtop 响应 → 模型。字段路径来自开源项目对网页版请求的实测（见 docs/API.md），
    /// 每个字段都列了多个候选路径，接口小改版时不至于整页空白。
    /// </summary>
    public static class GoofishParser
    {
        private static readonly Regex WantPattern = new Regex(@"(\d+)\s*人想要");
        private static readonly Regex CreditPattern = new Regex(@"^卖家信用(.+)$");

        /// <summary>mtop.taobao.idlehome.home.webpc.feed</summary>
        public static PageResult<ItemSummary> ParseFeed(JToken data)
        {
            var items = new List<ItemSummary>();
            foreach (var card in data.Items("cardList"))
            {
                var d = card["cardData"] ?? card;
                var item = new ItemSummary
                {
                    ItemId = d.Str("itemId", "detailParams.itemId", "id"),
                    Title = d.Str("titleSummary.text", "title", "detailParams.title"),
                    Price = TextFormat.ParsePrice(d.Str("priceInfo.price", "detailParams.soldPrice", "price")),
                    OriginalPrice = TextFormat.ParsePrice(d.Str("priceInfo.oriPrice", "oriPrice")),
                    City = d.Str("city", "area"),
                    SellerNick = d.Str("user.userNick", "detailParams.userNick"),
                    SellerAvatar = TextFormat.NormalizeUrl(d.Str("user.avatar")),
                    CoverUrl = TextFormat.NormalizeUrl(d.Str("mainPicInfo.url", "images[0].url", "picUrl", "detailParams.picUrl")),
                    WantCount = FindWant(d),
                    FreeShipping = ContainsText(d["fishTags"], "包邮")
                };
                if (IsUsable(item)) items.Add(item);
            }
            return new PageResult<ItemSummary>(items, data.Bool("nextPage"));
        }

        /// <summary>mtop.taobao.idlemtopsearch.pc.search：resultList[].data.item.main.{exContent, clickParam}</summary>
        public static PageResult<ItemSummary> ParseSearch(JToken data, int rowsPerPage)
        {
            var items = new List<ItemSummary>();
            foreach (var row in data.Items("resultList"))
            {
                var main = row.At("data.item.main") ?? row;
                var ex = main["exContent"] ?? main;
                var args = main.At("clickParam.args");
                var item = new ItemSummary
                {
                    ItemId = ex.Str("itemId", "detailParams.itemId") ?? args.Str("item_id", "id"),
                    Title = ex.Str("title", "detailParams.title"),
                    Price = TextFormat.ParsePrice(ex.Str("price", "detailParams.soldPrice") ?? args.Str("price", "displayPrice")),
                    OriginalPrice = TextFormat.ParsePrice(ex.Str("oriPrice", "originalPrice")),
                    City = ex.Str("area", "city", "userArea"),
                    SellerNick = ex.Str("userNickName", "detailParams.userNick", "nick"),
                    SellerAvatar = TextFormat.NormalizeUrl(ex.Str("userAvatarUrl", "userAvatar")),
                    CoverUrl = TextFormat.NormalizeUrl(ex.Str("picUrl", "mainPic", "detailParams.picUrl")),
                    WantCount = FindWant(ex),
                    FreeShipping = ContainsText(ex["fishTags"], "包邮")
                };
                if (IsUsable(item)) items.Add(item);
            }
            var total = data.Int("resultInfo.searchResultTotalCount", "resultInfo.totalCount", "totalCount");
            var hasMore = data.At("resultInfo.hasNextPage") != null ? data.Bool("resultInfo.hasNextPage") : items.Count >= rowsPerPage;
            return new PageResult<ItemSummary>(items, hasMore, total > 0 ? total : -1);
        }

        /// <summary>mtop.taobao.idle.pc.detail：itemDO + sellerDO</summary>
        public static ItemDetail ParseDetail(JToken data)
        {
            var item = data["itemDO"] ?? data;
            var seller = data["sellerDO"] ?? new JObject();

            var detail = new ItemDetail
            {
                ItemId = item.Str("itemId", "id"),
                Title = item.Str("title"),
                Description = item.Str("desc"),
                Price = TextFormat.ParsePrice(item.Str("soldPrice", "minPrice", "price")),
                OriginalPrice = TextFormat.ParsePrice(item.Str("originalPrice")),
                TransportFee = item.Str("transportFee"),
                BrowseCount = item.Int("browseCnt"),
                CollectCount = item.Int("collectCnt"),
                WantCount = item.Int("wantCnt"),
                PublishTime = TextFormat.FromUnix(item.Long("gmtCreate")),
                City = seller.Str("city") ?? item.Str("city", "area"),
                Seller = new SellerInfo
                {
                    UserId = seller.Str("sellerId", "userId"),
                    Nick = seller.Str("nick", "uniqueName"),
                    AvatarUrl = TextFormat.NormalizeUrl(seller.Str("portraitUrl", "avatar")),
                    City = seller.Str("city"),
                    SoldCount = seller.Int("hasSoldNumInteger", "hasSoldNum"),
                    ReplyRatio = seller.Str("replyRatio24h"),
                    LastVisit = seller.Str("lastVisitTime"),
                    Credit = FindCredit(seller)
                }
            };

            foreach (var image in item.Items("imageInfos"))
            {
                var url = TextFormat.NormalizeUrl(image.Str("url"));
                if (url != null) detail.Images.Add(url);
            }
            detail.CoverUrl = detail.Images.FirstOrDefault();
            detail.SellerId = detail.Seller.UserId;
            detail.SellerNick = detail.Seller.Nick;
            detail.SellerAvatar = detail.Seller.AvatarUrl;

            decimal fee;
            detail.FreeShipping = (decimal.TryParse(detail.TransportFee, out fee) && fee == 0) ||
                                  ContainsText(item["itemLabelExtList"], "包邮");

            // 成色在标签列表里，形如 { propertyText: "成色", text: "几乎全新" }
            foreach (var label in item.Items("itemLabelExtList", "cpvLabels"))
            {
                var name = label.Str("propertyText", "propertyName");
                if (name == "成色") detail.Condition = label.Str("text", "valueText", "valueName");
            }
            if (ContainsText(item["itemLabelExtList"], "可小刀") || ContainsText(item["itemLabelExtList"], "可议价")) detail.Bargain = true;
            else if (ContainsText(item["itemLabelExtList"], "不议价")) detail.Bargain = false;

            return detail;
        }

        /// <summary>mtop.taobao.idle.web.favor.item.list</summary>
        public static PageResult<ItemSummary> ParseFavorites(JToken data)
        {
            var items = new List<ItemSummary>();
            foreach (var f in data.Items("items"))
            {
                var item = new ItemSummary
                {
                    ItemId = f.Str("id", "longItemId", "itemId"),
                    Title = f.Str("title"),
                    Price = TextFormat.ParsePrice(f.Str("price")),
                    OriginalPrice = TextFormat.ParsePrice(f.Str("originalPrice")),
                    ReducedBy = TextFormat.ParsePrice(f.Str("reducePrice")),
                    CoverUrl = TextFormat.NormalizeUrl(f.Str("picUrl", "imageUrls[0]")),
                    SellerId = f.Str("userId"),
                    SellerNick = f.Str("userNick"),
                    SellerAvatar = TextFormat.NormalizeUrl(f.Str("userAvatar")),
                    City = f.Str("city", "province", "area"),
                    WantCount = f.Int("wantUv"),
                    FreeShipping = f.Bool("freeShip"),
                    // itemStatus 为负数表示已下架、删除等失效状态
                    IsInvalid = f.Long("itemStatus") < 0
                };
                if (IsUsable(item)) items.Add(item);
            }
            return new PageResult<ItemSummary>(items, data.Bool("nextPage"), data.Int("totalCount"));
        }

        /// <summary>
        /// mtop.idle.web.user.page.head。这条接口在公开资料里只有一个来源，字段名不确定，
        /// 所以从 module.base / module.head / module.count / userInfo / base 以及根节点里逐个找。
        ///
        /// 两级兜底：先按常见字段名取；再在节点子树里递归按键名找（Find）。
        /// 实测昵称常被嵌在更深一层（如 module.base.userInfo.nick），固定路径取不到，
        /// 只靠第一级会表现为“头像有、昵称没有”。
        /// </summary>
        public static UserProfile ParseUserProfile(JToken data)
        {
            var baseNode = data.At("module.base") ?? data.At("module.userInfo") ?? data.At("userInfo")
                           ?? data.At("base") ?? data;
            var head = data.At("module.head") ?? data.At("head") ?? baseNode;
            var count = data.At("module.count") ?? data.At("count") ?? new JObject();
            var nodes = new JToken[] { head, count, baseNode, data };

            var nickNames = new[] { "displayName", "nick", "fishNick", "userNick", "nickName", "nickname", "userName", "sellerNick", "accountNick" };
            var avatarNames = new[] { "avatar", "logo", "avatarUrl", "portraitUrl", "headPic", "headImgUrl", "portrait" };

            return new UserProfile
            {
                UserId = StripDomain(baseNode.Str("userId", "id", "sellerId", "memberId")
                                     ?? head.Str("userId", "sellerId")
                                     ?? baseNode.Find("userId", "sellerId", "memberId")),
                Nick = FirstValue(nickNames, baseNode, head, count, data),
                AvatarUrl = TextFormat.NormalizeUrl(FirstValue(avatarNames, baseNode, head, data)),
                City = baseNode.Str("city", "location", "ipLocation", "area", "province")
                       ?? baseNode.Find("city", "location", "ipLocation", "area"),
                Signature = baseNode.Str("signature", "intro", "description", "desc", "bio")
                            ?? baseNode.Find("signature", "intro", "description", "bio"),
                Credit = FindCredit(head) ?? FindCredit(baseNode) ?? FindCredit(data),
                ReplyRatio = head.Str("replyRatio24h", "replyRatio", "replyRate")
                             ?? baseNode.Str("replyRatio24h"),
                LastVisit = head.Str("lastVisitTime", "lastVisit", "activeTime", "loginTime")
                            ?? baseNode.Str("lastVisitTime"),
                Verified = ContainsText(baseNode["verifiedInfo"], "认证") ||
                           ContainsText(head["verifiedInfo"], "认证") ||
                           baseNode.Bool("verified", "realNameVerified", "hasVerified") ||
                           head.Bool("verified", "realNameVerified"),
                SoldCount = FirstInt(nodes, "soldCount", "hasSoldNumInteger", "soldNum", "soldCnt", "soldQuantity"),
                ItemCount = FirstInt(nodes, "itemCount", "onSaleCount", "idleItemCount", "itemNum", "onSaleNum"),
                FollowerCount = FirstInt(nodes, "followerCount", "fansCount", "fansNum", "followerNum"),
                FollowingCount = FirstInt(nodes, "followingCount", "followCount", "followNum"),
                RateCount = FirstInt(nodes, "rateCount", "commentCount", "evaluateCount", "rateNum")
            };
        }

        /// <summary>
        /// mtop.taobao.idlemessage.pc.session.sync v3.0：sessions[].{session, message}。
        /// 这条是 mtop 的 idlemessage 命名空间，和换 IM 令牌同族，可能被风控单独拦掉；
        /// 它只用来补昵称/头像，取不到时不要塞默认昵称，交给上层用别的接口补。
        ///
        /// 单聊里 <c>userInfo</c> 是**自己**，对方在 <c>ownerInfo</c>（实测：userInfo.userId 等于自己的 unb）。
        /// 所以按 selfUserId 排除自己再取对方，否则会把我的昵称当成卖家的。
        /// </summary>
        public static IList<Conversation> ParseSessions(JToken data, string selfUserId)
        {
            var list = new List<Conversation>();
            foreach (var s in data.Items("sessions", "sessionList", "sessionModels", "data.sessionList"))
            {
                var session = s["session"] ?? s;
                var summary = s.At("message.summary") ?? s.At("summary") ?? new JObject();

                // sessionType：1 真人单聊，3 系统通知，6 互动，23 通知。只保留和卖家的单聊。
                var type = session.Int("sessionType");
                if (type != 0 && type != 1) continue;

                var peer = PickPeer(session["ownerInfo"], session["userInfo"], selfUserId);

                var conv = new Conversation
                {
                    Cid = StripDomain(session.Str("sessionId", "cid")),
                    PeerUserId = StripDomain(peer.Str("userId", "sellerId", "peerUserId")),
                    // 拿不到就留空，不要塞默认昵称——否则上层会以为已经有了，不再去补
                    PeerNick = peer.Str("fishNick", "nick", "userNick", "displayName"),
                    PeerAvatar = TextFormat.NormalizeUrl(peer.Str("logo", "avatar", "avatarUrl", "portraitUrl")),
                    ItemId = session.Str("itemInfo.itemId", "extensions.itemId", "itemId"),
                    ItemTitle = session.Str("itemInfo.title", "extensions.itemTitle"),
                    ItemCover = TextFormat.NormalizeUrl(session.Str("itemInfo.mainPic", "itemInfo.picUrl", "extensions.itemMainPic")),
                    LastMessage = summary.Str("summary", "content") ?? string.Empty,
                    UnreadCount = summary.Int("unread"),
                    Time = TextFormat.FromUnix(summary.Long("ts", "time"))
                };
                if (!string.IsNullOrEmpty(conv.Cid)) list.Add(conv);
            }
            return list.OrderByDescending(c => c.Time).ToList();
        }

        /// <summary>从 ownerInfo / userInfo 里挑出对方：先排除自己，再优先 ownerInfo（卖家）。</summary>
        private static JToken PickPeer(JToken owner, JToken user, string selfUserId)
        {
            var ownerId = owner == null ? null : StripDomain(owner.Str("userId"));
            var userId = user == null ? null : StripDomain(user.Str("userId"));

            if (!string.IsNullOrEmpty(selfUserId))
            {
                if (ownerId == selfUserId && userId != selfUserId) return user ?? new JObject();
                if (userId == selfUserId && ownerId != selfUserId) return owner ?? new JObject();
            }
            return owner ?? user ?? new JObject();
        }

        /// <summary>
        /// WebSocket /r/Conversation/listNewestPagination 的会话列表：body.userConvs[]。
        ///
        /// 这条才是网页版左栏用的数据源。session.sync 只能拿到活跃 Top N，会漏掉最新会话，
        /// 所以会话列表以这里为准。缺点是这条响应里没有对方昵称和头像，稍后由 session.sync 补。
        /// </summary>
        public static IList<Conversation> ParseNewestConversations(JToken response, string selfUserId)
        {
            var list = new List<Conversation>();
            foreach (var item in response.Items("body.userConvs"))
            {
                var conv = item.At("singleChatUserConversation") ?? item;
                var single = conv.At("singleChatConversation") ?? item.At("singleChatConversation");
                if (single == null) continue;

                var cid = StripDomain(single.Str("cid"));
                if (string.IsNullOrEmpty(cid)) continue;

                var last = conv.At("lastMessage.message");
                var extension = single.At("extension") ?? new JObject();

                // 时间优先用最后一条消息的时间，没有就用会话的修改时间
                var time = last.Long("createAt");
                if (time <= 0) time = conv.Long("modifyTime");

                list.Add(new Conversation
                {
                    Cid = cid,
                    PeerUserId = PeerOf(single, selfUserId),
                    // 这条响应通常不带对方昵称/头像，但个别版本会塞在 userInfo 里，能取就取
                    PeerNick = item.Str("userInfo.fishNick", "userInfo.nick", "peerNick", "nick")
                               ?? single.Str("userInfo.fishNick", "userInfo.nick", "peerNick", "nick"),
                    PeerAvatar = TextFormat.NormalizeUrl(
                        item.Str("userInfo.logo", "userInfo.avatar", "peerAvatar", "avatar")
                        ?? single.Str("userInfo.logo", "userInfo.avatar")),
                    ItemId = extension.Str("itemId"),
                    ItemTitle = extension.Str("itemTitle"),
                    ItemCover = TextFormat.NormalizeUrl(extension.Str("itemMainPic")),
                    LastMessage = last.Str("content.custom.summary", "content.searchableContent.summary",
                        "extension.reminderContent") ?? string.Empty,
                    UnreadCount = (int)Math.Max(0, last.Long("unreadCount")),
                    Time = TextFormat.FromUnix(time)
                });
            }
            return list;
        }

        /// <summary>从 pairFirst / pairSecond 里挑出不是自己的那一方；0 / 负数 是系统占位，不算用户。</summary>
        private static string PeerOf(JToken single, string selfUserId)
        {
            var first = StripDomain(single.Str("pairFirst"));
            var second = StripDomain(single.Str("pairSecond"));

            string peer = null;
            if (!string.IsNullOrEmpty(selfUserId))
            {
                if (first == selfUserId) peer = second;
                else if (second == selfUserId) peer = first;
            }
            if (peer == null) peer = second ?? first;

            // "0@goofish" / "-1@goofish" 这类是系统会话（闲鱼精选、活动通知），不是真实用户
            return IsSystemPeer(peer) ? null : peer;
        }

        /// <summary>系统占位的对方 ID：空、"0"、负数（实测有 -1）。</summary>
        private static bool IsSystemPeer(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            long n;
            return long.TryParse(id, out n) && n <= 0;
        }

        public static string StripDomain(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var at = id.IndexOf('@');
            return at > 0 ? id.Substring(0, at) : id;
        }

        private static bool IsUsable(ItemSummary item)
        {
            // 没有 ID 或标题的多半是广告位、运营卡片
            return !string.IsNullOrEmpty(item.ItemId) && !string.IsNullOrEmpty(item.Title);
        }

        private static int FindWant(JToken node)
        {
            var direct = node.Int("wantCnt", "wantNum", "wantUv");
            if (direct > 0) return direct;
            foreach (var text in Strings(node))
            {
                var m = WantPattern.Match(text);
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
            return 0;
        }

        /// <summary>
        /// 在若干候选节点里依次按字段名取，取不到再在节点子树里递归按键名找。
        /// 用于字段名不确定、且可能嵌在更深一层的用户资料。
        /// </summary>
        private static string FirstValue(string[] names, params JToken[] nodes)
        {
            foreach (var node in nodes)
            {
                var direct = node.Str(names);
                if (!string.IsNullOrWhiteSpace(direct)) return direct;
            }
            foreach (var node in nodes)
            {
                var deep = node.Find(names);
                if (!string.IsNullOrWhiteSpace(deep)) return deep;
            }
            return null;
        }

        /// <summary>在若干候选节点里按顺序找第一个大于 0 的整数，用于字段名不确定的计数。</summary>
        private static int FirstInt(IEnumerable<JToken> nodes, params string[] paths)
        {
            foreach (var node in nodes)
            {
                var value = node.Int(paths);
                if (value > 0) return value;
            }
            return 0;
        }

        private static string FindCredit(JToken seller)
        {
            foreach (var text in Strings(seller))
            {
                var m = CreditPattern.Match(text.Trim());
                if (m.Success) return m.Groups[1].Value;
            }
            return seller.Str("zhimaLevelInfo.levelName", "creditLevel");
        }

        private static bool ContainsText(JToken node, string needle)
        {
            return node != null && Strings(node).Any(s => s.IndexOf(needle, StringComparison.Ordinal) >= 0);
        }

        private static IEnumerable<string> Strings(JToken node)
        {
            if (node == null) yield break;
            if (node.Type == JTokenType.String)
            {
                yield return (string)node;
                yield break;
            }
            var container = node as JContainer;
            if (container == null) yield break;
            foreach (var value in container.Descendants().OfType<JValue>())
            {
                if (value.Type == JTokenType.String) yield return (string)value.Value;
            }
        }
    }
}
