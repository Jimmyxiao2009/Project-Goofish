using System;
using System.Collections.Specialized;
using Goofish.Common;
using Goofish.Models;
using Goofish.ViewModels;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class ChatPage : Page
    {
        private readonly ChatViewModel _vm;

        public ChatPage()
        {
            InitializeComponent();
            _vm = new ChatViewModel(Dispatcher);
            DataContext = _vm;
            _vm.Messages.CollectionChanged += OnMessagesChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            var args = e.Parameter as ChatArgs ?? new ChatArgs { Cid = e.Parameter as string };
            await _vm.LoadAsync(args);
            ScrollToEnd();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            HideMedia();
            _vm.Unload();
            base.OnNavigatedFrom(e);
        }

        /// <summary>查看图片/视频时，返回键先关掉查看层，而不是退出会话。</summary>
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (MediaOverlay.Visibility != Visibility.Visible) return;
            HideMedia();
            e.Handled = true;
        }

        private void OnMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add) ScrollToEnd();
        }

        private async void ScrollToEnd()
        {
            // 等列表把新增项布局完再滚动
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (_vm.Messages.Count > 0) MessageList.ScrollIntoView(_vm.Messages[_vm.Messages.Count - 1]);
            });
        }

        private void DraftBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TwoWay 绑定默认在失去焦点时才回写，这里立即同步，发送按钮才能实时启用
            _vm.Draft = DraftBox.Text;
        }

        private async void DraftBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            await _vm.SendAsync();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            await _vm.SendAsync();
        }

        private async void QuickReply_Click(object sender, RoutedEventArgs e)
        {
            var text = (sender as MenuFlyoutItem)?.Text;
            if (!string.IsNullOrEmpty(text)) await _vm.SendAsync(text);
        }

        private async void Message_Click(object sender, ItemClickEventArgs e)
        {
            var message = e.ClickedItem as ChatMessage;
            if (message == null) return;

            // 图片/视频点开是查看，卡片点开是跳转，文本消息点一下是重发失败的
            if (message.Kind == MessageKind.Video)
            {
                ShowVideo(message);
                return;
            }
            if (message.Kind == MessageKind.Image)
            {
                ShowPhoto(message);
                return;
            }
            if (message.Kind == MessageKind.Card)
            {
                await OpenCardAsync(message);
                return;
            }
            await _vm.RetryAsync(message);
        }

        /// <summary>卡片：是宝贝就进详情页，否则用浏览器打开链接。</summary>
        private async System.Threading.Tasks.Task OpenCardAsync(ChatMessage message)
        {
            if (message == null) return;
            if (!string.IsNullOrEmpty(message.CardItemId))
            {
                Frame.Navigate(typeof(ItemDetailPage), message.CardItemId);
                return;
            }
            if (string.IsNullOrEmpty(message.CardUrl)) return;
            try
            {
                await Launcher.LaunchUriAsync(new Uri(TextFormat.NormalizeUrl(message.CardUrl)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("卡片链接打不开：" + ex.Message);
            }
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_vm.ItemId)) Frame.Navigate(typeof(ItemDetailPage), _vm.ItemId);
        }

        private void Peer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_vm.PeerUserId)) return;
            Frame.Navigate(typeof(UserProfilePage), new UserProfileArgs { UserId = _vm.PeerUserId, Nick = _vm.PeerNick });
        }

        private async void OpenWeb_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.goofish.com/im"));
        }

        // ---- 图片 / 视频查看层 ----

        private void ShowPhoto(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.MediaUrl)) return;

            StopVideo();
            MediaTitle.Text = "图片";
            try
            {
                PhotoView.Source = new BitmapImage(new Uri(TextFormat.NormalizeUrl(message.MediaUrl)));
                PhotoView.Visibility = Visibility.Visible;
                MediaOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MediaTitle.Text = "图片打不开：" + ex.Message;
                MediaOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ShowVideo(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.MediaUrl)) return;

            PhotoView.Source = null;
            PhotoView.Visibility = Visibility.Collapsed;
            MediaTitle.Text = "视频";
            MediaOverlay.Visibility = Visibility.Visible;
            VideoView.Visibility = Visibility.Visible;
            try
            {
                VideoView.Source = new Uri(TextFormat.NormalizeUrl(message.MediaUrl));
                VideoView.Play();
            }
            catch (Exception ex)
            {
                MediaTitle.Text = "视频打不开：" + ex.Message;
            }
        }

        private void MediaClose_Click(object sender, RoutedEventArgs e)
        {
            HideMedia();
        }

        private void PhotoView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            HideMedia();
        }

        private void HideMedia()
        {
            if (MediaOverlay == null || MediaOverlay.Visibility != Visibility.Visible) return;
            StopVideo();
            PhotoView.Source = null;
            PhotoView.Visibility = Visibility.Collapsed;
            MediaOverlay.Visibility = Visibility.Collapsed;
        }

        private void StopVideo()
        {
            try
            {
                VideoView.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("停止视频失败：" + ex.Message);
            }
            VideoView.Source = null;
            VideoView.Visibility = Visibility.Collapsed;
        }
    }
}
