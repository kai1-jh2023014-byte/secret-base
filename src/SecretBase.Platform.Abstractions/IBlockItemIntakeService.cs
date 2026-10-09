namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Intakes a user-dropped file into a Block. Prefer <b>moving</b> Desktop files
/// into that Block's folder under Secret Base storage (original names kept).
/// Program Files / Applications stay as path references (never relocated).
/// </summary>
public interface IBlockItemIntakeService
{
    BlockItemIntakeResult TryIntake(
        string sourceAbsolutePath,
        Guid blockId,
        Guid itemId,
        string? blockDisplayName = null);

    /// <summary>
    /// Returns a previously hidden Desktop item. Never deletes the file if restore fails.
    /// </summary>
    bool TryRestoreToDesktop(
        string currentPath,
        string? desktopOriginPath,
        out string restoredPath,
        out string? errorMessage);

    /// <summary>Absolute path of the per-Block storage folder (created if needed).</summary>
    string EnsureBlockFolder(Guid blockId, string? blockDisplayName);
}

public sealed record BlockItemIntakeResult(
    bool Succeeded,
    string TargetPath,
    bool MovedFromSource,
    string? ErrorMessage,
    string? DesktopOriginPath = null);
