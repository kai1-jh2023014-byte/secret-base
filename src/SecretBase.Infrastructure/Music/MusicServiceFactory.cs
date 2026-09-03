using SecretBase.Core.Music;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Music;

/// <summary>Composes music providers for the desktop host.</summary>
public static class MusicServiceFactory
{
    public static MusicService Create(ISecureSecretStore secrets, Func<string, bool> openBrowser)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(openBrowser);

        SpotifyOAuthClientConfig.TryLoad(SpotifyOAuthClientConfig.DefaultClientConfigPath, out var spotifyClient, out _);
        var providers = new List<IMusicProvider>
        {
            new SpotifyMusicProvider(spotifyClient, secrets, openBrowser),
            new DemoCatalogMusicProvider(),
            OpenWebMusicProvider.Instance,
            LocalMusicProvider.Instance
        };
        return new MusicService(providers);
    }
}
