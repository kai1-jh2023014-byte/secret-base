using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Stores user/preset Block icons as PNG under %LocalAppData%\SecretBase\icons\custom.
/// Each import writes a new file name so WinUI does not keep showing a cached bitmap.
/// </summary>
public sealed class WindowsCustomIconService : ICustomIconService
{
    private const int OutputSize = 128;

    private readonly string _customDirectory;

    public WindowsCustomIconService(string? customIconsDirectory = null)
    {
        _customDirectory = customIconsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase",
                "icons",
                "custom");
        Directory.CreateDirectory(_customDirectory);
    }

    public string CustomIconsDirectory => _customDirectory;

    public bool IsUserIcon(string? path) =>
        BlockCustomIcons.IsCustomIconPath(path, _customDirectory);

    public bool TryImportImage(string sourcePath, Guid itemId, out string? storedPath, out string? errorMessage)
    {
        storedPath = null;
        errorMessage = null;

        if (itemId == Guid.Empty)
        {
            errorMessage = "Block item id is missing.";
            return false;
        }

        if (!BlockCustomIcons.IsAllowedImagePath(sourcePath))
        {
            errorMessage = "Choose a PNG, JPG, BMP, GIF, ICO, WEBP, or TIFF image.";
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            errorMessage = "The selected image was not found.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(_customDirectory);
            var dest = Path.Combine(_customDirectory, BlockCustomIcons.CreateStoredFileName(itemId));
            if (!TryWriteNormalizedPng(sourcePath, dest, out errorMessage))
            {
                return false;
            }

            storedPath = dest;
            return File.Exists(dest);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryCreatePresetIcon(
        Guid itemId,
        string presetId,
        string glyph,
        out string? storedPath,
        out string? errorMessage)
    {
        storedPath = null;
        errorMessage = null;
        if (itemId == Guid.Empty)
        {
            errorMessage = "Block item id is missing.";
            return false;
        }

        var preset = BlockCustomIcons.Presets.FirstOrDefault(p =>
            string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            errorMessage = "Unknown icon design.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(_customDirectory);
            var dest = Path.Combine(_customDirectory, BlockCustomIcons.CreateStoredFileName(itemId));
            using var bitmap = new Bitmap(OutputSize, OutputSize);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var fill = new SolidBrush(ParseHex(preset.HexColor));
                var pad = 4;
                var rect = new Rectangle(pad, pad, OutputSize - pad * 2, OutputSize - pad * 2);
                g.FillRoundedRectangle(fill, rect, 20);

                var letter = string.IsNullOrWhiteSpace(glyph) ? "?" : glyph.Trim()[..1].ToUpperInvariant();
                using var font = new Font("Segoe UI", 36f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var textBrush = new SolidBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(letter, font, textBrush, rect, format);
            }

            SavePngAtomic(bitmap, dest);
            storedPath = dest;
            return File.Exists(dest);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public void TryDeleteUserIcon(string? storedPath)
    {
        if (!IsUserIcon(storedPath) || string.IsNullOrWhiteSpace(storedPath))
        {
            return;
        }

        try
        {
            if (File.Exists(storedPath))
            {
                File.Delete(storedPath);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static bool TryWriteNormalizedPng(string sourcePath, string destPath, out string? errorMessage)
    {
        errorMessage = null;
        if (TryDecodeWithWindowsImaging(sourcePath, destPath, out errorMessage))
        {
            return true;
        }

        var imagingError = errorMessage;
        if (TryDecodeWithGdi(sourcePath, destPath, out errorMessage))
        {
            return true;
        }

        errorMessage = string.IsNullOrWhiteSpace(imagingError)
            ? errorMessage
            : imagingError + (string.IsNullOrWhiteSpace(errorMessage) ? string.Empty : " " + errorMessage);
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "Could not read that image.";
        }

        return false;
    }

    private static bool TryDecodeWithWindowsImaging(string sourcePath, string destPath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var bytes = File.ReadAllBytes(sourcePath);
            if (bytes.Length == 0)
            {
                errorMessage = "The selected image is empty.";
                return false;
            }

            using var input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(input))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                writer.DetachStream();
            }

            input.Seek(0);
            var decoder = BitmapDecoder.CreateAsync(input).AsTask().GetAwaiter().GetResult();
            using var software = decoder.GetSoftwareBitmapAsync().AsTask().GetAwaiter().GetResult();
            if (software is null)
            {
                errorMessage = "Could not decode that image.";
                return false;
            }

            using var converted = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetSoftwareBitmap(converted);
            encoder.BitmapTransform.ScaledWidth = OutputSize;
            encoder.BitmapTransform.ScaledHeight = OutputSize;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

            output.Seek(0);
            var png = new byte[output.Size];
            using (var reader = new DataReader(output))
            {
                reader.LoadAsync((uint)output.Size).AsTask().GetAwaiter().GetResult();
                reader.ReadBytes(png);
            }

            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = destPath + ".tmp";
            File.WriteAllBytes(tmp, png);
            File.Copy(tmp, destPath, overwrite: true);
            File.Delete(tmp);
            return File.Exists(destPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryDecodeWithGdi(string sourcePath, string destPath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            using var bitmap = LoadAsBitmap(sourcePath);
            if (bitmap is null)
            {
                errorMessage = "Could not read that image.";
                return false;
            }

            SavePngAtomic(bitmap, destPath);
            return File.Exists(destPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void SavePngAtomic(Bitmap bitmap, string destPath)
    {
        var tmp = destPath + ".tmp";
        bitmap.Save(tmp, ImageFormat.Png);
        File.Copy(tmp, destPath, overwrite: true);
        File.Delete(tmp);
    }

    private static Bitmap? LoadAsBitmap(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            return null;
        }

        var ext = Path.GetExtension(path);
        if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream(bytes);
            using var icon = new Icon(ms, OutputSize, OutputSize);
            using var source = icon.ToBitmap();
            return ResizeHighQuality(source, OutputSize);
        }

        using var stream = new MemoryStream(bytes);
        using var original = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return ResizeHighQuality(original, OutputSize);
    }

    private static Bitmap ResizeHighQuality(Image source, int size)
    {
        var dest = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dest);
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, size, size));
        return dest;
    }

    private static Color ParseHex(string hex)
    {
        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 8)
        {
            var a = Convert.ToByte(value[..2], 16);
            var r = Convert.ToByte(value[2..4], 16);
            var g = Convert.ToByte(value[4..6], 16);
            var b = Convert.ToByte(value[6..8], 16);
            return Color.FromArgb(a, r, g, b);
        }

        if (value.Length == 6)
        {
            var r = Convert.ToByte(value[..2], 16);
            var g = Convert.ToByte(value[2..4], 16);
            var b = Convert.ToByte(value[4..6], 16);
            return Color.FromArgb(255, r, g, b);
        }

        return Color.FromArgb(255, 47, 111, 237);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
