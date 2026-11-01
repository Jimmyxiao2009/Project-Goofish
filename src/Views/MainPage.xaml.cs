using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ProjectGoofish.Models;
using ProjectGoofish.ViewModels;

namespace ProjectGoofish.Views
{
    /// <summary>
    /// 首页视图代码隐藏逻辑
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadFeedAsync();
        }

        private void FeedGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemDetail item)
            {
                Frame.Navigate(typeof(ItemDetailPage), item.ItemId);
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RootPivot.SelectedIndex == 0) ViewModel.SelectedCategory = "all";
            else if (RootPivot.SelectedIndex == 1) ViewModel.SelectedCategory = "digital";
            else if (RootPivot.SelectedIndex == 2) ViewModel.SelectedCategory = "game";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadFeedAsync();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SearchPage));
        }

        private void Messages_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ChatPage));
        }

        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            // 导航至收藏夹
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            // 切换应用主题
            if (RequestedTheme == ElementTheme.Dark)
                RequestedTheme = ElementTheme.Light;
            else
                RequestedTheme = ElementTheme.Dark;
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Project Goofish UWP",
                Content = "专为 Windows 10 Mobile 设计的非官方闲鱼客户端。\n纯粹二手捡漏与私聊，无付款干扰。",
                CloseButtonText = "确定"
            };
            await dialog.ShowAsync();
        }
    }
}
