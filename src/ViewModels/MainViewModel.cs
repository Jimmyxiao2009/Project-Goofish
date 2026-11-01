using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ProjectGoofish.Models;
using ProjectGoofish.Services;

namespace ProjectGoofish.ViewModels
{
    /// <summary>
    /// 首页推荐与分类 Feed 视图模型
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _selectedCategory = "all";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    var task = LoadFeedAsync();
                }
            }
        }

        public ObservableCollection<ItemDetail> Items { get; } = new ObservableCollection<ItemDetail>();

        public async Task LoadFeedAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var list = await GoofishApiService.Instance.GetHomeFeedAsync(1, 20, SelectedCategory);
                Items.Clear();
                foreach (var item in list)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
