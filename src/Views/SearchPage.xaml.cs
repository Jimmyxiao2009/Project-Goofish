using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ProjectGoofish.Models;
using ProjectGoofish.ViewModels;

namespace ProjectGoofish.Views
{
    /// <summary>
    /// 搜索页面代码隐藏逻辑
    /// </summary>
    public sealed partial class SearchPage : Page
    {
        public SearchViewModel ViewModel { get; } = new SearchViewModel();
        private string _currentSort = "default";

        public SearchPage()
        {
            this.InitializeComponent();
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ViewModel.Keyword = args.QueryText;
            await ViewModel.ExecuteSearchAsync(_currentSort);
        }

        private async void Sort_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _currentSort = rb.Tag.ToString();
                if (!string.IsNullOrEmpty(ViewModel.Keyword))
                {
                    await ViewModel.ExecuteSearchAsync(_currentSort);
                }
            }
        }

        private void SearchResultsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemDetail item)
            {
                Frame.Navigate(typeof(ItemDetailPage), item.ItemId);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
