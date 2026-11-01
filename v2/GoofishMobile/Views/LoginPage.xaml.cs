using System;
using System.Threading;
using System.Threading.Tasks;
using Goofish.Common;
using Goofish.Services;
using Windows.Graphics.Display;
using Windows.System.Display;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Goofish.Views
{
    /// <summary>
    /// 电脑版扫码登录。
    ///
    /// 原来是在 WebView 里打开官方登录页，但 W10M 的 Edge 内核太老，登录页里那套人机校验会直接报错。
    /// 现在改成纯 HTTP 走闲鱼电脑版的扫码链路（见 <see cref="GoofishQrLogin"/>），把二维码画在页面上，
    /// 用户用另一台设备上的闲鱼 App 扫码确认，扫完把会话 Cookie 交给 MtopClient，和以前一样。
    /// 备用入口还是粘贴浏览器里复制的 Cookie。
    /// </summary>
    public sealed partial class LoginPage : Page
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(3);

        /// <summary>二维码的目标边长（DIP）。小屏上按可用宽度收缩，避免顶满或溢出。</summary>
        private const double QrMaxDip = 200;
        private const double QrMinDip = 140;

        /// <summary>二维码四周留白（含边框与页面边距），用来算可用宽度。</summary>
        private const double QrSideMarginDip = 56;

        private GoofishQrLogin _login;
        private CancellationTokenSource _cancel;

        /// <summary>当前正在展示的二维码。窗口尺寸变化时用它重画。</summary>
        private QrSymbol _symbol;

        /// <summary>扫码期间不让屏幕熄掉：用户是拿另一台设备对着屏幕扫的。</summary>
        private readonly DisplayRequest _display = new DisplayRequest();
        private bool _displayActive;

        public LoginPage()
        {
            InitializeComponent();
            // 旋转或改变窗口大小时重画二维码，始终按当前宽度铺满可用空间
            RootGrid.SizeChanged += (s, e) =>
            {
                if (_symbol != null) RenderQr();
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            StartSession();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            StopSession();
        }

        /// <summary>开一轮新的扫码会话（首次进入和点“重新获取二维码”都走这里）。</summary>
        private void StartSession()
        {
            StopSession();
            _login = new GoofishQrLogin(AppServices.Mtop);
            _cancel = new CancellationTokenSource();
            var login = _login;
            var cancel = _cancel.Token;
            _ = RunAsync(login, cancel);
        }

        private void StopSession()
        {
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }
            if (_login != null)
            {
                _login.Dispose();
                _login = null;
            }
            _symbol = null;
            ReleaseDisplay();
        }

        private async Task RunAsync(GoofishQrLogin login, CancellationToken cancel)
        {
            try
            {
                ShowBusy("正在获取二维码…");
                await login.StartAsync(cancel);
                ShowQr(login);

                var deadline = DateTimeOffset.Now + SessionTimeout;
                while (!cancel.IsCancellationRequested)
                {
                    // 先等一轮再查：刚生成的二维码不可能是确认状态
                    await Task.Delay(PollInterval, cancel);
                    if (DateTimeOffset.Now > deadline) break;

                    var result = await login.PollAsync(cancel);
                    switch (result.State)
                    {
                        case QrLoginState.Scanned:
                            SetStatus("已经扫到了，请在手机上点“确认登录”。");
                            break;
                        case QrLoginState.Confirmed:
                            await FinishAsync(login, result.Token, cancel);
                            return;
                        case QrLoginState.Expired:
                        case QrLoginState.Canceled:
                            // 服务端的二维码有过期时间，直接换一张，用户不用管
                            SetStatus("二维码失效了，正在换一张新的…");
                            await login.StartAsync(cancel);
                            ShowQr(login);
                            deadline = DateTimeOffset.Now + SessionTimeout;
                            break;
                    }
                }
                if (!cancel.IsCancellationRequested) Fail("等太久了，二维码可能已经过期，点下面的按钮换一张。");
            }
            catch (OperationCanceledException)
            {
                // 页面已经离开，正常
            }
            catch (MtopException ex)
            {
                Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Fail("登录过程出错了：" + ex.Message);
            }
        }

        private async Task FinishAsync(GoofishQrLogin login, string token, CancellationToken cancel)
        {
            SetStatus("已确认，正在完成登录…");

            if (!await login.FinishAsync(token, cancel))
            {
                // 只有 cookie2 而没有 unb 等于拿到一份看起来正常、其实没登录的凭据，不能当成功
                Fail("没有拿到完整的登录凭据，请重新扫码。");
                return;
            }

            try
            {
                // 顺便验证会话是否真的可用，并取回昵称和头像
                await StatusTray.RunAsync("正在验证登录状态...", () => AppServices.Account.RefreshProfileAsync());
            }
            catch (MtopException ex) when (ex.NeedsLogin)
            {
                Fail(ex.Message);
                return;
            }
            catch (Exception)
            {
                // 网络抖动等不影响登录本身，资料下次再取
            }

            SetStatus("登录成功。");
            AppServices.Account.NotifyChanged();
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private void ShowBusy(string text)
        {
            _symbol = null;
            SetStatus(text);
            QrImage.Source = null;
            BusyRing.IsActive = true;
            BusyRing.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Collapsed;
        }

        private void ShowQr(GoofishQrLogin login)
        {
            _symbol = QrEncoder.Encode(login.CodeContent);
            RenderQr();

            BusyRing.IsActive = false;
            BusyRing.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Collapsed;
            RequestDisplay();
            SetStatus("请用闲鱼 App 扫一扫，然后在手机上确认登录。");
        }

        /// <summary>
        /// 按当前可用宽度画二维码。位图像素按显示比例放大，再按 DIP 设回 1:1，
        /// 这样既有足够分辨率、边缘也不糊，同时不会超出屏幕。
        /// </summary>
        private void RenderQr()
        {
            if (_symbol == null) return;

            var scale = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0;
            var available = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : 360;
            var targetDip = Math.Max(QrMinDip, Math.Min(QrMaxDip, available - QrSideMarginDip));

            var total = _symbol.Size + QrBitmap.QuietZone * 2;
            var modulePixels = Math.Max(1, (int)Math.Round(targetDip * scale / total));

            var bitmap = QrBitmap.Render(_symbol.Modules, modulePixels);
            QrImage.Source = bitmap;
            QrImage.Width = bitmap.PixelWidth / scale;
            QrImage.Height = bitmap.PixelHeight / scale;
        }

        private void Fail(string message)
        {
            _symbol = null;
            BusyRing.IsActive = false;
            BusyRing.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Visible;
            ReleaseDisplay();
            SetStatus(message);
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        private void RequestDisplay()
        {
            if (_displayActive) return;
            try
            {
                _display.RequestActive();
                _displayActive = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保持屏幕常亮失败：" + ex.Message);
            }
        }

        private void ReleaseDisplay()
        {
            if (!_displayActive) return;
            try
            {
                _display.RequestRelease();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("恢复屏幕休眠失败：" + ex.Message);
            }
            _displayActive = false;
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            StartSession();
        }

        private async void CookieLogin_Click(object sender, RoutedEventArgs e)
        {
            var box = new TextBox
            {
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                Height = 120,
                PlaceholderText = "unb=...; cookie2=...; _m_h5_tk=..."
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "在电脑浏览器登录 goofish.com，打开开发者工具复制任意请求的 Cookie，粘贴到这里。至少需要 unb 和 cookie2。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(box);

            var dialog = new ContentDialog
            {
                Title = "用 Cookie 登录",
                Content = panel,
                PrimaryButtonText = "登录",
                SecondaryButtonText = "取消"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            if (AppServices.Account.ImportCookies(box.Text))
            {
                try
                {
                    await StatusTray.RunAsync("正在验证登录状态...", () => AppServices.Account.RefreshProfileAsync());
                }
                catch (Exception)
                {
                    // 资料取不到不影响登录本身
                }
                AppServices.Account.NotifyChanged();
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }
            await new MessageDialog("没有找到 unb 和 cookie2，请确认复制的是完整的 Cookie。", "登录失败").ShowAsync();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
