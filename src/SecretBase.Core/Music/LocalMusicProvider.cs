namespace SecretBase.Core.Music;

/// <summary>
/// Local music placeholder. No filesystem scanning, no player yet.
/// </summary>
public sealed class LocalMusicProvider : IMusicProvider
{
    public static LocalMusicProvider Instance { get; } = new();

    public string ProviderId => "local";

    public string DisplayName => "Local";

    public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.None;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.NotApplicable;

    public MusicTrack? CurrentTrack => null;

    public bool IsPlaying => false;

    public bool CanHandle(MusicSourceType type) => type == MusicSourceType.Local;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = "Local music player is not available yet. A future Local Music Provider will use user-selected folders only — never unrestricted scanning.";
        return false;
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<MusicTrack>>(
            new NotSupportedException("Local music search is not available yet."));

    public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Local music playback is not available yet."));

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Local music pause is not available yet."));

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Local music resume is not available yet."));

    public Task NextAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Local music next is not available yet."));

    public Task PreviousAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Local music previous is not available yet."));
}
