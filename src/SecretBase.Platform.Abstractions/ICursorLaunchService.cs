namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Narrow Cursor desktop launch API. Opens Cursor alone or with one absolute folder path.
/// Never runs PowerShell, never accepts free-form command lines, never elevates.
/// </summary>
public interface ICursorLaunchService
{
    /// <summary>True when a Cursor executable can be resolved (PATH or known install location).</summary>
    bool IsAvailable { get; }

    /// <summary>Resolved executable path when available; otherwise null.</summary>
    string? ResolvedExecutablePath { get; }

    CursorLaunchResult TryOpenApp();

    /// <summary>Opens Cursor with a single absolute folder path as the only argument.</summary>
    CursorLaunchResult TryOpenFolder(string absoluteFolderPath);
}

public sealed record CursorLaunchResult(
    bool Succeeded,
    string? ErrorMessage,
    bool OfferWebsiteFallback = false);
