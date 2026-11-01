using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Media.Imaging;

namespace Goofish.Common
{
    /// <summary>把二维码矩阵画成位图。</summary>
    public static class QrBitmap
    {
        /// <summary>标准要求的最小静区宽度（模块数）。</summary>
        public const int QuietZone = 4;

        /// <summary>
        /// 画二维码。浅色用不透明白色：深色主题下背景是黑的，留透明的二维码扫不出来。
        /// </summary>
        /// <param name="modules">模块矩阵，下标 [行, 列]。</param>
        /// <param name="modulePixels">每个模块占多少像素，至少 1。</param>
        public static WriteableBitmap Render(bool[,] modules, int modulePixels)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            if (modulePixels < 1) modulePixels = 1;

            var count = modules.GetLength(0);
            var side = (count + QuietZone * 2) * modulePixels;
            var pixels = new byte[side * side * 4];

            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xFF;
                pixels[i + 1] = 0xFF;
                pixels[i + 2] = 0xFF;
                pixels[i + 3] = 0xFF;
            }

            for (var y = 0; y < count; y++)
            {
                for (var x = 0; x < count; x++)
                {
                    if (!modules[y, x]) continue;
                    var top = (y + QuietZone) * modulePixels;
                    var left = (x + QuietZone) * modulePixels;
                    for (var dy = 0; dy < modulePixels; dy++)
                    {
                        var rowStart = ((top + dy) * side + left) * 4;
                        for (var dx = 0; dx < modulePixels; dx++)
                        {
                            var offset = rowStart + dx * 4;
                            pixels[offset] = 0;
                            pixels[offset + 1] = 0;
                            pixels[offset + 2] = 0;
                        }
                    }
                }
            }

            var bitmap = new WriteableBitmap(side, side);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                stream.Write(pixels, 0, pixels.Length);
            }
            bitmap.Invalidate();
            return bitmap;
        }
    }
}
