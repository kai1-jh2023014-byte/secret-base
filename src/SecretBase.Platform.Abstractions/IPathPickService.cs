namespace SecretBase.Platform.Abstractions;

/// <summary>
/// OS file/folder pickers for user-explicit registration (no custom explorer UI).
/// </summary>
public interface IPathPickService
{
    /// <summary>Associates the picker with the host window (Win32 HWND).</summary>
    void SetOwnerWindow(nint windowHandle);

    Task<PathPickResult> PickFileAsync();

    Task<PathPickResult> PickFolderAsync();

    /// <summary>Image picker for Block / widget custom icons (png, jpg, bmp, gif, ico, webp, tiff).</summary>
    Task<PathPickResult> PickImageAsync();
}

public sealed record PathPickResult(
    bool Succeeded,
    bool Cancelled,
    string? Path,
    bool IsDirectory,
    string? ErrorMessage);
