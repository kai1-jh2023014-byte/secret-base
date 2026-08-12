namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Extracts the associated Windows shell icon for a user-chosen file/folder path
/// into a cached PNG. Documented shell icon APIs only — no Explorer mutation.
/// </summary>
public interface IFileIconService
{
    /// <summary>
    /// Returns an absolute path to a PNG icon for <paramref name="absoluteTargetPath"/>,
    /// or null if extraction failed.
    /// </summary>
    string? TryGetCachedIconPath(string absoluteTargetPath, int sizePx = 48);
}
