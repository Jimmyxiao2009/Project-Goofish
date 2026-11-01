using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using ProjectGoofish.Models;
using ProjectGoofish.ViewModels;

namespace ProjectGoofish.Views
{
    /// <summary>
    /// 私聊页面代码隐藏逻辑
    /// </summary>
    public sealed partial class ChatPage : Page
    {
        public ChatViewModel ViewModel { get; } = new ChatViewModel();

        public ChatPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ItemDetail item)
            {
                var conv = new Conversation
                {
                    Id = "conv_new",
                    SellerId = item.Seller?.UserId,
                    SellerNick = item.Seller?.Nick ?? "卖家",
                    SellerAvatar = item.Seller?.Avatar,
                    ItemId = item.ItemId,
                    ItemTitle = item.Title,
                    ItemPrice = item.Price,
                    ItemThumb = item.PrimaryImageUrl
                };
                ViewModel.SetActiveConversation(conv);
            }
            else
            {
                await ViewModel.LoadConversationsAsync();
                if (ViewModel.Conversations.Count > 0)
                {
                    ViewModel.SetActiveConversation(ViewModel.Conversations[0]);
                }
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await DoSendAsync();
        }

        private async void MessageInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                await DoSendAsync();
            }
        }

        private async System.Threading.Tasks.Task DoSendAsync()
        {
            var text = MessageInputBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            MessageInputBox.Text = string.Empty;
            await ViewModel.SendMessageAsync(text);

            // 滚动到最新气泡
            if (ViewModel.CurrentMessages.Count > 0)
            {
                MessagesListView.ScrollIntoView(ViewModel.CurrentMessages[ViewModel.CurrentMessages.Count - 1]);
            }
        }
    }
}
