using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Goofish.Models;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Goofish.Common
{
    /// <summary>
    /// 支持 ListView/GridView 滚动到底自动加载下一页的集合。
    /// </summary>
    public class IncrementalCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        private readonly Func<int, Task<PageResult<T>>> _loader;
        private int _page;
        private bool _hasMore = true;
        private bool _busy;
        private int _generation;

        public IncrementalCollection(Func<int, Task<PageResult<T>>> loader)
        {
            _loader = loader;
        }

        /// <summary>加载失败后停止自动加载，避免 ListView 不停重试同一个出错的请求。</summary>
        public Exception LastError { get; private set; }

        public bool IsLoading => _busy;

        public bool HasMoreItems => _hasMore && LastError == null;

        public event EventHandler LoadingChanged;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                var added = await LoadNextPageAsync();
                return new LoadMoreItemsResult { Count = (uint)added };
            });
        }

        public async Task<int> LoadNextPageAsync()
        {
            if (_busy || !_hasMore) return 0;
            var generation = _generation;
            SetBusy(true);
            try
            {
                var result = await _loader(_page + 1);
                // 加载期间被 Reset 过，丢弃旧结果
                if (generation != _generation) return 0;
                _page++;
                _hasMore = result.HasMore && result.Items.Count > 0;
                LastError = null;
                foreach (var item in result.Items) Add(item);
                return result.Items.Count;
            }
            catch (Exception ex)
            {
                if (generation == _generation) LastError = ex;
                return 0;
            }
            finally
            {
                if (generation == _generation) SetBusy(false);
            }
        }

        public Task<int> ResetAsync()
        {
            _generation++;
            _busy = false;
            _page = 0;
            _hasMore = true;
            LastError = null;
            Clear();
            return LoadNextPageAsync();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            LoadingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
