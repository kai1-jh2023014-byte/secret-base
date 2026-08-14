namespace SecretBase.Core.Music;

/// <summary>
/// A named music source in the Music Widget / Hub.
/// Stores metadata only — never tokens, cookies, or credentials.
/// </summary>
public sealed class MusicSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public MusicSourceType Type { get; set; } = MusicSourceType.Web;

    public string Name { get; set; } = string.Empty;

    /// <summary>http(s) URL for web-backed sources. Null/empty for Local placeholder.</summary>
    public string? Url { get; set; }

    public bool IsEnabled { get; set; } = true;
}
