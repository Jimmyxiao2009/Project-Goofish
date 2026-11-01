using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using ProjectGoofish.Models;

namespace ProjectGoofish.Services
{
    /// <summary>
    /// 基于 Windows 10 Mobile 原生 MessageWebSocket 的闲鱼/钉钉私聊通信客户端
    /// </summary>
    public class GoofishImWebSocket : IDisposable
    {
        private static GoofishImWebSocket _instance;
        public static GoofishImWebSocket Instance => _instance ?? (_instance = new GoofishImWebSocket());

        private MessageWebSocket _webSocket;
        private DataWriter _writer;
        private const string ImServerUrl = "wss://wss-goofish.dingtalk.com/";

        public bool IsConnected { get; private set; }

        public event Action<ChatMessage> OnMessageReceived;
        public event Action<bool> OnConnectionStateChanged;

        private GoofishImWebSocket() { }

        /// <summary>
        /// 建立 WebSocket 长连接
        /// </summary>
        public async Task ConnectAsync(string imToken, string deviceId)
        {
            if (IsConnected) return;

            try
            {
                _webSocket = new MessageWebSocket();
                _webSocket.Control.MessageType = SocketMessageType.Utf8;
                _webSocket.MessageReceived += WebSocket_MessageReceived;
                _webSocket.Closed += WebSocket_Closed;

                await _webSocket.ConnectAsync(new Uri(ImServerUrl));
                _writer = new DataWriter(_webSocket.OutputStream);
                IsConnected = true;
                OnConnectionStateChanged?.Invoke(true);

                // 发送 /reg 注册协议包
                await RegisterSessionAsync(imToken, deviceId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IM WebSocket Connect Error: {ex.Message}");
                IsConnected = false;
                OnConnectionStateChanged?.Invoke(false);
            }
        }

        private async Task RegisterSessionAsync(string imToken, string deviceId)
        {
            var packet = new ImRegisterPacket
            {
                Uri = "/reg",
                Body = new ImRegisterBody
                {
                    Token = imToken,
                    DeviceId = deviceId,
                    AppKey = "12574478"
                }
            };

            await SendPacketAsync(packet);
        }

        public async Task SendPacketAsync(object packet)
        {
            if (!IsConnected || _writer == null) return;

            try
            {
                var json = JsonConvert.SerializeObject(packet);
                _writer.WriteString(json);
                await _writer.StoreAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send Packet Error: {ex.Message}");
            }
        }

        private void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    var message = reader.ReadString(reader.UnconsumedBufferLength);
                    System.Diagnostics.Debug.WriteLine($"IM WS Received: {message}");

                    // 解析推送包并派发
                    if (message.Contains("/s/sync"))
                    {
                        // 触发新消息事件通知
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parse Message Error: {ex.Message}");
            }
        }

        private void WebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            IsConnected = false;
            OnConnectionStateChanged?.Invoke(false);
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _webSocket?.Dispose();
        }
    }
}
