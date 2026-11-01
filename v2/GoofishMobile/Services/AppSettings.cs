using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace Goofish.Services
{
    public sealed class AppSettings
    {
        private readonly ApplicationDataContainer _store = ApplicationData.Current.LocalSettings;

        public event EventHandler Changed;

        /// <summary>"system" / "dark" / "light"。强调色始终跟随系统，符合 Windows 10 Mobile 惯例。</summary>
        public string Theme
        {
            get { return Get("Theme", "system"); }
            set { Set("Theme", value); }
        }

        /// <summary>未登录时用演示数据浏览。</summary>
        public bool DemoMode
        {
            get { return Get("DemoMode", true); }
            set { Set("DemoMode", value); }
        }

        /// <summary>省流量：列表与详情只加载缩略图。</summary>
        public bool DataSaver
        {
            get { return Get("DataSaver", false); }
            set { Set("DataSaver", value); }
        }

        public bool Notifications
        {
            get { return Get("Notifications", true); }
            set { Set("Notifications", value); }
        }

        public string AccountNick
        {
            get { return Get<string>("AccountNick", null); }
            set { Set("AccountNick", value); }
        }

        public string AccountAvatar
        {
            get { return Get<string>("AccountAvatar", null); }
            set { Set("AccountAvatar", value); }
        }

        public string DeviceIdFor(string userId)
        {
            return Get<string>("DeviceId_" + userId, null);
        }

        public void SetDeviceId(string userId, string deviceId)
        {
            Set("DeviceId_" + userId, deviceId);
        }

        public IList<string> SearchHistory
        {
            get
            {
                var raw = Get("SearchHistory", string.Empty);
                return raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        public void AddSearchHistory(string keyword)
        {
            keyword = (keyword ?? string.Empty).Replace("\n", " ").Trim();
            if (keyword.Length == 0) return;
            var list = SearchHistory;
            list.Remove(keyword);
            list.Insert(0, keyword);
            Set("SearchHistory", string.Join("\n", list.Take(12)));
        }

        public void ClearSearchHistory()
        {
            Set("SearchHistory", string.Empty);
        }

        private T Get<T>(string key, T fallback)
        {
            object value;
            if (_store.Values.TryGetValue(key, out value) && value is T) return (T)value;
            return fallback;
        }

        private void Set<T>(string key, T value)
        {
            if (value == null) _store.Values.Remove(key);
            else _store.Values[key] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
