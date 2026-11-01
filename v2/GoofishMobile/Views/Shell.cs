using Goofish.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Goofish.Views
{
    /// <summary>
    /// 窗口根元素：放 Frame、负责主题与返回键。
    /// 手机上的加载提示由系统托盘显示，桌面没有托盘，就在顶部放一条不确定进度条。
    /// </summary>
    public sealed class Shell : Grid
    {
        private readonly ProgressBar _desktopProgress;

        public Shell()
        {
            Frame = new Frame();
            Frame.ContentTransitions = new TransitionCollection { new NavigationThemeTransition() };
            Frame.Navigated += (s, e) => UpdateBackButton();
            Children.Add(Frame);

            _desktopProgress = new ProgressBar
            {
                IsIndeterminate = true,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Background = null
            };
            Children.Add(_desktopProgress);

            StatusTray.BusyChanged += (s, busy) =>
            {
                if (!StatusTray.HasStatusBar)
                    _desktopProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            };

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            ApplyTheme();
        }

        public Frame Frame { get; }

        public void ApplyTheme()
        {
            switch (AppServices.Settings.Theme)
            {
                case "dark": RequestedTheme = ElementTheme.Dark; break;
                case "light": RequestedTheme = ElementTheme.Light; break;
                default: RequestedTheme = ElementTheme.Default; break;
            }
            var dark = RequestedTheme == ElementTheme.Dark ||
                       (RequestedTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            StatusTray.ApplyTheme(dark);
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (e.Handled) return;
            var page = Frame.Content as IBackHandler;
            if (page != null && page.HandleBack())
            {
                e.Handled = true;
                return;
            }
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private void UpdateBackButton()
        {
            // 手机用硬件/导航栏返回键；桌面在标题栏显示返回按钮
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                Frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }
    }

    /// <summary>页面想先自己处理返回键（关闭浮层、清空搜索）时实现它。</summary>
    public interface IBackHandler
    {
        bool HandleBack();
    }
}
