namespace SecretBase.Core.Music;

/// <summary>Auth readiness for a music provider (API phases later).</summary>
public enum MusicAuthStatus
{
    /// <summary>Provider does not use auth (web-open / local placeholder).</summary>
    NotApplicable = 0,

    NotConfigured = 1,
    Disconnected = 2,
    Connected = 3,
    Error = 4
}
