namespace SecretBase.Core.Music;

/// <summary>Composes music providers for the Music Widget / Command service.</summary>
public sealed class MusicService
{
    private readonly IReadOnlyList<IMusicProvider> _providers;

    public MusicService(IEnumerable<IMusicProvider>? providers = null)
    {
        _providers = providers?.ToList()
            ??
            [
                new DemoCatalogMusicProvider(),
                OpenWebMusicProvider.Instance,
                LocalMusicProvider.Instance
            ];
    }

    public IReadOnlyList<IMusicProvider> Providers => _providers;

    public IMusicProvider? FindProvider(MusicSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _providers.FirstOrDefault(p => p.CanHandle(source.Type) && p is not DemoCatalogMusicProvider)
               ?? _providers.FirstOrDefault(p => p.CanHandle(source.Type));
    }

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        var provider = FindProvider(source);
        if (provider is null)
        {
            url = null;
            error = "No music provider handles this source.";
            return false;
        }

        return provider.TryResolveOpenUrl(source, out url, out error);
    }

    public MusicProviderCapabilities AggregateCapabilities()
    {
        MusicProviderCapabilities caps = MusicProviderCapabilities.None;
        foreach (var p in _providers)
        {
            caps |= p.Capabilities;
        }

        return caps;
    }

    public IMusicProvider? GetPlaybackProvider() =>
        _providers.FirstOrDefault(p =>
            p.Capabilities.HasFlag(MusicProviderCapabilities.Playback)
            && p.CurrentTrack is not null)
        ?? _providers.FirstOrDefault(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Playback));
}
