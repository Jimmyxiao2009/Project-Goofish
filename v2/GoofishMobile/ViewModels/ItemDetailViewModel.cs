using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;

namespace Goofish.ViewModels
{
    /// <summary>详情页大图。没有图片（演示数据）时显示色块 + 图标。</summary>
    public sealed class GallerySlide
    {
        public string Url { get; set; }
        public string Color { get; set; }
        public string Glyph { get; set; }
    }

    public sealed class ItemDetailViewModel : BindableBase
    {
        private ItemDetail _item;
        private ItemSummary _preview;
        private bool _isFavorite;
        private bool _isLoading;
        private string _error;
        private int _slideIndex;
        private List<GallerySlide> _slides = new List<GallerySlide>();

        public ItemDetail Item
        {
            get { return _item; }
            private set
            {
                if (SetProperty(ref _item, value)) OnPropertyChanged(nameof(HasItem));
            }
        }

        /// <summary>从列表点进来时先用列表里已有的标题和价格占位，详情回来后替换。</summary>
        public ItemSummary Preview
        {
            get { return _preview; }
            private set { SetProperty(ref _preview, value); }
        }

        public bool HasItem => Item != null;

        public List<GallerySlide> Slides
        {
            get { return _slides; }
            private set
            {
                if (SetProperty(ref _slides, value)) OnPropertyChanged(nameof(SlideText));
            }
        }

        public int SlideIndex
        {
            get { return _slideIndex; }
            set
            {
                if (SetProperty(ref _slideIndex, value)) OnPropertyChanged(nameof(SlideText));
            }
        }

        public string SlideText => Slides.Count > 1 ? (Math.Max(0, SlideIndex) + 1) + " / " + Slides.Count : null;

        public bool IsFavorite
        {
            get { return _isFavorite; }
            private set { SetProperty(ref _isFavorite, value); }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            private set { SetProperty(ref _isLoading, value); }
        }

        public string Error
        {
            get { return _error; }
            private set { SetProperty(ref _error, value); }
        }

        public async Task LoadAsync(string itemId, ItemSummary preview)
        {
            Preview = preview;
            if (preview != null) Slides = BuildSlides(new List<string> { preview.CoverUrl }, preview);
            IsLoading = true;
            Error = null;
            try
            {
                var detail = await StatusTray.RunAsync("正在加载宝贝...", () => AppServices.Data.GetItemDetailAsync(itemId));
                // 演示数据的占位色块要沿用列表里的
                if (preview != null && detail.Images.Count == 0)
                {
                    detail.PlaceholderColor = preview.PlaceholderColor;
                    detail.PlaceholderGlyph = preview.PlaceholderGlyph;
                }
                Item = detail;
                Slides = BuildSlides(detail.Images, detail);
                SlideIndex = 0;
                IsFavorite = AppServices.Data.IsFavorite(detail.ItemId);
            }
            catch (Exception ex)
            {
                Error = MtopException.Describe(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>返回给用户看的结果说明；null 表示一切正常。</summary>
        public async Task<string> ToggleFavoriteAsync()
        {
            if (Item == null) return null;
            var target = !IsFavorite;
            try
            {
                var synced = await StatusTray.RunAsync(target ? "正在收藏..." : "正在取消收藏...",
                    () => AppServices.Data.SetFavoriteAsync(Item, target));
                IsFavorite = target;
                Item.CollectCount = Math.Max(0, Item.CollectCount + (target ? 1 : -1));
                OnPropertyChanged(nameof(Item));
                return synced ? null : "已保存在本机，但没能同步到闲鱼账号。";
            }
            catch (Exception ex)
            {
                return MtopException.Describe(ex);
            }
        }

        private static List<GallerySlide> BuildSlides(IList<string> urls, ItemSummary source)
        {
            var list = urls.Where(u => !string.IsNullOrEmpty(u))
                .Select(u => new GallerySlide { Url = u, Color = "#00000000", Glyph = string.Empty })
                .ToList();
            if (list.Count == 0)
            {
                list.Add(new GallerySlide { Color = source.PlaceholderColor, Glyph = source.PlaceholderGlyph });
                // 演示数据只有一种色块，补一张“细节图”让翻页可以演示
                if (AppServices.UseDemo) list.Add(new GallerySlide { Color = source.PlaceholderColor, Glyph = "\uE8B9" });
            }
            return list;
        }
    }
}
