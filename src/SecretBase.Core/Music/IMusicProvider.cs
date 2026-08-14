namespace SecretBase.Core.Music;

/// <summary>
/// Provider boundary for music. Unsupported operations must check
/// <see cref="Capabilities"/> — do not pretend Spotify/YouTube APIs work.
/// </summary>
public interface IMusicProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    MusicProviderCapabilities Capabilities { get; }

    MusicAuthStatus AuthStatus { get; }

    bool CanHandle(MusicSourceType type);

    MusicTrack? CurrentTrack { get; }

    bool IsPlaying { get; }

    /// <summary>
    /// Maps a source to a safe http(s) URL when <see cref="MusicProviderCapabilities.OpenInWidget"/> is set.
    /// </summary>
    bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error);

    Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task NextAsync(CancellationToken cancellationToken = default);

    Task PreviousAsync(CancellationToken cancellationToken = default);
}
