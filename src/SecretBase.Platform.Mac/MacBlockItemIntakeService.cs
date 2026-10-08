using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Moves Desktop files into a per-Block folder. Never relocates .app bundles or /Applications.
/// </summary>
public sealed class MacBlockItemIntakeService : IBlockItemIntakeService
{
    private readonly string _rootDirectory;
    private readonly IReadOnlyList<string> _desktopRoots;
    private readonly IReadOnlyList<string> _protectedRoots;

    public MacBlockItemIntakeService(
        string? blockItemsDirectory = null,
        IReadOnlyList<string>? desktopRoots = null,
        IReadOnlyList<string>? protectedRoots = null)
    {
        _rootDirectory = blockItemsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase",
                "block-items");
        Directory.CreateDirectory(_rootDirectory);
        _desktopRoots = desktopRoots ?? DefaultDesktopRoots();
        _protectedRoots = protectedRoots ?? ["/Applications", "/System", "/Library"];
    }

    public string EnsureBlockFolder(Guid blockId, string? blockDisplayName) =>
        BlockItemStorage.EnsureBlockDirectory(_rootDirectory, blockId, blockDisplayName);

    public BlockItemIntakeResult TryIntake(
        string sourceAbsolutePath,
        Guid blockId,
        Guid itemId,
        string? blockDisplayName = null)
    {
        var moved = DesktopItemRelocator.TryHide(
            sourceAbsolutePath,
            blockId,
            itemId,
            _rootDirectory,
            _desktopRoots,
            _protectedRoots,
            blockDisplayName);
        return new BlockItemIntakeResult(
            moved.Succeeded,
            moved.TargetPath,
            moved.MovedFromSource,
            moved.ErrorMessage,
            moved.DesktopOriginPath);
    }

    public bool TryRestoreToDesktop(
        string currentPath,
        string? desktopOriginPath,
        out string restoredPath,
        out string? errorMessage)
    {
        var desktop = _desktopRoots.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? string.Empty;
        return DesktopItemRelocator.TryRestore(
            currentPath,
            desktopOriginPath,
            desktop,
            out restoredPath,
            out errorMessage);
    }

    internal static IReadOnlyList<string> DefaultDesktopRoots() =>
        new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToArray();
}
