using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;

namespace Goofish.ViewModels
{
    /// <summary>
    /// 订单列表。买到的/卖出的用同一个增量集合，切换页签或状态时 Reset 重新拉第一页。
    /// </summary>
    public sealed class OrdersViewModel : BindableBase
    {
        /// <summary>与 BoughtOrderStatus 的取值顺序一一对应。</summary>
        public static readonly string[] StatusNames = { "全部", "待付款", "待发货", "待收货", "待评价", "退款中" };

        public static readonly string[] TabNames = { "我买到的", "我卖出的" };

        private readonly IncrementalCollection<OrderSummary> _orders;
        private OrderTab _tab = OrderTab.Bought;
        private BoughtOrderStatus _status = BoughtOrderStatus.All;
        private bool _ready;
        private bool _loading;
        private string _error;

        public OrdersViewModel()
        {
            // 加载器在调用时才读当前页签与状态，所以切页签只要 Reset 一下
            _orders = new IncrementalCollection<OrderSummary>(page =>
                _tab == OrderTab.Sold
                    ? AppServices.Data.GetSoldOrdersAsync(page)
                    : AppServices.Data.GetBoughtOrdersAsync(_status, page));
            _orders.LoadingChanged += (s, e) => IsLoading = _orders.IsLoading;
        }

        public ObservableCollection<OrderSummary> Orders => _orders;

        public bool IsSold => _tab == OrderTab.Sold;

        public bool ShowStatusFilter => _tab == OrderTab.Bought;

        public bool IsReady
        {
            get { return _ready; }
            private set { if (SetProperty(ref _ready, value)) OnPropertyChanged(nameof(IsEmpty)); }
        }

        public bool IsEmpty => _ready && _orders.Count == 0;

        public bool IsLoading
        {
            get { return _loading; }
            private set { if (SetProperty(ref _loading, value)) OnPropertyChanged(nameof(HasError)); }
        }

        public string Error
        {
            get { return _error; }
            private set
            {
                if (SetProperty(ref _error, value))
                {
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_error);

        /// <summary>绑到 ComboBox.SelectedIndex。</summary>
        public int TabIndex
        {
            get { return (int)_tab; }
            set
            {
                var tab = (OrderTab)value;
                if (tab == _tab) return;
                _tab = tab;
                OnPropertyChanged(nameof(TabIndex));
                OnPropertyChanged(nameof(IsSold));
                OnPropertyChanged(nameof(ShowStatusFilter));
                _ = ReloadAsync();
            }
        }

        /// <summary>绑到状态 ComboBox.SelectedIndex，取值与 StatusNames 对齐。</summary>
        public int StatusIndex
        {
            get { return (int)_status; }
            set
            {
                var status = (BoughtOrderStatus)value;
                if (status == _status) return;
                _status = status;
                OnPropertyChanged(nameof(StatusIndex));
                if (_tab == OrderTab.Bought) _ = ReloadAsync();
            }
        }

        public Task RefreshAsync()
        {
            return ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            Error = null;
            IsReady = false;
            try
            {
                await _orders.ResetAsync();
                if (_orders.LastError != null) Error = MtopException.Describe(_orders.LastError);
            }
            catch (System.Exception ex)
            {
                Error = MtopException.Describe(ex);
            }
            finally
            {
                IsReady = true;
            }
        }
    }
}
