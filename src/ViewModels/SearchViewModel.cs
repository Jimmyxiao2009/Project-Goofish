using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ProjectGoofish.Models;
using ProjectGoofish.Services;

namespace ProjectGoofish.ViewModels
{
    /// <summary>
    /// 搜索与筛选视图模型
    /// </summary>
    public class SearchViewModel : ViewModelBase
    {
        private string _keyword;
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public ObservableCollection<ItemDetail> SearchResults { get; } = new ObservableCollection<ItemDetail>();

        public async Task ExecuteSearchAsync(string sortType = "default")
        {
            if (string.IsNullOrWhiteSpace(Keyword)) return;

            IsSearching = true;
            try
            {
                var results = await GoofishApiService.Instance.SearchAsync(Keyword, 1, sortType);
                SearchResults.Clear();
                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }
            }
            finally
            {
                IsSearching = false;
            }
        }
    }
}
