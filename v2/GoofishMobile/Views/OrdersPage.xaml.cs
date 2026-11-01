using Goofish.Models;
using Goofish.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class OrdersPage : Page
    {
        private readonly OrdersViewModel _vm = new OrdersViewModel();

        public OrdersPage()
        {
            InitializeComponent();
            DataContext = _vm;
            TabBox.ItemsSource = OrdersViewModel.TabNames;
            StatusBox.ItemsSource = OrdersViewModel.StatusNames;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.RefreshAsync();
        }

        private void Order_Click(object sender, ItemClickEventArgs e)
        {
            var order = e.ClickedItem as OrderSummary;
            if (order != null) Frame.Navigate(typeof(OrderDetailPage), order);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await _vm.RefreshAsync();
        }
    }
}
