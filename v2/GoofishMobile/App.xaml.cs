using System;
using Goofish.Models;
using Goofish.Services;
using Goofish.ViewModels;
using Goofish.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Goofish
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
        }

        public static Shell Shell { get; private set; }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                await LaunchAsync(e);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                throw;
            }
        }

        private async System.Threading.Tasks.Task LaunchAsync(LaunchActivatedEventArgs e)
        {
            if (Shell == null)
            {
                // 桌面上以手机尺寸的窄窗口启动，方便在 PC 上调试；手机上这些设置不起作用
                var view = ApplicationView.GetForCurrentView();
                view.SetPreferredMinSize(new Size(320, 500));
                ApplicationView.PreferredLaunchViewSize = new Size(400, 720);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

                await AppServices.Favorites.LoadAsync();
                Shell = new Shell();
                Window.Current.Content = Shell;
                AppServices.Demo.MessageReceived += OnChatMessage;
                AppServices.Im.MessageReceived += OnChatMessage;
            }

            if (Shell.Frame.Content == null)
            {
                Shell.Frame.Navigate(typeof(MainPage));
            }

            // 从新私信 Toast 启动：直接打开对应会话
            var args = e.Arguments ?? string.Empty;
            if (args.StartsWith("cid=", StringComparison.Ordinal))
            {
                Shell.Frame.Navigate(typeof(ChatPage), new ChatArgs { Cid = args.Substring(4) });
            }

            Window.Current.Activate();
        }

        /// <summary>
        /// 收到私信：当前正在看这个会话就不打扰，否则弹系统横幅。
        /// 真实 IM 的回调在后台线程，切回 UI 线程再处理。
        /// </summary>
        private async void OnChatMessage(object sender, ChatMessage message)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (message.Cid == AppServices.ActiveConversationId) return;
                if (!AppServices.Settings.Notifications) return;
                ToastService.ShowMessage(message.SenderName ?? "新消息", message.Text, message.Cid);
            });
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // 挂起后系统会冻结网络，长连接迟早被服务端踢掉，主动断开，回到前台时按需重连
            AppServices.Im.Disconnect();
            deferral.Complete();
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception);
            if (e.Exception is MtopException) e.Handled = true;
        }

        /// <summary>
        /// 手机上没法挂调试器时，崩溃原因写到 LocalState\crash.log，
        /// 可以通过 Windows Device Portal 的文件浏览器取回。
        /// </summary>
        internal static void WriteCrashLog(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("未处理的异常：" + ex);
            try
            {
                var path = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "crash.log");
                System.IO.File.AppendAllText(path, DateTimeOffset.Now.ToString("u") + "\r\n" + ex + "\r\n\r\n");
            }
            catch (Exception)
            {
                // 写日志失败时没有更多可做的
            }
        }
    }
}
