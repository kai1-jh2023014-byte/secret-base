namespace SecretBase.Core.Blocks;

/// <summary>
/// Rules for user-chosen Block icons. Shell-extracted cache paths are not "custom".
/// </summary>
public static class BlockCustomIcons
{
    public const string CustomFolderName = "custom";

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".jfif",
        ".bmp",
        ".gif",
        ".ico",
        ".webp",
        ".tif",
        ".tiff"
    };

    private static long _storedNameSequence;

    /// <summary>
    /// Unique PNG name per import. Reusing one path per item left the old BitmapImage
    /// cached and could fail when that file was still open.
    /// </summary>
    public static string CreateStoredFileName(Guid itemId) =>
        $"{itemId:N}_{DateTime.UtcNow.Ticks:x}_{Interlocked.Increment(ref _storedNameSequence):x}.png";

    public static readonly IReadOnlyList<BlockIconPreset> Presets =
    [
        new("ocean", "Ocean", "#FF2F6FED"),
        new("forest", "Forest", "#FF2F8F5B"),
        new("amber", "Amber", "#FFC9851A"),
        new("rose", "Rose", "#FFC44B6A"),
        new("violet", "Violet", "#FF7A5AF8"),
        new("slate", "Slate", "#FF5B6578")
    ];

    public static bool IsAllowedImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path.Trim());
        return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
    }

    public static bool IsCustomIconPath(string? path, string? customIconsDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(customIconsDirectory))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(customIconsDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string GlyphFor(BlockItemType type, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var ch = name.Trim()[0];
            if (char.IsLetterOrDigit(ch))
            {
                return char.ToUpperInvariant(ch).ToString();
            }
        }

        return type switch
        {
            BlockItemType.Folder => "D",
            BlockItemType.Shortcut => "L",
            BlockItemType.File => "F",
            _ => "A"
        };
    }
}

public sealed record BlockIconPreset(string Id, string DisplayName, string HexColor);
