using System.Threading.Tasks;
using ProjectGoofish.Models;
using ProjectGoofish.Services;

namespace ProjectGoofish.ViewModels
{
    /// <summary>
    /// 商品详情页视图模型
    /// </summary>
    public class ItemDetailViewModel : ViewModelBase
    {
        private ItemDetail _item;
        public ItemDetail Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public async Task LoadItemAsync(string itemId)
        {
            IsLoading = true;
            try
            {
                Item = await GoofishApiService.Instance.GetItemDetailAsync(itemId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ToggleFavoriteAsync()
        {
            if (Item == null) return;
            var isFav = await GoofishApiService.Instance.ToggleFavoriteAsync(Item.ItemId);
            Item.IsFavorite = isFav;
            OnPropertyChanged(nameof(Item));
        }
    }
}
