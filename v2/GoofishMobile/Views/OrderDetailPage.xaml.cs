using System;
using System.Threading.Tasks;
using Goofish.Models;
using Goofish.Services;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    /// <summary>订单详情。列表里带过来的字段先填上，接口回来后再补齐。</summary>
    public sealed partial class OrderDetailPage : Page
    {
        private OrderSummary _order;

        public OrderDetailPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _order = e.Parameter as OrderSummary;
            if (_order != null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            Busy.IsActive = true;
            Busy.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Collapsed;

            try
            {
                var detail = await AppServices.Data.GetOrderDetailAsync(_order.OrderId, _order.IsSold);
                // 详情接口的字段可能缺失，用列表里已有的补上
                if (string.IsNullOrEmpty(detail.Title)) detail.Title = _order.Title;
                if (string.IsNullOrEmpty(detail.CoverUrl)) detail.CoverUrl = _order.CoverUrl;
                if (detail.Price == 0) detail.Price = _order.Price;
                if (detail.Quantity == 0) detail.Quantity = _order.Quantity;
                if (string.IsNullOrEmpty(detail.StatusText)) detail.StatusText = _order.StatusText;
                if (string.IsNullOrEmpty(detail.CreateTime)) detail.CreateTime = _order.CreateTime;
                detail.PlaceholderColor = _order.PlaceholderColor;
                detail.PlaceholderGlyph = _order.PlaceholderGlyph;

                DataContext = detail;
                DetailPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ErrorText.Text = MtopException.Describe(ex);
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                Busy.IsActive = false;
                Busy.Visibility = Visibility.Collapsed;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_order != null) await LoadAsync();
        }

        private async void Web_Click(object sender, RoutedEventArgs e)
        {
            if (_order != null) await Launcher.LaunchUriAsync(new Uri(_order.WebUrl));
        }
    }
}
