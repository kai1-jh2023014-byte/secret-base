namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Intakes a user-dropped file into a Block. Prefer <b>moving</b> Desktop files
/// (shortcuts and Desktop .exe) into Secret Base storage so the Desktop original is hidden.
/// Program Files / Applications stay as path references (never relocated). Restore returns hidden items.
/// </summary>
public interface IBlockItemIntakeService
{
    BlockItemIntakeResult TryIntake(string sourceAbsolutePath, Guid blockId, Guid itemId);

    /// <summary>
    /// Returns a previously hidden Desktop item. Never deletes the file if restore fails.
    /// </summary>
    bool TryRestoreToDesktop(
        string currentPath,
        string? desktopOriginPath,
        out string restoredPath,
        out string? errorMessage);
}

public sealed record BlockItemIntakeResult(
    bool Succeeded,
    string TargetPath,
    bool MovedFromSource,
    string? ErrorMessage,
    string? DesktopOriginPath = null);
