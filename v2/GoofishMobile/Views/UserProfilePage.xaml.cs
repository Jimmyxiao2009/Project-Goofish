using Goofish.Models;
using Goofish.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    /// <summary>卖家 / 个人主页。入口：宝贝详情页的卖家行、私信页顶部的对方信息。</summary>
    public sealed partial class UserProfilePage : Page
    {
        private readonly UserProfileViewModel _vm = new UserProfileViewModel();

        public UserProfilePage()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back && _vm.HasProfile) return;

            var args = e.Parameter as UserProfileArgs;
            var userId = args?.UserId ?? e.Parameter as string;
            await _vm.LoadAsync(userId, args?.Nick);
        }

        private void Item_Click(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ItemSummary;
            if (item != null) Frame.Navigate(typeof(ItemDetailPage), item);
        }

        private void Chat_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var profile = _vm.Profile;
            if (profile == null || string.IsNullOrEmpty(profile.UserId)) return;
            Frame.Navigate(typeof(ChatPage), new ChatArgs { PeerUserId = profile.UserId, PeerNick = profile.Nick });
        }
    }
}
