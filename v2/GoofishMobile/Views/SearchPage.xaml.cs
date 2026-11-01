using System;
using Goofish.Models;
using Goofish.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    public sealed partial class SearchPage : Page, IBackHandler
    {
        private readonly SearchViewModel _vm = new SearchViewModel();

        public SearchPage()
        {
            InitializeComponent();
            DataContext = _vm;
            // 返回到这一页时保留关键词与结果
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var keyword = e.Parameter as string;
            if (e.NavigationMode == NavigationMode.New && !string.IsNullOrEmpty(keyword))
            {
                SearchBox.Text = keyword;
                await _vm.SubmitAsync(keyword);
            }
            else if (e.NavigationMode == NavigationMode.New && !_vm.HasSearched)
            {
                // 让输入框获得焦点并弹出键盘
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => SearchBox.Focus(FocusState.Programmatic));
            }
        }

        /// <summary>在结果页按返回键先回到搜索首页，再按一次才离开。</summary>
        public bool HandleBack()
        {
            if (!_vm.Reset()) return false;
            SearchBox.Text = string.Empty;
            return true;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) _vm.UpdateSuggestions(sender.Text);
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var keyword = args.ChosenSuggestion as string ?? args.QueryText;
            sender.Text = keyword ?? string.Empty;
            // 让输入框失去焦点以收起软键盘
            sender.IsEnabled = false;
            sender.IsEnabled = true;
            await _vm.SubmitAsync(keyword);
        }

        private async void Keyword_Click(object sender, ItemClickEventArgs e)
        {
            var keyword = e.ClickedItem as string;
            SearchBox.Text = keyword ?? string.Empty;
            await _vm.SubmitAsync(keyword);
        }

        private void Result_Click(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ItemSummary;
            if (item != null) Frame.Navigate(typeof(ItemDetailPage), item);
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearHistory();
        }
    }
}
