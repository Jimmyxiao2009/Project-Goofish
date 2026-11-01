using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace Goofish.Services
{
    /// <summary>
    /// 闲鱼私信：钉钉 IM 的 LWP 协议（JSON over WebSocket）。
    ///
    /// 流程：mtop 换 accessToken → 连 wss://wss-goofish.dingtalk.com/ → 发 /reg → 等服务端推 /s/vulcan
    /// （在它之前发出的 /r/ 请求会被以 400 拒绝）→ 之后每 15 秒发 /! 心跳。
    /// 每个下行帧都要回 code=200 的 ack，否则服务端会重复推送。
    /// </summary>
    public sealed class GoofishImClient : IChatService
    {
        public const string ImAppKey = "444e9908a51d1cb236a27862abc769c9";
        private const string LoginTokenApi = "mtop.taobao.idlemessage.pc.login.token";
        private const string SessionSyncApi = "mtop.taobao.idlemessage.pc.session.sync";
        private const string Domain = "@goofish";
        private static readonly Uri ServerUri = new Uri("wss://wss-goofish.dingtalk.com/");

        private const string ImUserAgent = MtopClient.UserAgent +
            " DingTalk(2.1.5) OS(Windows/10) Browser(Chrome/140.0.0.0) DingWeb/2.1.5 IMPaaS DingWeb/2.1.5";

        private readonly MtopClient _mtop;
        private readonly AccountService _account;
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending =
            new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();
        private readonly Random _random = new Random();

        private MessageWebSocket _socket;
        private DataWriter _writer;
        private TaskCompletionSource<bool> _ready;
        private ThreadPoolTimer _heartbeat;
        private string _regMid;

        public GoofishImClient(MtopClient mtop, AccountService account)
        {
            _mtop = mtop;
            _account = account;
        }

        public event EventHandler<ChatMessage> MessageReceived;

        public string SelfUserId => _account.UserId;

        public bool IsConnected => _socket != null && _ready != null && _ready.Task.IsCompleted && _ready.Task.Result;

        /// <summary>
        /// 会话列表。
        ///
        /// 以 WebSocket 的 /r/Conversation/listNewestPagination 为准——网页版左栏就是用它，
        /// 能拿到最新的会话。mtop 的 session.sync 只能拿到活跃 Top N（goofish-cli 明确标注为阉割版），
        /// 单独用会漏掉最新会话，所以只拿它来补这条响应里没有的昵称、头像等字段，并作为兜底。
        /// </summary>
        public async Task<IList<Conversation>> GetConversationsAsync()
        {
            await EnsureConnectedAsync();

            var newest = new List<Conversation>();
            try
            {
                // body: [起始时间戳（从最新开始取 = JS 的 MAX_SAFE_INTEGER）, 条数]
                var response = await RequestAsync("/r/Conversation/listNewestPagination",
                    new JArray(9007199254740991L, 50));
                newest = GoofishParser.ParseNewestConversations(response, SelfUserId).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("拉取最新会话失败，退回 session.sync：" + ex.Message);
            }

            var sync = new Dictionary<string, Conversation>();
            try
            {
                var data = await _mtop.CallAsync(SessionSyncApi, "3.0", new { fetchNum = 50 });
                foreach (var c in GoofishParser.ParseSessions(data, SelfUserId)) sync[c.Cid] = c;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("session.sync 失败：" + ex.Message);
            }

            // 最新分页里没有昵称/头像，用 session.sync 的同一条会话补上
            var known = new HashSet<string>();
            foreach (var conv in newest)
            {
                known.Add(conv.Cid);
                Conversation s;
                if (!sync.TryGetValue(conv.Cid, out s)) continue;
                if (string.IsNullOrEmpty(conv.PeerNick)) conv.PeerNick = s.PeerNick;
                if (string.IsNullOrEmpty(conv.PeerAvatar)) conv.PeerAvatar = s.PeerAvatar;
                if (string.IsNullOrEmpty(conv.PeerUserId)) conv.PeerUserId = s.PeerUserId;
                if (string.IsNullOrEmpty(conv.ItemTitle)) conv.ItemTitle = s.ItemTitle;
                if (string.IsNullOrEmpty(conv.ItemCover)) conv.ItemCover = s.ItemCover;
                if (conv.ItemPrice == 0) conv.ItemPrice = s.ItemPrice;
                if (string.IsNullOrEmpty(conv.LastMessage)) conv.LastMessage = s.LastMessage;
                if (conv.UnreadCount == 0) conv.UnreadCount = s.UnreadCount;
                if (conv.Time == DateTimeOffset.MinValue) conv.Time = s.Time;
            }

            // session.sync 里有、最新分页没返回的会话也保留，避免反而丢东西
            foreach (var pair in sync)
            {
                if (!known.Contains(pair.Key)) newest.Add(pair.Value);
            }

            // 昵称/头像由上层补齐（见 MainViewModel）：这里不塞默认值，留空表示“还不知道”
            return newest.OrderByDescending(c => c.Time).ToList();
        }

        public async Task<IList<ChatMessage>> GetMessagesAsync(string cid)
        {
            await EnsureConnectedAsync();
            // body: [cid, 是否正序, 游标（从最新开始取 = JS 的 MAX_SAFE_INTEGER）, 条数, 未知开关]
            var response = await RequestAsync("/r/MessageManager/listUserMessages",
                new JArray(cid + Domain, false, 9007199254740991L, 30, false));

            var self = SelfUserId;
            var list = new List<ChatMessage>();
            foreach (var model in response.Items("body.userMessageModels"))
            {
                var message = model["message"];
                if (message == null) continue;
                var sender = GoofishParser.StripDomain(message.Str("extension.senderUserId", "sender.uid"));
                var item = new ChatMessage
                {
                    MessageId = message.Str("messageId"),
                    Cid = cid,
                    SenderId = sender,
                    SenderName = message.Str("extension.reminderTitle"),
                    IsMine = sender != null && sender == self,
                    Time = TextFormat.FromUnix(message.Long("createAt"))
                };
                ApplyContent(item, message["content"], message.Str("extension.reminderContent") ?? "[暂不支持的消息]");
                list.Add(item);
            }
            // 服务端按时间倒序返回
            return list.OrderBy(m => m.Time).ToList();
        }

        public async Task<string> OpenConversationAsync(string peerUserId, string itemId)
        {
            await EnsureConnectedAsync();
            var body = new JArray(new JObject
            {
                ["pairFirst"] = peerUserId + Domain,
                ["pairSecond"] = SelfUserId + Domain,
                ["bizType"] = "1",
                ["extension"] = new JObject { ["itemId"] = itemId },
                ["ctx"] = new JObject { ["appVersion"] = "1.0", ["platform"] = "web" }
            });
            var response = await RequestAsync("/r/SingleChatConversation/create", body);
            var cid = response.Str("body.singleChatConversation.cid", "body.cid");
            if (string.IsNullOrEmpty(cid)) throw new MtopException(MtopErrorKind.Api, null, "无法创建与卖家的会话。");
            return GoofishParser.StripDomain(cid);
        }

        public async Task SendTextAsync(string cid, string peerUserId, ChatMessage message)
        {
            await EnsureConnectedAsync();
            var payload = JsonConvert.SerializeObject(new { contentType = 1, text = new { text = message.Text } });
            var body = new JArray(
                new JObject
                {
                    ["uuid"] = "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "1",
                    ["cid"] = cid + Domain,
                    ["conversationType"] = 1,
                    ["content"] = new JObject
                    {
                        ["contentType"] = 101,
                        ["custom"] = new JObject
                        {
                            ["type"] = 1,
                            ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
                        }
                    },
                    ["redPointPolicy"] = 0,
                    ["extension"] = new JObject { ["extJson"] = "{}" },
                    ["ctx"] = new JObject { ["appVersion"] = "1.0", ["platform"] = "web" },
                    ["mtags"] = new JObject(),
                    ["msgReadStatusSetting"] = 1
                },
                new JObject
                {
                    ["actualReceivers"] = new JArray(peerUserId + Domain, SelfUserId + Domain)
                });

            var response = await RequestAsync("/r/MessageSend/sendByReceiverScope", body);
            message.MessageId = response.Str("body.messageId") ?? message.MessageId;
        }

        /// <summary>登录后在后台保持连接，这样不在私信页时也能收到新消息横幅。</summary>
        public async Task<bool> TryConnectAsync()
        {
            if (!_account.IsSignedIn) return false;
            try
            {
                await EnsureConnectedAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IM 后台连接失败：" + ex.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            _heartbeat?.Cancel();
            _heartbeat = null;
            var socket = _socket;
            _socket = null;
            _writer = null;
            _ready = null;
            foreach (var pending in _pending.Values) pending.TrySetCanceled();
            _pending.Clear();
            if (socket != null)
            {
                try { socket.Close(1000, "bye"); } catch (Exception) { }
                socket.Dispose();
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (IsConnected) return;
            if (!_account.IsSignedIn) throw new MtopException(MtopErrorKind.Auth, null, "登录后才能和卖家聊天。");

            await _connectLock.WaitAsync();
            try
            {
                if (IsConnected) return;
                Disconnect();

                var tokenData = await _mtop.CallAsync(LoginTokenApi, "1.0", new { appKey = ImAppKey, deviceId = _account.DeviceId });
                var accessToken = tokenData.Str("accessToken");
                if (string.IsNullOrEmpty(accessToken))
                    throw new MtopException(MtopErrorKind.Auth, null, "无法获取消息服务凭证，请重新登录。");

                var socket = new MessageWebSocket();
                socket.Control.MessageType = SocketMessageType.Utf8;
                TrySetHeader(socket, "Origin", "https://www.goofish.com");
                TrySetHeader(socket, "User-Agent", MtopClient.UserAgent);
                TrySetHeader(socket, "Cookie", _mtop.GetCookieHeader(MtopClient.SiteUri));
                socket.MessageReceived += OnMessageReceived;
                socket.Closed += OnClosed;

                try
                {
                    await socket.ConnectAsync(ServerUri);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    throw new MtopException(MtopErrorKind.Network, null, "无法连接闲鱼消息服务。", ex);
                }

                _socket = socket;
                _writer = new DataWriter(socket.OutputStream);
                _ready = new TaskCompletionSource<bool>();

                _regMid = NewMid();
                await SendFrameAsync(new JObject
                {
                    ["lwp"] = "/reg",
                    ["headers"] = new JObject
                    {
                        ["cache-header"] = "app-key token ua wv",
                        ["app-key"] = ImAppKey,
                        ["token"] = accessToken,
                        ["ua"] = ImUserAgent,
                        ["dt"] = "j",
                        ["wv"] = "im:3,au:3,sy:6",
                        ["sync"] = "0,0;0;0;",
                        ["did"] = _account.DeviceId,
                        ["mid"] = _regMid
                    }
                });

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await SendFrameAsync(new JObject
                {
                    ["lwp"] = "/r/SyncStatus/ackDiff",
                    ["headers"] = new JObject { ["mid"] = NewMid() },
                    ["body"] = new JArray(new JObject
                    {
                        ["pipeline"] = "sync",
                        ["tooLong2Tag"] = "PNM,1",
                        ["channel"] = "sync",
                        ["topic"] = "sync",
                        ["highPts"] = 0,
                        ["pts"] = now * 1000,
                        ["seq"] = 0,
                        ["timestamp"] = now
                    })
                });

                var ready = _ready.Task;
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(15))) != ready || !ready.Result)
                {
                    Disconnect();
                    throw new MtopException(MtopErrorKind.Network, null, "消息服务没有响应，请稍后再试。");
                }

                _heartbeat = ThreadPoolTimer.CreatePeriodicTimer(async _ =>
                {
                    try
                    {
                        await SendFrameAsync(new JObject { ["lwp"] = "/!", ["headers"] = new JObject { ["mid"] = NewMid() } });
                    }
                    catch (Exception)
                    {
                        Disconnect();
                    }
                }, TimeSpan.FromSeconds(15));
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task<JObject> RequestAsync(string lwp, JToken body)
        {
            var mid = NewMid();
            var tcs = new TaskCompletionSource<JObject>();
            _pending[mid] = tcs;
            try
            {
                await SendFrameAsync(new JObject { ["lwp"] = lwp, ["headers"] = new JObject { ["mid"] = mid }, ["body"] = body });
                var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(12)));
                if (done != tcs.Task) throw new MtopException(MtopErrorKind.Network, null, "消息服务响应超时。");
                var response = await tcs.Task;
                var code = response.Int("code");
                if (code != 200)
                {
                    var reason = response.Str("body.reason", "body.message") ?? ("code " + code);
                    if (code == 401) throw new MtopException(MtopErrorKind.Auth, reason, "消息服务登录已失效，请重新登录。");
                    throw new MtopException(MtopErrorKind.Api, reason, "消息服务拒绝了请求：" + reason);
                }
                return response;
            }
            finally
            {
                TaskCompletionSource<JObject> removed;
                _pending.TryRemove(mid, out removed);
            }
        }

        private async Task SendFrameAsync(JObject frame)
        {
            var writer = _writer;
            if (writer == null) throw new MtopException(MtopErrorKind.Network, null, "消息服务已断开。");
            await _sendLock.WaitAsync();
            try
            {
                writer.WriteString(frame.ToString(Formatting.None));
                await writer.StoreAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            JObject frame;
            try
            {
                using (var reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    frame = JObject.Parse(reader.ReadString(reader.UnconsumedBufferLength));
                }
            }
            catch (Exception ex)
            {
                // 连接被关闭时 GetDataReader 会抛异常，这里只记录
                System.Diagnostics.Debug.WriteLine("IM 收包失败：" + ex.Message);
                return;
            }

            var mid = frame.Str("headers.mid");
            var lwp = frame.Str("lwp");

            // 对请求的响应：mid 与请求相同（服务端推送用的是它自己生成的 mid，不会撞上）
            TaskCompletionSource<JObject> pending;
            if (mid != null && _pending.TryGetValue(mid, out pending))
            {
                pending.TrySetResult(frame);
                return;
            }

            if (mid != null && mid == _regMid)
            {
                if (frame.Int("code") != 200) _ready?.TrySetResult(false);
                return;
            }

            if (lwp == null) return;

            try
            {
                await SendFrameAsync(BuildAck(frame));
            }
            catch (Exception)
            {
                return;
            }

            if (lwp == "/s/vulcan") _ready?.TrySetResult(true);

            foreach (var push in frame.Items("body.syncPushPackage.data"))
            {
                var decoded = DecodePush(push.Str("data"));
                var message = decoded == null ? null : ExtractIncoming(decoded);
                if (message != null && !message.IsMine) MessageReceived?.Invoke(this, message);
            }
        }

        private void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            if (ReferenceEquals(sender, _socket)) Disconnect();
        }

        private static JObject BuildAck(JObject frame)
        {
            var headers = frame["headers"] as JObject ?? new JObject();
            var ack = new JObject
            {
                ["code"] = 200,
                ["headers"] = new JObject
                {
                    ["mid"] = headers.Str("mid") ?? string.Empty,
                    ["sid"] = headers.Str("sid") ?? string.Empty
                }
            };
            foreach (var key in new[] { "app-key", "ua", "dt" })
            {
                if (headers[key] != null) ack["headers"][key] = headers[key];
            }
            return ack;
        }

        /// <summary>推送数据三种编码依次尝试：明文 JSON、base64(JSON)、base64(MessagePack)。</summary>
        private static JToken DecodePush(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return null;
            var trimmed = data.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try { return JToken.Parse(trimmed); } catch (JsonException) { }
            }

            byte[] bytes;
            try
            {
                var clean = new string(trimmed.Where(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=').ToArray());
                while (clean.Length % 4 != 0) clean += "=";
                bytes = Convert.FromBase64String(clean);
            }
            catch (FormatException)
            {
                return null;
            }

            try
            {
                var text = new UTF8Encoding(false, true).GetString(bytes, 0, bytes.Length);
                if (text.TrimStart().StartsWith("{")) return JToken.Parse(text);
            }
            catch (Exception)
            {
                // 不是 UTF-8 JSON，按 MessagePack 处理
            }

            try
            {
                return MessagePackReader.Decode(bytes);
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine("IM 推送解码失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 两种推送格式：
        /// 新版 { sessionId, operation: { content, senderInfo } }；
        /// 旧版（MessagePack）{ "1": { "2": cid, "5": 时间, "10": { reminderContent, reminderTitle, senderUserId } } }。
        /// 只有正文的会被转成消息，已读回执、正在输入等元事件忽略。
        /// </summary>
        private ChatMessage ExtractIncoming(JToken decoded)
        {
            var op = decoded["operation"] as JObject;
            if (op != null)
            {
                var content = op["content"];
                // 8 是会话激活心跳，不是真正的消息
                if (content.Int("contentType") == 8) return null;
                var sender = GoofishParser.StripDomain(op.Str("senderInfo.senderUserId") ?? content.Str("reminder.senderUserId"));
                var incoming = new ChatMessage
                {
                    MessageId = op.Str("messageId"),
                    Cid = GoofishParser.StripDomain(decoded.Str("sessionId") ?? op.Str("sessionInfo.sessionId")),
                    SenderId = sender,
                    SenderName = content.Str("reminder.reminderTitle"),
                    IsMine = sender == SelfUserId,
                    Time = DateTimeOffset.Now
                };
                ApplyContent(incoming, content, content.Str("reminder.reminderContent"));
                // 既没有可显示的正文，也没有图片/视频，就当它不是消息
                if (string.IsNullOrEmpty(incoming.Text) && !incoming.HasMedia) return null;
                return incoming;
            }

            var one = decoded["1"] as JObject;
            var node = one?["10"] as JObject;
            if (node == null) return null;
            var body = node.Str("reminderContent");
            if (string.IsNullOrEmpty(body)) return null;
            var from = node.Str("senderUserId");
            var time = TextFormat.FromUnix(one.Long("5"));
            return new ChatMessage
            {
                MessageId = one.Str("3"),
                Cid = GoofishParser.StripDomain(one.Str("2")),
                SenderId = from,
                SenderName = node.Str("reminderTitle"),
                Text = body,
                IsMine = from == SelfUserId,
                IsSystem = body.StartsWith("[") && body.EndsWith("]"),
                Time = time == DateTimeOffset.MinValue ? DateTimeOffset.Now : time
            };
        }

        /// <summary>把解析出的内容写进消息，并给媒体/卡片消息补一个占位文本（用于无障碍与降级显示）。</summary>
        private static void ApplyContent(ChatMessage message, JToken content, string fallback)
        {
            var parsed = ParseContent(content, fallback);
            if (parsed == null)
            {
                message.Text = fallback ?? string.Empty;
                return;
            }
            message.Kind = parsed.Kind;
            message.MediaUrl = parsed.Url;
            message.MediaCover = parsed.Cover;
            message.MediaWidth = parsed.Width;
            message.MediaHeight = parsed.Height;
            message.CardTitle = parsed.Title;
            message.CardDesc = parsed.Desc;
            message.CardPrice = parsed.Price;
            message.CardUrl = parsed.CardUrl;
            message.CardItemId = parsed.ItemId;

            if (parsed.Kind == MessageKind.Card)
            {
                // 卡片在气泡里显示标题/描述，Text 只作为无障碍文本与降级
                message.Text = !string.IsNullOrEmpty(parsed.Title) ? parsed.Title
                    : !string.IsNullOrEmpty(parsed.Desc) ? parsed.Desc
                    : (fallback ?? "[卡片]");
            }
            else if (parsed.Kind == MessageKind.Text)
            {
                message.Text = parsed.Text ?? fallback ?? string.Empty;
            }
            else
            {
                message.Text = LabelFor(parsed.Kind);
            }
        }

        /// <summary>
        /// 解析一条消息的内容。
        ///
        /// 内容类型与字段结构取自闲鱼 Web IM 自身的枚举（打包在页面 JS 里）：
        /// 1 文本 text.text、2 图片 image.pics[].url/width/height、3 语音、4 视频 video.url/width/height；
        /// 5 起是卡片类（5 位置、6 卡片、7 链接、8 @、9 文件、10 引用、11 合并转发）。
        /// 内容可能直接是载荷，也可能裹在发送信封的 custom.data（base64 的 JSON）里。
        ///
        /// 卡片类的载荷没有公开文档、字段名还随版本漂移，所以走 ParseCard 的宽松取值：
        /// 先从常见固定路径取，取不到再递归按键名找，尽量还原成一张卡片，而不是只显示占位符。
        /// </summary>
        private static ParsedContent ParseContent(JToken content, string fallback)
        {
            if (content == null) return null;
            var payload = DecodePayload(content) ?? content;
            var kind = payload.Int("contentType");
            var text = payload.Str("text.text", "text");

            if (kind == 1 && !string.IsNullOrEmpty(text)) return ParsedContent.FromText(text);

            if (kind == 2)
            {
                var url = payload.Str("image.pics[0].url", "image.url", "picUrl");
                if (!string.IsNullOrEmpty(url))
                {
                    return new ParsedContent
                    {
                        Kind = MessageKind.Image,
                        Url = url,
                        Width = payload.Num("image.pics[0].width", "image.width"),
                        Height = payload.Num("image.pics[0].height", "image.height")
                    };
                }
            }

            if (kind == 3) return ParsedContent.FromText("[语音]");

            if (kind == 4)
            {
                var url = payload.Str("video.url", "video.videoUrl", "videoUrl");
                if (!string.IsNullOrEmpty(url))
                {
                    return new ParsedContent
                    {
                        Kind = MessageKind.Video,
                        Url = url,
                        Cover = payload.Str("video.cover", "video.coverUrl", "video.picUrl", "video.thumbUrl"),
                        Width = payload.Num("video.width"),
                        Height = payload.Num("video.height")
                    };
                }
            }

            if (kind >= 5)
            {
                var card = ParseCard(payload);
                if (card != null) return card;
            }

            // 其余类型与兜底：能取到文本就显示文本，否则给一个类型占位
            if (!string.IsNullOrEmpty(text)) return ParsedContent.FromText(text);
            var summary = payload.Str("tip.tips", "title", "reminderContent") ?? fallback;
            if (!string.IsNullOrEmpty(summary)) return ParsedContent.FromText(summary);
            if (kind > 0) return ParsedContent.FromText(PlaceholderFor(kind));
            return null;
        }

        /// <summary>
        /// 把卡片类载荷还原成一张卡片。字段名不确定，所以固定路径与递归按键名各试一遍；
        /// 一个字段都取不到就返回 null，由调用方退回文本/占位。
        /// </summary>
        private static ParsedContent ParseCard(JToken payload)
        {
            var title = payload.Str("title", "card.title", "bizCard.title", "content.title", "itemTitle")
                        ?? payload.Find("title", "cardTitle", "mainTitle", "itemTitle", "name");
            var desc = payload.Str("desc", "description", "summary", "card.desc", "content.summary")
                       ?? payload.Find("desc", "description", "summary", "subTitle", "content", "tips", "tip", "text");
            var image = payload.Str("picUrl", "imageUrl", "cover", "imgUrl", "card.picUrl")
                        ?? payload.Find("picUrl", "imageUrl", "mainPic", "pic", "cover", "imgUrl", "image", "thumb");
            var url = payload.Str("url", "linkUrl", "jumpUrl", "targetUrl", "itemUrl", "detailUrl", "card.url")
                      ?? payload.Find("url", "linkUrl", "jumpUrl", "targetUrl", "itemUrl", "detailUrl", "link");
            var price = payload.Str("price", "soldPrice", "priceText", "card.price")
                        ?? payload.Find("price", "soldPrice", "priceText", "displayPrice");

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(desc) && string.IsNullOrEmpty(image) && string.IsNullOrEmpty(url))
                return null;

            // 宝贝 ID 只认明确的字段，或从链接里抠；避免把消息 ID 之类的数字当成宝贝
            var itemId = payload.Str("itemId", "item.itemId", "card.itemId", "bizCard.itemId")
                         ?? payload.Find("itemId", "longItemId");
            if (!LooksLikeItemId(itemId)) itemId = ItemIdFromUrl(url);

            return new ParsedContent
            {
                Kind = MessageKind.Card,
                Url = image == null ? null : TextFormat.NormalizeUrl(image),
                Title = title,
                Desc = desc,
                Price = price,
                CardUrl = url,
                ItemId = LooksLikeItemId(itemId) ? itemId : null
            };
        }

        private static bool LooksLikeItemId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 8 || value.Length > 20) return false;
            foreach (var c in value) if (!char.IsDigit(c)) return false;
            return true;
        }

        private static string ItemIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=(\d{8,20})");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>custom.data 是 base64 的 JSON；解不出来返回 null，由调用方退回外层字段。</summary>
        private static JToken DecodePayload(JToken content)
        {
            var data = content.Str("custom.data");
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                var bytes = Convert.FromBase64String(data);
                return JToken.Parse(Encoding.UTF8.GetString(bytes, 0, bytes.Length));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string LabelFor(MessageKind kind)
        {
            switch (kind)
            {
                case MessageKind.Image: return "[图片]";
                case MessageKind.Video: return "[视频]";
                case MessageKind.Audio: return "[语音]";
                case MessageKind.Card: return "[卡片]";
                default: return string.Empty;
            }
        }

        private static string PlaceholderFor(int contentType)
        {
            switch (contentType)
            {
                case 5: return "[位置]";
                case 6: return "[卡片消息]";
                case 7: return "[链接]";
                case 8: return "[@消息]";
                case 9: return "[文件]";
                case 10: return "[引用消息]";
                case 11: return "[合并转发]";
                default: return "[暂不支持的消息]";
            }
        }

        /// <summary>解析结果。宽高为 0 表示服务端没给尺寸；卡片字段取不到的为 null。</summary>
        private sealed class ParsedContent
        {
            public MessageKind Kind = MessageKind.Text;
            public string Text;
            public string Url;
            public string Cover;
            public double Width;
            public double Height;

            // 卡片专用
            public string Title;
            public string Desc;
            public string Price;
            public string CardUrl;
            public string ItemId;

            public static ParsedContent FromText(string value)
            {
                return new ParsedContent { Kind = MessageKind.Text, Text = value };
            }
        }

        private string NewMid()
        {
            int r;
            lock (_random) r = _random.Next(1000);
            return r + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + " 0";
        }

        private static void TrySetHeader(MessageWebSocket socket, string name, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                socket.SetRequestHeader(name, value);
            }
            catch (Exception ex)
            {
                // 部分系统版本不允许设置 Origin/Cookie 等受限头，由系统 Cookie 存储兜底
                System.Diagnostics.Debug.WriteLine("无法设置 WebSocket 请求头 " + name + "：" + ex.Message);
            }
        }
    }
}
