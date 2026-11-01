using System;
using System.Globalization;
using Goofish.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Goofish.Common
{
    /// <summary>bool / 非空字符串 / 非零数字 / 非 null → Visible。ConverterParameter="Invert" 取反。</summary>
    public sealed class VisibleWhenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool on;
            if (value is bool) on = (bool)value;
            else if (value is string) on = !string.IsNullOrWhiteSpace((string)value);
            else if (value is int) on = (int)value != 0;
            else if (value is decimal) on = (decimal)value != 0;
            else on = value != null;

            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) on = !on;
            return on ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 字符串 → BitmapImage。空地址返回 null，避免 Image 去解析空 Uri 报错；
    /// DecodePixelWidth 由 ConverterParameter 指定，列表缩略图只解码需要的尺寸以节省手机内存。
    /// </summary>
    public sealed class ImageUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var url = value as string;
            if (string.IsNullOrWhiteSpace(url)) return null;

            int width;
            var hasWidth = int.TryParse(parameter as string, out width) && width > 0;
            // 列表缩略图向 CDN 要小图；省流量模式下详情大图也只要中等尺寸
            var saver = AppServices.Settings.DataSaver;
            if (hasWidth) url = TextFormat.Thumbnail(url, saver ? 200 : 400);
            else if (saver) url = TextFormat.Thumbnail(url, 600);
            else url = TextFormat.NormalizeUrl(url);

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return null;

            var image = new BitmapImage();
            if (hasWidth)
            {
                image.DecodePixelType = DecodePixelType.Logical;
                image.DecodePixelWidth = width;
            }
            image.UriSource = uri;
            return image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>"#AARRGGBB" / "#RRGGBB" → SolidColorBrush，用于演示数据的占位色块与头像底色。</summary>
    public sealed class ColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var hex = (value as string ?? string.Empty).TrimStart('#');
            uint argb;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb))
                return new SolidColorBrush(Color.FromArgb(0xFF, 0x76, 0x76, 0x76));
            if (hex.Length == 6) argb |= 0xFF000000;
            return new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 随屏幕宽度变化的尺寸。推荐页双列方块的边长由 MainPage 根据窗口宽度计算后写进来，
    /// 模板通过 {Binding Source={StaticResource Layout}} 读取。
    /// </summary>
    public sealed class LayoutMetrics : BindableBase
    {
        private double _tileSize = 164;

        public double TileSize
        {
            get { return _tileSize; }
            set { SetProperty(ref _tileSize, value); }
        }
    }
}
