namespace SecretBase.Core.Music;

/// <summary>
/// Common track model for Music UI / Commands. Provider-specific payloads must map here.
/// ArtworkUrl is optional metadata only — widgets must not fetch arbitrary images without validation.
/// </summary>
public sealed class MusicTrack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? Album { get; set; }

    /// <summary>Optional https artwork URL from the provider. Never a file:// path.</summary>
    public string? ArtworkUrl { get; set; }

    /// <summary>Provider id that owns this track (e.g. demo-catalog).</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Human-readable source label (e.g. "Demo catalog").</summary>
    public string Source { get; set; } = string.Empty;
}
