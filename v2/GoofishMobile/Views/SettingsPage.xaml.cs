using System;
using Goofish.Services;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class SettingsPage : Page
    {
        // 初始化控件状态时会触发 Checked/Toggled，期间不写回设置
        private bool _loading;

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _loading = true;
            var settings = AppServices.Settings;
            ThemeDark.IsChecked = settings.Theme == "dark";
            ThemeLight.IsChecked = settings.Theme == "light";
            ThemeSystem.IsChecked = settings.Theme != "dark" && settings.Theme != "light";
            DemoSwitch.IsOn = settings.DemoMode;
            SaverSwitch.IsOn = settings.DataSaver;
            NotifySwitch.IsOn = settings.Notifications;
            _loading = false;
            UpdateAccount();
        }

        private void UpdateAccount()
        {
            var account = AppServices.Account;
            if (account.IsSignedIn)
            {
                var info = account.Info;
                AccountName.Text = info.Nick;
                AccountStatus.Text = "已登录 · 用户 ID " + info.UserId;
                AccountButton.Content = "注销";
                AvatarInitial.Text = string.IsNullOrEmpty(info.Nick) ? "鱼" : info.Nick.Substring(0, 1);
                AvatarImage.ImageSource = string.IsNullOrEmpty(info.AvatarUrl) ? null : new BitmapImage(new Uri(info.AvatarUrl));
            }
            else
            {
                AccountName.Text = "未登录";
                AccountStatus.Text = "登录后可以收藏宝贝、同步收藏夹并和卖家聊天。";
                AccountButton.Content = "登录";
                AvatarInitial.Text = "鱼";
                AvatarImage.ImageSource = null;
            }
        }

        private async void Account_Click(object sender, RoutedEventArgs e)
        {
            if (!AppServices.Account.IsSignedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }
            var dialog = new ContentDialog
            {
                Title = "注销闲鱼账号？",
                Content = new TextBlock
                {
                    Text = "将清除本机保存的登录信息。收藏和聊天记录保存在闲鱼服务器上，不会丢失。",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "注销",
                SecondaryButtonText = "取消"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            AppServices.Im.Disconnect();
            AppServices.Account.SignOut();
            UpdateAccount();
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppServices.Settings.Theme = (string)((FrameworkElement)sender).Tag;
            App.Shell?.ApplyTheme();
        }

        private void DemoSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_loading) AppServices.Settings.DemoMode = DemoSwitch.IsOn;
        }

        private void SaverSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_loading) AppServices.Settings.DataSaver = SaverSwitch.IsOn;
        }

        private void NotifySwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_loading) AppServices.Settings.Notifications = NotifySwitch.IsOn;
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            AppServices.Settings.ClearSearchHistory();
            await new MessageDialog("搜索历史已清除。").ShowAsync();
        }
    }
}
