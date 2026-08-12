using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Moves Desktop shortcuts into %LocalAppData%\SecretBase\block-items so the real
/// icon leaves the Desktop. Does not relocate Program Files binaries or folders.
/// </summary>
public sealed class BlockItemIntakeService : IBlockItemIntakeService
{
    private readonly string _rootDirectory;

    public BlockItemIntakeService(string? blockItemsDirectory = null)
    {
        _rootDirectory = blockItemsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase",
                "block-items");
        Directory.CreateDirectory(_rootDirectory);
    }

    public BlockItemIntakeResult TryIntake(string sourceAbsolutePath, Guid blockId, Guid itemId)
    {
        if (string.IsNullOrWhiteSpace(sourceAbsolutePath) || !Path.IsPathRooted(sourceAbsolutePath))
        {
            return new BlockItemIntakeResult(false, string.Empty, false, "Source path is invalid.");
        }

        var source = Path.GetFullPath(sourceAbsolutePath.Trim());

        if (Directory.Exists(source))
        {
            // Folders stay as references — never relocate a directory tree.
            return new BlockItemIntakeResult(true, source, false, null);
        }

        if (!File.Exists(source))
        {
            return new BlockItemIntakeResult(false, string.Empty, false, "Source file was not found.");
        }

        var ext = Path.GetExtension(source);
        var isShortcut = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
        var onDesktop = IsUnderUserDesktop(source);

        // Only move shortcuts, or other files that live on the user Desktop.
        // Never move installed .exe from Program Files / elsewhere (keep path reference).
        if (!isShortcut && !onDesktop)
        {
            return new BlockItemIntakeResult(true, source, false, null);
        }

        if (!isShortcut && onDesktop && ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Rare Desktop .exe: prefer reference over moving binaries.
            return new BlockItemIntakeResult(true, source, false, null);
        }

        try
        {
            var destDir = Path.Combine(_rootDirectory, blockId.ToString("N"));
            Directory.CreateDirectory(destDir);
            var destName = itemId.ToString("N") + (string.IsNullOrEmpty(ext) ? ".lnk" : ext);
            var dest = Path.Combine(destDir, destName);

            if (File.Exists(dest))
            {
                File.Delete(dest);
            }

            // Documented File.Move — relocates the Desktop .lnk so Explorer no longer shows a duplicate.
            File.Move(source, dest);
            return new BlockItemIntakeResult(true, dest, true, null);
        }
        catch (Exception ex)
        {
            // Fall back to referencing the original path if move fails (e.g. locked file).
            return new BlockItemIntakeResult(true, source, false, $"Move failed, linked instead: {ex.Message}");
        }
    }

    private static bool IsUnderUserDesktop(string fullPath)
    {
        foreach (var desktop in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                     Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                 })
        {
            if (string.IsNullOrWhiteSpace(desktop))
            {
                continue;
            }

            var root = Path.GetFullPath(desktop)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
