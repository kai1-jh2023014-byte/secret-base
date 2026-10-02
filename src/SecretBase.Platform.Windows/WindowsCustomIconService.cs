using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Stores user/preset Block icons as PNG under %LocalAppData%\SecretBase\icons\custom.
/// </summary>
public sealed class WindowsCustomIconService : ICustomIconService
{
    private const int OutputSize = 96;

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

        if (!BlockCustomIcons.IsAllowedImagePath(sourcePath))
        {
            errorMessage = "Choose a PNG, JPG, BMP, GIF, ICO, or WEBP image.";
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
            var dest = Path.Combine(_customDirectory, $"{itemId:N}.png");
            using var bitmap = LoadAsBitmap(sourcePath);
            if (bitmap is null)
            {
                errorMessage = "Could not read that image.";
                return false;
            }

            using var sized = new Bitmap(bitmap, OutputSize, OutputSize);
            sized.Save(dest, ImageFormat.Png);
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
            var dest = Path.Combine(_customDirectory, $"{itemId:N}.png");
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

            bitmap.Save(dest, ImageFormat.Png);
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

    private static Bitmap? LoadAsBitmap(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var icon = new Icon(path, OutputSize, OutputSize);
            return new Bitmap(icon.ToBitmap(), OutputSize, OutputSize);
        }

        using var original = Image.FromFile(path);
        return new Bitmap(original, OutputSize, OutputSize);
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
