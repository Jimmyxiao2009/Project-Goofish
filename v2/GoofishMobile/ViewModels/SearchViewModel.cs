using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;

namespace Goofish.ViewModels
{
    public sealed class SearchViewModel : BindableBase
    {
        private static readonly string[] Hot = { "Lumia 950", "Windows Phone", "Surface Pro", "Xbox", "机械键盘", "Kindle" };

        private string _keyword;
        private int _sortIndex;
        private string _message;
        private string _summary;
        private bool _hasSearched;
        private int _total = -1;

        public SearchViewModel()
        {
            Results = new IncrementalCollection<ItemSummary>(LoadPageAsync);
            Results.LoadingChanged += (s, e) => OnLoadingChanged();
            ReloadHistory();
        }

        public IncrementalCollection<ItemSummary> Results { get; }

        public ObservableCollection<string> History { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> Suggestions { get; } = new ObservableCollection<string>();

        public string[] HotKeywords => Hot;

        public string[] SortOptions { get; } = { "综合排序", "最新发布", "价格从低到高", "价格从高到低" };

        public string Keyword
        {
            get { return _keyword; }
            private set { SetProperty(ref _keyword, value); }
        }

        public int SortIndex
        {
            get { return _sortIndex; }
            set
            {
                if (value < 0 || !SetProperty(ref _sortIndex, value)) return;
                if (HasSearched) { var ignored = Results.ResetAsync(); }
            }
        }

        public bool HasSearched
        {
            get { return _hasSearched; }
            private set { SetProperty(ref _hasSearched, value); }
        }

        public string Message
        {
            get { return _message; }
            private set { SetProperty(ref _message, value); }
        }

        public string Summary
        {
            get { return _summary; }
            private set { SetProperty(ref _summary, value); }
        }

        public void UpdateSuggestions(string text)
        {
            Suggestions.Clear();
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return;
            foreach (var word in Hot.Concat(History).Distinct()
                         .Where(w => w.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
                         .Take(6))
            {
                Suggestions.Add(word);
            }
        }

        public Task SubmitAsync(string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();
            if (keyword.Length == 0) return Task.FromResult(0);
            AppServices.Settings.AddSearchHistory(keyword);
            ReloadHistory();
            Keyword = keyword;
            HasSearched = true;
            Suggestions.Clear();
            return Results.ResetAsync();
        }

        public void ClearHistory()
        {
            AppServices.Settings.ClearSearchHistory();
            ReloadHistory();
        }

        /// <summary>返回首页状态（清空结果，显示历史与热门）。返回 false 表示本来就在首页。</summary>
        public bool Reset()
        {
            if (!HasSearched) return false;
            HasSearched = false;
            Keyword = null;
            Results.Clear();
            Message = null;
            Summary = null;
            return true;
        }

        private void ReloadHistory()
        {
            History.Clear();
            foreach (var h in AppServices.Settings.SearchHistory) History.Add(h);
        }

        private async Task<PageResult<ItemSummary>> LoadPageAsync(int page)
        {
            var result = await AppServices.Data.SearchAsync(Keyword, (SearchSort)SortIndex, page);
            if (page == 1) _total = result.Total;
            return result;
        }

        private void OnLoadingChanged()
        {
            if (Results.IsLoading)
            {
                StatusTray.Begin("正在搜索“" + Keyword + "”...");
                Message = null;
                return;
            }
            StatusTray.End();
            if (Results.LastError != null)
            {
                Message = MtopException.Describe(Results.LastError);
                Summary = null;
            }
            else if (Results.Count == 0)
            {
                Message = "没有找到相关宝贝，换个关键词试试";
                Summary = null;
            }
            else
            {
                Message = null;
                Summary = _total > 0 ? "找到 " + _total + " 件宝贝" : "以下是“" + Keyword + "”的搜索结果";
            }
        }
    }
}
