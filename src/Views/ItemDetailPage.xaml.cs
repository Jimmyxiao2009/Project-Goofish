using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ProjectGoofish.ViewModels;

namespace ProjectGoofish.Views
{
    /// <summary>
    /// 商品详情页代码隐藏逻辑
    /// </summary>
    public sealed partial class ItemDetailPage : Page
    {
        public ItemDetailViewModel ViewModel { get; } = new ItemDetailViewModel();

        public ItemDetailPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string itemId)
            {
                await ViewModel.LoadItemAsync(itemId);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ToggleFavoriteAsync();
        }

        private void Chat_Click(object sender, RoutedEventArgs e)
        {
            // 携带商品上下文进入聊天页
            Frame.Navigate(typeof(ChatPage), ViewModel.Item);
        }
    }
}
