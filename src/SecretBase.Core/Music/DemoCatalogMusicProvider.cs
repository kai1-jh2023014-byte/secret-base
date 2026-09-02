namespace SecretBase.Core.Music;

/// <summary>
/// In-memory demo catalog so Music UI / Commands work without Spotify/YouTube APIs.
/// Honest Source label: "Demo catalog" — not a fake Spotify integration.
/// </summary>
public sealed class DemoCatalogMusicProvider : IMusicProvider
{
    public const string Id = "demo-catalog";

    private readonly List<MusicTrack> _catalog;
    private readonly List<MusicTrack> _queue = [];
    private int _queueIndex = -1;
    private bool _isPlaying;

    public DemoCatalogMusicProvider()
    {
        _catalog =
        [
            Track("demo-lilac", "Lilac", "Mrs. GREEN APPLE", "Antenna"),
            Track("demo-dance", "ダンスホール", "Mrs. GREEN APPLE", "Unity"),
            Track("demo-ao", "青と夏", "Mrs. GREEN APPLE", "Ensemble"),
            Track("demo-infer", "Inferno", "Mrs. GREEN APPLE", "Attitude"),
            Track("demo-wante", "WanteD! WanteD!", "Mrs. GREEN APPLE", "Variety"),
            Track("demo-yoru", "夜に駆ける", "YOASOBI", "THE BOOK"),
            Track("demo-idol", "アイドル", "YOASOBI", "THE BOOK 3"),
            Track("demo-gunjo", "群青", "YOASOBI", "THE BOOK"),
            Track("demo-pretender", "Pretender", "Official髭男dism", "Traveler"),
            Track("demo-mixednuts", "ミックスナッツ", "Official髭男dism", "Editorial"),
            Track("demo-lemon", "Lemon", "米津玄師", "STRAY SHEEP"),
            Track("demo-kickback", "KICK BACK", "米津玄師", "KICK BACK")
        ];
    }

    public string ProviderId => Id;

    public string DisplayName => "Demo catalog";

    public MusicProviderCapabilities Capabilities =>
        MusicProviderCapabilities.Search
        | MusicProviderCapabilities.Playback
        | MusicProviderCapabilities.Pause
        | MusicProviderCapabilities.Resume
        | MusicProviderCapabilities.Next
        | MusicProviderCapabilities.Previous
        | MusicProviderCapabilities.NowPlaying;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.NotApplicable;

    public MusicTrack? CurrentTrack =>
        _queueIndex >= 0 && _queueIndex < _queue.Count ? Clone(_queue[_queueIndex]) : null;

    public bool IsPlaying => _isPlaying;

    public bool CanHandle(MusicSourceType type) => true;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = "Demo catalog does not open external web pages.";
        return false;
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var q = query.Trim();
        IReadOnlyList<MusicTrack> results = _catalog
            .Where(t =>
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (t.Album?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(Clone)
            .ToList();
        return Task.FromResult(results);
    }

    public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrWhiteSpace(track.Id) || string.IsNullOrWhiteSpace(track.Title))
        {
            throw new ArgumentException("Track is invalid.", nameof(track));
        }

        var owned = Clone(track);
        owned.ProviderId = ProviderId;
        if (string.IsNullOrWhiteSpace(owned.Source))
        {
            owned.Source = DisplayName;
        }

        _queue.Clear();
        _queue.Add(owned);
        // Keep related catalog neighbors as a tiny queue for Next/Previous.
        var siblings = _catalog
            .Where(t => !string.Equals(t.Id, owned.Id, StringComparison.Ordinal)
                        && string.Equals(t.Artist, owned.Artist, StringComparison.OrdinalIgnoreCase))
            .Select(Clone)
            .Take(4)
            .ToList();
        _queue.AddRange(siblings);
        _queueIndex = 0;
        _isPlaying = true;
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentTrack is null)
        {
            throw new InvalidOperationException("Nothing is playing.");
        }

        _isPlaying = false;
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentTrack is null)
        {
            throw new InvalidOperationException("Nothing to resume.");
        }

        _isPlaying = true;
        return Task.CompletedTask;
    }

    public Task NextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        _queueIndex = (_queueIndex + 1) % _queue.Count;
        _isPlaying = true;
        return Task.CompletedTask;
    }

    public Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        _queueIndex = (_queueIndex - 1 + _queue.Count) % _queue.Count;
        _isPlaying = true;
        return Task.CompletedTask;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static MusicTrack Track(string id, string title, string artist, string album) =>
        new()
        {
            Id = id,
            Title = title,
            Artist = artist,
            Album = album,
            ProviderId = Id,
            Source = "Demo catalog",
            ArtworkUrl = null
        };

    private static MusicTrack Clone(MusicTrack t) =>
        new()
        {
            Id = t.Id,
            Title = t.Title,
            Artist = t.Artist,
            Album = t.Album,
            ArtworkUrl = t.ArtworkUrl,
            ProviderId = string.IsNullOrWhiteSpace(t.ProviderId) ? Id : t.ProviderId,
            Source = string.IsNullOrWhiteSpace(t.Source) ? "Demo catalog" : t.Source
        };
}
