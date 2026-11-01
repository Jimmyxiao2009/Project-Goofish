using System;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;
using Goofish.ViewModels;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class MainPage : Page
    {
        private const int FeedPivot = 0;
        private const int MessagesPivot = 1;
        private const int FavoritesPivot = 2;

        private readonly MainViewModel _vm = new MainViewModel();
        private bool _favoritesLoaded;
        private string _dataSourceKey;

        public MainPage()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateAccountButton();

            // 登录、注销或切换演示模式后，三个分区的数据都要换成新的来源
            var key = (AppServices.Account.UserId ?? "-") + "|" + AppServices.UseDemo;
            if (key != _dataSourceKey)
            {
                _dataSourceKey = key;
                _favoritesLoaded = false;
                _vm.NotifyAccountChanged();
                if (AppServices.Account.IsSignedIn)
                {
                    var ignored = AppServices.Im.TryConnectAsync();
                    try { await AppServices.Account.RefreshProfileAsync(); } catch (Exception) { }
                    UpdateAccountButton();
                }
                await _vm.RefreshFeedAsync();
            }

            // 从会话页返回时刷新未读数
            if (RootPivot.SelectedIndex == MessagesPivot) await _vm.LoadConversationsAsync(e.NavigationMode == NavigationMode.Back);
            else if (RootPivot.SelectedIndex == FavoritesPivot && !_favoritesLoaded) await LoadFavoritesAsync();
            else _vm.RecountUnread();
        }

        private async void RootPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            switch (RootPivot.SelectedIndex)
            {
                case MessagesPivot:
                    await _vm.LoadConversationsAsync();
                    break;
                case FavoritesPivot:
                    if (!_favoritesLoaded) await LoadFavoritesAsync();
                    break;
            }
        }

        private async System.Threading.Tasks.Task LoadFavoritesAsync()
        {
            _favoritesLoaded = true;
            await _vm.ReloadFavoritesAsync();
        }

        private ItemsWrapGrid _feedPanel;

        // 面板在 GridView 第一次 SizeChanged 之后才创建，两处都要算一次
        private void FeedPanel_Loaded(object sender, RoutedEventArgs e)
        {
            _feedPanel = sender as ItemsWrapGrid;
            LayoutFeed(FeedGrid.ActualWidth);
        }

        private void FeedGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LayoutFeed(e.NewSize.Width);
        }

        /// <summary>推荐页双列：每列宽度随屏幕变化，方块图边长 = 列宽 - 左右 4px 间距。</summary>
        private void LayoutFeed(double totalWidth)
        {
            var panel = _feedPanel ?? FeedGrid.ItemsPanelRoot as ItemsWrapGrid;
            if (panel == null || totalWidth <= 0) return;
            var usable = totalWidth - FeedGrid.Padding.Left - FeedGrid.Padding.Right;
            // 手机竖屏两列；横屏或桌面窗口宽了就加列，保持方块不超过约 220px
            var columns = Math.Max(2, (int)Math.Floor(usable / 220));
            var width = Math.Floor(usable / columns);
            panel.ItemWidth = width;
            ((LayoutMetrics)Application.Current.Resources["Layout"]).TileSize = width - 8;
        }

        private void Item_Click(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ItemSummary;
            if (item != null) Frame.Navigate(typeof(ItemDetailPage), item);
        }

        private void Conversation_Click(object sender, ItemClickEventArgs e)
        {
            var conv = e.ClickedItem as Conversation;
            if (conv != null) Frame.Navigate(typeof(ChatPage), new ChatArgs { Cid = conv.Cid, Conversation = conv });
        }

        /// <summary>长按（触屏）或右键（鼠标）收藏项：弹出上下文菜单。</summary>
        private void FavoriteList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            var item = element?.DataContext as ItemSummary;
            if (item == null) return;

            var menu = new MenuFlyout();
            var remove = new MenuFlyoutItem { Text = "取消收藏" };
            remove.Click += async (s, a) =>
            {
                try
                {
                    await StatusTray.RunAsync("正在取消收藏...", () => AppServices.Data.SetFavoriteAsync(item, false));
                    _vm.Favorites.Remove(item);
                }
                catch (Exception ex)
                {
                    await new MessageDialog(MtopException.Describe(ex), "取消收藏失败").ShowAsync();
                }
            };
            var detail = new MenuFlyoutItem { Text = "查看宝贝" };
            detail.Click += (s, a) => Frame.Navigate(typeof(ItemDetailPage), item);
            menu.Items.Add(remove);
            menu.Items.Add(detail);
            menu.ShowAt(element, e.GetPosition(element));
            e.Handled = true;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            switch (RootPivot.SelectedIndex)
            {
                case FeedPivot:
                    if (_vm.Feed.Count > 0) FeedGrid.ScrollIntoView(_vm.Feed[0]);
                    await _vm.RefreshFeedAsync();
                    break;
                case MessagesPivot:
                    await _vm.LoadConversationsAsync(true);
                    break;
                default:
                    await LoadFavoritesAsync();
                    break;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SearchPage));
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            // 订单要登录才有；没登录也没开演示数据就先引导登录
            Frame.Navigate(AppServices.Account.IsSignedIn || AppServices.Settings.DemoMode
                ? typeof(OrdersPage)
                : typeof(LoginPage));
        }

        private void Account_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(AppServices.Account.IsSignedIn ? typeof(SettingsPage) : typeof(LoginPage));
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "关于",
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = "闲鱼 for Windows 10 Mobile（第三方）\n版本 2.0.0\n\n" +
                           "本应用通过闲鱼网页版使用的接口工作，不提供付款与下单功能，与阿里巴巴及闲鱼无关。"
                },
                PrimaryButtonText = "确定"
            };
            await dialog.ShowAsync();
        }

        private void UpdateAccountButton()
        {
            AccountButton.Label = AppServices.Account.IsSignedIn ? "账户" : "登录";
        }
    }
}
