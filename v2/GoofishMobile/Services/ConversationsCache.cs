using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Models;
using Newtonsoft.Json;
using Windows.Storage;

namespace Goofish.Services
{
    /// <summary>
    /// 会话列表的本地缓存。
    ///
    /// 打开应用或切回消息页时先用它把列表显示出来，再在后台用网络刷新，
    /// 不用每次都空等一次“正在同步消息...”（对齐闲鱼 App 的做法）。
    /// 缓存里连对方的昵称/头像一起存，刷新时用来补齐，界面不会闪回“闲鱼用户”。
    ///
    /// 缓存按账号区分：换账号后旧缓存直接作废，避免显示上一个账号的会话。
    /// </summary>
    public static class ConversationsCache
    {
        private const string FileName = "conversations.json";

        private sealed class Row
        {
            public string Cid { get; set; }
            public string PeerUserId { get; set; }
            public string PeerNick { get; set; }
            public string PeerAvatar { get; set; }
            public string PeerColor { get; set; }
            public string ItemId { get; set; }
            public string ItemTitle { get; set; }
            public string ItemCover { get; set; }
            public decimal ItemPrice { get; set; }
            public string LastMessage { get; set; }
            public int UnreadCount { get; set; }
            public long Time { get; set; }
        }

        private sealed class Envelope
        {
            public string UserId { get; set; }
            public List<Row> Rows { get; set; }
        }

        /// <summary>读取缓存；没有缓存、解析失败或账号不匹配时返回空列表。</summary>
        public static async Task<IList<Conversation>> LoadAsync(string userId)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(FileName);
                var text = await FileIO.ReadTextAsync(file);
                var envelope = JsonConvert.DeserializeObject<Envelope>(text);
                if (envelope == null || envelope.Rows == null) return new List<Conversation>();
                if (!string.Equals(envelope.UserId, userId, StringComparison.Ordinal)) return new List<Conversation>();

                return envelope.Rows.Select(r => new Conversation
                {
                    Cid = r.Cid,
                    PeerUserId = r.PeerUserId,
                    PeerNick = r.PeerNick,
                    PeerAvatar = r.PeerAvatar,
                    PeerColor = string.IsNullOrEmpty(r.PeerColor) ? "#FF767676" : r.PeerColor,
                    ItemId = r.ItemId,
                    ItemTitle = r.ItemTitle,
                    ItemCover = r.ItemCover,
                    ItemPrice = r.ItemPrice,
                    LastMessage = r.LastMessage ?? string.Empty,
                    UnreadCount = r.UnreadCount,
                    Time = r.Time > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(r.Time) : DateTimeOffset.MinValue
                }).ToList();
            }
            catch (Exception)
            {
                // 文件不存在（首次运行）或内容损坏都当成没有缓存
                return new List<Conversation>();
            }
        }

        public static async Task SaveAsync(string userId, IEnumerable<Conversation> conversations)
        {
            try
            {
                var envelope = new Envelope
                {
                    UserId = userId,
                    Rows = conversations.Select(c => new Row
                    {
                        Cid = c.Cid,
                        PeerUserId = c.PeerUserId,
                        PeerNick = c.PeerNick,
                        PeerAvatar = c.PeerAvatar,
                        PeerColor = c.PeerColor,
                        ItemId = c.ItemId,
                        ItemTitle = c.ItemTitle,
                        ItemCover = c.ItemCover,
                        ItemPrice = c.ItemPrice,
                        LastMessage = c.LastMessage,
                        UnreadCount = c.UnreadCount,
                        Time = c.Time == DateTimeOffset.MinValue ? 0 : c.Time.ToUnixTimeMilliseconds()
                    }).ToList()
                };
                var file = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(envelope));
            }
            catch (Exception)
            {
                // 写缓存失败不影响使用
            }
        }
    }
}
