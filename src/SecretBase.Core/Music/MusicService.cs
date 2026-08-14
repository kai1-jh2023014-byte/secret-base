namespace SecretBase.Core.Music;

/// <summary>Resolves which <see cref="IMusicProvider"/> opens a <see cref="MusicSource"/>.</summary>
public sealed class MusicService
{
    private readonly IReadOnlyList<IMusicProvider> _providers;

    public MusicService(IEnumerable<IMusicProvider>? providers = null)
    {
        _providers = providers?.ToList()
            ??
            [
                OpenWebMusicProvider.Instance,
                LocalMusicProvider.Instance
            ];
    }

    public IReadOnlyList<IMusicProvider> Providers => _providers;

    public IMusicProvider? FindProvider(MusicSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _providers.FirstOrDefault(p => p.CanHandle(source.Type));
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
}
