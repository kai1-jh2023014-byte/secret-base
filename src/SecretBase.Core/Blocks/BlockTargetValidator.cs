namespace SecretBase.Core.Blocks;

/// <summary>
/// Validates Block item targets without OS APIs. Launch still goes through Platform.
/// </summary>
public static class BlockTargetValidator
{
    private static readonly char[] ForbiddenChars = ['|', '&', '>', '<', '^', '\n', '\r', '\0', '"'];

    public static bool TryValidate(string? target, BlockItemType type, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "Target is empty.";
            return false;
        }

        var trimmed = target.Trim();
        if (trimmed.Length > 1024)
        {
            error = "Target is too long.";
            return false;
        }

        if (trimmed.IndexOfAny(ForbiddenChars) >= 0)
        {
            error = "Target contains forbidden characters.";
            return false;
        }

        // Reject obvious "program.exe /arg" command lines — V1 is path-only.
        foreach (var marker in new[] { ".exe ", ".cmd ", ".bat ", ".com ", ".lnk ", ".app " })
        {
            var index = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index + marker.Length < trimmed.Length)
            {
                error = "Command arguments are not allowed; choose a single path.";
                return false;
            }
        }

        if (!HostPath.IsAbsolute(trimmed))
        {
            error = "Target must be an absolute path.";
            return false;
        }

        normalized = HostPath.NormalizeSeparators(trimmed);
        _ = type;
        return true;
    }

    public static BlockItemType InferType(string absolutePath, bool isDirectory)
    {
        if (HostPath.IsMacAppBundle(absolutePath))
        {
            return BlockItemType.Application;
        }

        if (isDirectory)
        {
            return BlockItemType.Folder;
        }

        var ext = HostPath.GetExtension(absolutePath);
        if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webloc", StringComparison.OrdinalIgnoreCase))
        {
            return BlockItemType.Shortcut;
        }

        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".com", StringComparison.OrdinalIgnoreCase))
        {
            return BlockItemType.Application;
        }

        return BlockItemType.File;
    }

    public static string InferDisplayName(string absolutePath)
    {
        var name = HostPath.GetFileName(absolutePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Item";
        }

        var ext = HostPath.GetExtension(name);
        if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webloc", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".app", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^ext.Length];
        }

        return name;
    }
}
