using SecretBase.Core;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Moves Desktop aliases / .webloc files into Secret Base storage. Never relocates .app bundles
/// or Applications binaries. Does not mutate Finder internals.
/// </summary>
public sealed class MacBlockItemIntakeService : IBlockItemIntakeService
{
    private readonly string _rootDirectory;

    public MacBlockItemIntakeService(string? blockItemsDirectory = null)
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
        if (string.IsNullOrWhiteSpace(sourceAbsolutePath) || !HostPath.IsAbsolute(sourceAbsolutePath))
        {
            return new BlockItemIntakeResult(false, string.Empty, false, "Source path is invalid.");
        }

        var source = Path.GetFullPath(sourceAbsolutePath.Trim());

        if (HostPath.IsMacAppBundle(source) || Directory.Exists(source))
        {
            return new BlockItemIntakeResult(true, source, false, null);
        }

        if (!File.Exists(source))
        {
            return new BlockItemIntakeResult(false, string.Empty, false, "Source file was not found.");
        }

        var ext = HostPath.GetExtension(source);
        var isShortcut = ext.Equals(".webloc", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
        var onDesktop = IsUnderUserDesktop(source);

        if (!isShortcut && !onDesktop)
        {
            return new BlockItemIntakeResult(true, source, false, null);
        }

        try
        {
            var destDir = Path.Combine(_rootDirectory, blockId.ToString("N"));
            Directory.CreateDirectory(destDir);
            var destName = itemId.ToString("N") + (string.IsNullOrEmpty(ext) ? ".webloc" : ext);
            var dest = Path.Combine(destDir, destName);

            if (File.Exists(dest))
            {
                File.Delete(dest);
            }

            File.Move(source, dest);
            return new BlockItemIntakeResult(true, dest, true, null);
        }
        catch (Exception ex)
        {
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
            if (fullPath.StartsWith(root, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
