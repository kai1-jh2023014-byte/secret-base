using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SecretBase.App.Desktop;

/// <summary>
/// Turns a full-color icon PNG into a white silhouette (alpha preserved) for fashion rails.
/// </summary>
internal static class IconSilhouettePainter
{
    public static Image? TryCreate(string path, double displaySize = 32)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var file = File.OpenRead(path);
            using var ras = file.AsRandomAccessStream();
            var decoder = BitmapDecoder.CreateAsync(ras).AsTask().GetAwaiter().GetResult();
            var pixelData = decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            var bytes = pixelData.DetachPixelData();
            for (var i = 0; i + 3 < bytes.Length; i += 4)
            {
                var alpha = bytes[i + 3];
                if (alpha < 12)
                {
                    bytes[i] = 0;
                    bytes[i + 1] = 0;
                    bytes[i + 2] = 0;
                    bytes[i + 3] = 0;
                    continue;
                }

                // White fill, keep source alpha for soft edges.
                bytes[i] = 255;
                bytes[i + 1] = 255;
                bytes[i + 2] = 255;
            }

            var bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
            using (var buffer = bitmap.PixelBuffer.AsStream())
            {
                buffer.Write(bytes, 0, bytes.Length);
            }

            return new Image
            {
                Source = bitmap,
                Width = displaySize,
                Height = displaySize,
                Stretch = Stretch.Uniform,
                Opacity = 0.92
            };
        }
        catch
        {
            return null;
        }
    }
}
