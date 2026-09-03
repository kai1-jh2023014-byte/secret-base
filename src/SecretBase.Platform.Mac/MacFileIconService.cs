using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Icon extraction for macOS is deferred (NSWorkspace) — Blocks still launch by path.
/// Returning null is honest; the host shows a fallback glyph.
/// </summary>
public sealed class MacFileIconService : IFileIconService
{
    public string? TryGetCachedIconPath(string absoluteTargetPath, int sizePx = 48)
    {
        _ = absoluteTargetPath;
        _ = sizePx;
        return null;
    }
}
