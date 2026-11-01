using System;
using System.Threading.Tasks;
using Goofish.Models;
using Goofish.Services;
using Goofish.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class ItemDetailPage : Page
    {
        private readonly ItemDetailViewModel _vm = new ItemDetailViewModel();
        private string _itemId;
        private ItemSummary _preview;

        public ItemDetailPage()
        {
            InitializeComponent();
            DataContext = _vm;
            SizeChanged += (s, e) => Gallery.Height = Math.Min(e.NewSize.Width, 480);
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ItemDetailViewModel.IsFavorite) || e.PropertyName == nameof(ItemDetailViewModel.Item))
                    UpdateCommands();
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
            if (e.NavigationMode == NavigationMode.Back && _vm.HasItem)
            {
                // 从私信页返回，收藏状态可能在别处改过
                UpdateCommands();
                return;
            }

            _preview = e.Parameter as ItemSummary;
            _itemId = _preview?.ItemId ?? e.Parameter as string;
            UpdateCommands();
            if (!string.IsNullOrEmpty(_itemId)) await _vm.LoadAsync(_itemId, _preview);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
            base.OnNavigatedFrom(e);
        }

        private void UpdateCommands()
        {
            var ready = _vm.HasItem;
            FavoriteButton.IsEnabled = ready;
            ChatButton.IsEnabled = ready;
            ShareButton.IsEnabled = ready;
            FavoriteIcon.Symbol = _vm.IsFavorite ? Symbol.SolidStar : Symbol.OutlineStar;
            FavoriteButton.Label = _vm.IsFavorite ? "取消收藏" : "收藏";
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_itemId)) await _vm.LoadAsync(_itemId, _preview);
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureSignedInAsync("收藏宝贝")) return;
            var message = await _vm.ToggleFavoriteAsync();
            if (message != null) await new MessageDialog(message, "收藏").ShowAsync();
        }

        private async void Chat_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureSignedInAsync("联系卖家")) return;
            Frame.Navigate(typeof(ChatPage), new ChatArgs { Item = _vm.Item });
        }

        private void Seller_Click(object sender, RoutedEventArgs e)
        {
            var seller = _vm.Item?.Seller;
            if (seller == null) return;
            if (!string.IsNullOrEmpty(seller.UserId))
            {
                Frame.Navigate(typeof(UserProfilePage), new UserProfileArgs { UserId = seller.UserId, Nick = seller.Nick });
                return;
            }
            // 拿不到用户 ID 时退回按昵称搜索
            if (!string.IsNullOrEmpty(seller.Nick)) Frame.Navigate(typeof(SearchPage), seller.Nick);
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var item = _vm.Item;
            if (item == null) return;
            var request = args.Request;
            request.Data.Properties.Title = item.Title;
            request.Data.Properties.Description = item.PriceText + " · 闲鱼";
            request.Data.SetWebLink(new Uri(item.WebUrl));
            request.Data.SetText(item.Title + " " + item.PriceText + "\n" + item.WebUrl);
        }

        private async void OpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Item != null) await Launcher.LaunchUriAsync(new Uri(_vm.Item.WebUrl));
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Item == null) return;
            var package = new DataPackage();
            package.SetText(_vm.Item.WebUrl);
            Clipboard.SetContent(package);
        }

        /// <summary>真实数据下收藏和聊天都需要登录；演示数据不需要。</summary>
        private async Task<bool> EnsureSignedInAsync(string action)
        {
            if (AppServices.UseDemo || AppServices.Account.IsSignedIn) return true;
            var dialog = new ContentDialog
            {
                Title = "需要登录",
                Content = new TextBlock
                {
                    Text = action + "需要登录闲鱼账号。登录在内置浏览器里完成，本应用看不到你的密码。",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "登录",
                SecondaryButtonText = "取消"
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) Frame.Navigate(typeof(LoginPage));
            return false;
        }
    }
}
