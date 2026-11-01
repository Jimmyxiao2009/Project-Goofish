using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;

namespace Goofish.Services
{
    /// <summary>
    /// 加载提示。手机上用系统托盘的 StatusBar.ProgressIndicator（W10M 应用的标准做法），
    /// 桌面上没有托盘，改由 Shell 顶部的细进度条显示。
    /// </summary>
    public static class StatusTray
    {
        public static readonly bool HasStatusBar = ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar");
        private static int _count;
        private static string _text;

        /// <summary>桌面回退用：true 表示有任务在加载。</summary>
        public static event EventHandler<bool> BusyChanged;

        public static async Task<T> RunAsync<T>(string text, Func<Task<T>> work)
        {
            Begin(text);
            try
            {
                return await work();
            }
            finally
            {
                End();
            }
        }

        public static async Task RunAsync(string text, Func<Task> work)
        {
            Begin(text);
            try
            {
                await work();
            }
            finally
            {
                End();
            }
        }

        public static void Begin(string text)
        {
            _count++;
            _text = text;
            Update();
        }

        public static void End()
        {
            _count = Math.Max(0, _count - 1);
            Update();
        }

        /// <summary>按页面主题设置托盘前景色，避免浅色主题下白字看不见。</summary>
        public static void ApplyTheme(bool dark)
        {
            if (HasStatusBar) PhoneTray.ApplyTheme(dark);
        }

        private static void Update()
        {
            var busy = _count > 0;
            BusyChanged?.Invoke(null, busy);
            if (HasStatusBar) PhoneTray.Show(busy, _text);
        }

        /// <summary>
        /// 所有直接引用 StatusBar 的代码都放在这里且禁止内联：
        /// 桌面系统没有这个类型，JIT 一编译到引用它的方法就会抛 TypeLoadException，
        /// 只靠方法内部的 ApiInformation 判断挡不住。
        /// </summary>
        private static class PhoneTray
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void ApplyTheme(bool dark)
            {
                var bar = StatusBar.GetForCurrentView();
                bar.ForegroundColor = dark ? Windows.UI.Colors.White : Windows.UI.Colors.Black;
                bar.BackgroundOpacity = 0;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async void Show(bool busy, string text)
            {
                try
                {
                    var indicator = StatusBar.GetForCurrentView().ProgressIndicator;
                    if (busy)
                    {
                        indicator.Text = text ?? "正在加载...";
                        indicator.ProgressValue = null;
                        await indicator.ShowAsync();
                    }
                    else
                    {
                        await indicator.HideAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("StatusBar 更新失败：" + ex.Message);
                }
            }
        }
    }
}
