using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Models;
using Goofish.Services;

namespace Goofish.ViewModels
{
    /// <summary>打开个人主页要带的信息：UserId 必填，Nick 用于接口没返回昵称时兜底显示。</summary>
    public sealed class UserProfileArgs
    {
        public string UserId { get; set; }
        public string Nick { get; set; }
    }

    public sealed class UserProfileViewModel : BindableBase
    {
        private UserProfile _profile;
        private bool _isLoading;
        private string _error;
        private string _itemsMessage;

        public UserProfileViewModel()
        {
            Items = new IncrementalCollection<ItemSummary>(LoadItemsAsync);
            Items.LoadingChanged += (s, e) => OnItemsLoadingChanged();
        }

        public IncrementalCollection<ItemSummary> Items { get; }

        public UserProfile Profile
        {
            get { return _profile; }
            private set
            {
                if (SetProperty(ref _profile, value)) OnPropertyChanged(nameof(HasProfile));
            }
        }

        public bool HasProfile => Profile != null;

        public bool IsLoading
        {
            get { return _isLoading; }
            private set { SetProperty(ref _isLoading, value); }
        }

        public string Error
        {
            get { return _error; }
            private set { SetProperty(ref _error, value); }
        }

        public string ItemsMessage
        {
            get { return _itemsMessage; }
            private set { SetProperty(ref _itemsMessage, value); }
        }

        public async Task LoadAsync(string userId, string nick)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Error = "没有拿到用户 ID，打不开主页。";
                return;
            }

            IsLoading = true;
            Error = null;
            try
            {
                var profile = await StatusTray.RunAsync("正在加载主页...",
                    () => AppServices.Data.GetUserProfileAsync(userId));
                if (string.IsNullOrEmpty(profile.Nick)) profile.Nick = nick;
                if (string.IsNullOrEmpty(profile.AvatarColor)) profile.AvatarColor = "#FF767676";
                Profile = profile;
            }
            catch (Exception ex)
            {
                Error = MtopException.Describe(ex);
            }
            finally
            {
                IsLoading = false;
            }

            if (HasProfile) await Items.ResetAsync();
        }

        /// <summary>
        /// 主页的商品列表：用昵称搜索（接口只给了用户 ID，没有“某人在售”的列表接口）。
        /// 结果里可能有同名的别人，页面会注明这一点。
        /// </summary>
        private async Task<PageResult<ItemSummary>> LoadItemsAsync(int page)
        {
            var nick = Profile?.Nick;
            if (string.IsNullOrEmpty(nick)) return new PageResult<ItemSummary>(new List<ItemSummary>(), false);
            return await AppServices.Data.SearchAsync(nick, SearchSort.Default, page);
        }

        private void OnItemsLoadingChanged()
        {
            if (Items.IsLoading) return;
            if (Items.LastError != null) ItemsMessage = MtopException.Describe(Items.LastError);
            else ItemsMessage = Items.Count == 0 ? "没有找到 TA 在售的宝贝。" : null;
        }
    }
}
