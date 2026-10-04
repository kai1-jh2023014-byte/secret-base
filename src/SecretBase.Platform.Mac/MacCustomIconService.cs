using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Copies raster images into AppData for custom Block icons.
/// Each import uses a unique file name so the UI does not keep a stale bitmap.
/// </summary>
public sealed class MacCustomIconService : ICustomIconService
{
    private readonly string _customDirectory;

    public MacCustomIconService(string? customIconsDirectory = null)
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
            errorMessage = "Choose a PNG, JPG, BMP, GIF, WEBP, or TIFF image.";
            return false;
        }

        if (Path.GetExtension(sourcePath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "ICO import is not available on this host. Use PNG or JPG.";
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
            var storedName = BlockCustomIcons.CreateStoredFileName(itemId);
            var ext = Path.GetExtension(sourcePath);
            if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ext))
            {
                storedName = Path.GetFileNameWithoutExtension(storedName) + ext.ToLowerInvariant();
            }

            var dest = Path.Combine(_customDirectory, storedName);

            var tmp = dest + ".tmp";
            File.Copy(sourcePath, tmp, overwrite: true);
            File.Copy(tmp, dest, overwrite: true);
            File.Delete(tmp);
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
        _ = glyph;
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

        if (!BlockIconPng.TryParseHex(preset.HexColor, out var a, out var r, out var g, out var b))
        {
            errorMessage = "Invalid icon design color.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(_customDirectory);
            var dest = Path.Combine(_customDirectory, BlockCustomIcons.CreateStoredFileName(itemId));
            if (!BlockIconPng.TryWriteSolidTile(dest, 96, a, r, g, b, out errorMessage))
            {
                return false;
            }

            storedPath = dest;
            return true;
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
            // Best-effort.
        }
    }
}
