namespace SecretBase.Core.Blocks;

/// <summary>Result of hiding or linking a Desktop item. Platform maps this to intake APIs.</summary>
public readonly record struct DesktopRelocationResult(
    bool Succeeded,
    string TargetPath,
    bool MovedFromSource,
    string? ErrorMessage,
    string? DesktopOriginPath = null);

/// <summary>
/// Moves Desktop items into a per-Block folder under Secret Base storage and back.
/// Uses <see cref="File.Move"/> only — never deletes user files, never touches Program Files / Applications.
/// </summary>
public static class DesktopItemRelocator
{
    public static DesktopRelocationResult TryHide(
        string sourceAbsolutePath,
        Guid blockId,
        Guid itemId,
        string storageRoot,
        IReadOnlyList<string> desktopRoots,
        IReadOnlyList<string> protectedRoots,
        string? blockDisplayName = null)
    {
        _ = itemId; // retained for API stability / logging callers
        if (string.IsNullOrWhiteSpace(sourceAbsolutePath) || !HostPath.IsAbsolute(sourceAbsolutePath))
        {
            return new DesktopRelocationResult(false, string.Empty, false, "Source path is invalid.");
        }

        var source = Path.GetFullPath(sourceAbsolutePath.Trim());
        var isDirectory = Directory.Exists(source) && !HostPath.IsMacAppBundle(source);

        if (isDirectory || HostPath.IsMacAppBundle(source))
        {
            return new DesktopRelocationResult(true, source, false, null);
        }

        if (!File.Exists(source))
        {
            return new DesktopRelocationResult(false, string.Empty, false, "Source file was not found.");
        }

        if (!DesktopHidePolicy.ShouldHideFromDesktop(source, isDirectory: false, desktopRoots, protectedRoots))
        {
            return new DesktopRelocationResult(true, source, false, null);
        }

        try
        {
            var destDir = BlockItemStorage.EnsureBlockDirectory(storageRoot, blockId, blockDisplayName);
            var originalName = Path.GetFileName(source);
            if (string.IsNullOrWhiteSpace(originalName))
            {
                originalName = blockId.ToString("N");
            }

            var dest = UniqueDestination(Path.Combine(destDir, originalName));
            File.Move(source, dest);
            return new DesktopRelocationResult(
                true,
                dest,
                true,
                null,
                DesktopOriginPath: source);
        }
        catch (Exception ex)
        {
            return new DesktopRelocationResult(true, source, false, $"Move failed, linked instead: {ex.Message}");
        }
    }

    public static bool TryRestore(
        string currentPath,
        string? desktopOriginPath,
        string fallbackDesktopDirectory,
        out string restoredPath,
        out string? error)
    {
        restoredPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
        {
            error = "Stored item was not found.";
            return false;
        }

        var dest = desktopOriginPath;
        if (string.IsNullOrWhiteSpace(dest))
        {
            if (string.IsNullOrWhiteSpace(fallbackDesktopDirectory))
            {
                error = "Desktop folder is unavailable.";
                return false;
            }

            dest = Path.Combine(fallbackDesktopDirectory, Path.GetFileName(currentPath));
        }

        try
        {
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            dest = UniqueDestination(dest);
            File.Move(currentPath, dest);
            restoredPath = dest;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string UniqueDestination(string dest)
    {
        if (!File.Exists(dest) && !Directory.Exists(dest))
        {
            return dest;
        }

        var dir = Path.GetDirectoryName(dest) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (var i = 2; i < 100; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{name}-{Guid.NewGuid():N}{ext}");
    }
}
