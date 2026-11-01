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
    /// 本机收藏。演示模式下它就是收藏夹；登录后它记录“已知收藏过的宝贝”，
    /// 用于详情页显示收藏状态，以及远端收藏接口失败时的兜底。
    /// </summary>
    public sealed class FavoritesStore
    {
        private const string FileName = "favorites.json";
        private readonly Dictionary<string, ItemSummary> _items = new Dictionary<string, ItemSummary>();
        private readonly HashSet<string> _remoteIds = new HashSet<string>();
        private bool _loaded;

        public async Task LoadAsync()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
                if (file == null) return;
                var json = await FileIO.ReadTextAsync(file);
                var list = JsonConvert.DeserializeObject<List<ItemSummary>>(json) ?? new List<ItemSummary>();
                foreach (var item in list.Where(i => !string.IsNullOrEmpty(i.ItemId))) _items[item.ItemId] = item;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("读取本机收藏失败：" + ex.Message);
            }
        }

        public bool Contains(string itemId)
        {
            return itemId != null && (_items.ContainsKey(itemId) || _remoteIds.Contains(itemId));
        }

        public IList<ItemSummary> All => _items.Values.Reverse().ToList();

        /// <summary>同步远端收藏列表里出现过的 ID，详情页据此显示“已收藏”。</summary>
        public void RememberRemote(IEnumerable<string> ids)
        {
            foreach (var id in ids) _remoteIds.Add(id);
        }

        public async Task SetAsync(ItemSummary item, bool favorite)
        {
            if (favorite)
            {
                _items[item.ItemId] = Snapshot(item);
            }
            else
            {
                _items.Remove(item.ItemId);
                _remoteIds.Remove(item.ItemId);
            }
            await SaveAsync();
        }

        private async Task SaveAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(_items.Values.ToList()));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存本机收藏失败：" + ex.Message);
            }
        }

        // 只存列表需要的字段，详情页的大段描述与图片列表不落盘
        private static ItemSummary Snapshot(ItemSummary item)
        {
            return new ItemSummary
            {
                ItemId = item.ItemId,
                Title = item.Title,
                Price = item.Price,
                OriginalPrice = item.OriginalPrice,
                CoverUrl = item.CoverUrl,
                City = item.City,
                SellerId = item.SellerId,
                SellerNick = item.SellerNick,
                SellerAvatar = item.SellerAvatar,
                WantCount = item.WantCount,
                FreeShipping = item.FreeShipping,
                Condition = item.Condition,
                PlaceholderColor = item.PlaceholderColor,
                PlaceholderGlyph = item.PlaceholderGlyph
            };
        }
    }
}
