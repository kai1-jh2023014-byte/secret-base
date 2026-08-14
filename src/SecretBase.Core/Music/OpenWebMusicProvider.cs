using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Music;

/// <summary>
/// Opens Spotify / YouTube / Web sources via validated http(s) URLs.
/// No Spotify/YouTube API — Search/Playback unsupported.
/// </summary>
public sealed class OpenWebMusicProvider : IMusicProvider
{
    public static OpenWebMusicProvider Instance { get; } = new();

    public string ProviderId => "web-open";

    public string DisplayName => "Web Music";

    public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.OpenInWidget;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.NotApplicable;

    public MusicTrack? CurrentTrack => null;

    public bool IsPlaying => false;

    public bool CanHandle(MusicSourceType type) =>
        type is MusicSourceType.Spotify or MusicSourceType.YouTube or MusicSourceType.Web;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = null;
        ArgumentNullException.ThrowIfNull(source);

        if (!CanHandle(source.Type))
        {
            error = "This provider cannot open that source type.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.Url))
        {
            error = "This music source has no URL.";
            return false;
        }

        if (!WebUrlValidator.TryNormalize(source.Url, out var normalized, out var validationError))
        {
            error = validationError ?? WebUrlValidator.BlockedMessage;
            return false;
        }

        url = normalized;
        return true;
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<MusicTrack>>(
            new NotSupportedException("Web Music provider does not support search. Spotify/YouTube API is not connected."));

    public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Web Music provider does not support playback."));

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Web Music provider does not support pause."));

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Web Music provider does not support resume."));

    public Task NextAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Web Music provider does not support next."));

    public Task PreviousAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Web Music provider does not support previous."));
}
