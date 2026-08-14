using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Music;

/// <summary>
/// Opens Spotify / YouTube / Web sources via validated http(s) URLs.
/// No Spotify/YouTube API, no OAuth, no credential handling.
/// </summary>
public sealed class OpenWebMusicProvider : IMusicProvider
{
    public static OpenWebMusicProvider Instance { get; } = new();

    public string ProviderId => "web-open";

    public string DisplayName => "Web Music";

    public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.OpenInWidget;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.NotApplicable;

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
}
