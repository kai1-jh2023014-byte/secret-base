namespace SecretBase.Core.Music;

/// <summary>
/// Validates and routes <see cref="MusicCommand"/> to <see cref="IMusicProvider"/>.
/// Future AI must go through this layer — never providers or OS APIs directly.
/// </summary>
public sealed class MusicCommandService
{
    public const int MaxQueryLength = 200;

    private readonly MusicService _musicService;
    private readonly Dictionary<string, MusicTrack> _trackIndex = new(StringComparer.Ordinal);

    public MusicCommandService(MusicService musicService)
    {
        _musicService = musicService ?? throw new ArgumentNullException(nameof(musicService));
    }

    public MusicService MusicService => _musicService;

    public void RememberTracks(IEnumerable<MusicTrack> tracks)
    {
        foreach (var track in tracks)
        {
            if (!string.IsNullOrWhiteSpace(track.Id))
            {
                _trackIndex[track.Id] = track;
            }
        }
    }

    public async Task<MusicCommandResult> ExecuteAsync(
        MusicCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            MusicCommandKind.SearchTrack => await SearchAsync(command, cancellationToken).ConfigureAwait(false),
            MusicCommandKind.PlayTrack => await PlayAsync(command, cancellationToken).ConfigureAwait(false),
            MusicCommandKind.Pause => await TransportAsync(command, MusicProviderCapabilities.Pause, p => p.PauseAsync(cancellationToken)).ConfigureAwait(false),
            MusicCommandKind.Resume => await TransportAsync(command, MusicProviderCapabilities.Resume, p => p.ResumeAsync(cancellationToken)).ConfigureAwait(false),
            MusicCommandKind.Next => await TransportAsync(command, MusicProviderCapabilities.Next, p => p.NextAsync(cancellationToken)).ConfigureAwait(false),
            MusicCommandKind.Previous => await TransportAsync(command, MusicProviderCapabilities.Previous, p => p.PreviousAsync(cancellationToken)).ConfigureAwait(false),
            MusicCommandKind.GetPlaybackState => await GetPlaybackStateAsync(command, cancellationToken).ConfigureAwait(false),
            MusicCommandKind.ConnectProvider => await ConnectAsync(command, cancellationToken).ConfigureAwait(false),
            MusicCommandKind.DisconnectProvider => await DisconnectAsync(command, cancellationToken).ConfigureAwait(false),
            _ => MusicCommandResult.Fail(command.Kind, "Unknown music command.")
        };
    }

    private async Task<MusicCommandResult> SearchAsync(MusicCommand command, CancellationToken ct)
    {
        var query = command.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return MusicCommandResult.Fail(MusicCommandKind.SearchTrack, "Search query is empty.");
        }

        if (query.Length > MaxQueryLength)
        {
            return MusicCommandResult.Fail(MusicCommandKind.SearchTrack, "Search query is too long.");
        }

        // Reject path-like / scheme-like abuse in queries (commands are not URL openers).
        if (query.Contains("://", StringComparison.Ordinal)
            || query.Contains("..", StringComparison.Ordinal)
            || query.StartsWith('\\')
            || query.StartsWith('/'))
        {
            return MusicCommandResult.Fail(MusicCommandKind.SearchTrack, "Search query is not allowed.");
        }

        var providers = _musicService.Providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .Where(p => string.IsNullOrWhiteSpace(command.ProviderId)
                        || string.Equals(p.ProviderId, command.ProviderId, StringComparison.Ordinal))
            .ToList();

        if (providers.Count == 0)
        {
            return MusicCommandResult.Fail(
                MusicCommandKind.SearchTrack,
                "No music provider supports search yet.");
        }

        var merged = new List<MusicTrack>();
        string? lastError = null;
        var skippedDisconnected = 0;
        foreach (var provider in providers)
        {
            ct.ThrowIfCancellationRequested();
            if (provider.Capabilities.HasFlag(MusicProviderCapabilities.Authentication)
                && provider.AuthStatus is MusicAuthStatus.NotConfigured
                    or MusicAuthStatus.Disconnected
                    or MusicAuthStatus.Error)
            {
                skippedDisconnected++;
                continue;
            }

            try
            {
                var found = await provider.SearchAsync(query, ct).ConfigureAwait(false);
                merged.AddRange(found);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        if (merged.Count == 0)
        {
            if (skippedDisconnected > 0 && lastError is null)
            {
                return MusicCommandResult.Fail(
                    MusicCommandKind.SearchTrack,
                    "Connect a music provider to search that catalog.");
            }

            if (lastError is not null)
            {
                SpotifyWebSearch.TryCreateSearchUrl(query, out var webSearchUrl, out _);
                return MusicCommandResult.Fail(MusicCommandKind.SearchTrack, lastError, webSearchUrl);
            }
        }

        RememberTracks(merged);
        return MusicCommandResult.Ok(MusicCommandKind.SearchTrack, merged);
    }

    private async Task<MusicCommandResult> PlayAsync(MusicCommand command, CancellationToken ct)
    {
        MusicTrack? track = command.Track;
        if (track is null && !string.IsNullOrWhiteSpace(command.TrackId))
        {
            _trackIndex.TryGetValue(command.TrackId!, out track);
        }

        if (track is null || string.IsNullOrWhiteSpace(track.Id) || string.IsNullOrWhiteSpace(track.Title))
        {
            SpotifyWebSearch.TryCreateOpenUrl(null, command.Query, out var missingUrl, out _);
            return MusicCommandResult.Fail(
                MusicCommandKind.PlayTrack,
                "Track is missing or invalid.",
                missingUrl);
        }

        var provider = ResolveProvider(command.ProviderId ?? track.ProviderId, MusicProviderCapabilities.Playback);
        if (provider is null)
        {
            SpotifyWebSearch.TryCreateOpenUrl(track, command.Query, out var noPlaybackUrl, out _);
            return MusicCommandResult.Fail(
                MusicCommandKind.PlayTrack,
                "No music provider supports playback for this track.",
                noPlaybackUrl);
        }

        try
        {
            await provider.PlayAsync(track, ct).ConfigureAwait(false);
            RememberTracks([track]);
            return MusicCommandResult.Ok(
                MusicCommandKind.PlayTrack,
                current: provider.CurrentTrack ?? track,
                isPlaying: provider.IsPlaying);
        }
        catch (Exception ex)
        {
            // Free / non-Premium accounts often fail Web API playback (403 PREMIUM_REQUIRED).
            SpotifyWebSearch.TryCreateOpenUrl(track, command.Query, out var openUrl, out _);
            return MusicCommandResult.Fail(MusicCommandKind.PlayTrack, ex.Message, openUrl);
        }
    }

    private async Task<MusicCommandResult> TransportAsync(
        MusicCommand command,
        MusicProviderCapabilities required,
        Func<IMusicProvider, Task> action)
    {
        var provider = ResolveProvider(command.ProviderId, required)
                       ?? _musicService.Providers.FirstOrDefault(p =>
                           p.Capabilities.HasFlag(required)
                           && p.CurrentTrack is not null);

        if (provider is null)
        {
            return MusicCommandResult.Fail(command.Kind, $"No music provider supports {command.Kind}.");
        }

        try
        {
            await action(provider).ConfigureAwait(false);
            return MusicCommandResult.Ok(
                command.Kind,
                current: provider.CurrentTrack,
                isPlaying: provider.IsPlaying);
        }
        catch (NotSupportedException ex)
        {
            return MusicCommandResult.Fail(command.Kind, ex.Message);
        }
        catch (Exception ex)
        {
            return MusicCommandResult.Fail(command.Kind, ex.Message);
        }
    }

    private IMusicProvider? ResolveProvider(string? providerId, MusicProviderCapabilities required)
    {
        IEnumerable<IMusicProvider> candidates = _musicService.Providers
            .Where(p => p.Capabilities.HasFlag(required));

        if (!string.IsNullOrWhiteSpace(providerId))
        {
            candidates = candidates.Where(p =>
                string.Equals(p.ProviderId, providerId, StringComparison.Ordinal));
        }

        return candidates.FirstOrDefault();
    }

    private async Task<MusicCommandResult> GetPlaybackStateAsync(MusicCommand command, CancellationToken ct)
    {
        var provider = ResolveProvider(command.ProviderId, MusicProviderCapabilities.NowPlaying)
                       ?? _musicService.Providers.FirstOrDefault(p =>
                           p.Capabilities.HasFlag(MusicProviderCapabilities.NowPlaying)
                           && p.AuthStatus == MusicAuthStatus.Connected);

        if (provider is null)
        {
            return MusicCommandResult.Fail(MusicCommandKind.GetPlaybackState, "No connected music provider.");
        }

        try
        {
            await provider.RefreshPlaybackStateAsync(ct).ConfigureAwait(false);
            var track = provider.CurrentTrack;
            return MusicCommandResult.Ok(
                MusicCommandKind.GetPlaybackState,
                current: track,
                isPlaying: provider.IsPlaying,
                progressMs: track?.ProgressMilliseconds,
                durationMs: track?.DurationMilliseconds,
                providerId: provider.ProviderId,
                authStatus: provider.AuthStatus);
        }
        catch (Exception ex)
        {
            return MusicCommandResult.Fail(MusicCommandKind.GetPlaybackState, ex.Message);
        }
    }

    private async Task<MusicCommandResult> ConnectAsync(MusicCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ProviderId))
        {
            return MusicCommandResult.Fail(MusicCommandKind.ConnectProvider, "Provider id is required.");
        }

        var provider = _musicService.Providers.FirstOrDefault(p =>
            string.Equals(p.ProviderId, command.ProviderId, StringComparison.Ordinal));
        if (provider is null)
        {
            return MusicCommandResult.Fail(MusicCommandKind.ConnectProvider, "Music provider was not found.");
        }

        if (!string.IsNullOrWhiteSpace(command.ClientId))
        {
            if (provider is not IMusicOAuthClientSource source)
            {
                return MusicCommandResult.Fail(
                    MusicCommandKind.ConnectProvider,
                    "This music provider does not accept a client id.");
            }

            try
            {
                source.RememberClient(command.ClientId, command.ClientSecret);
            }
            catch (Exception ex)
            {
                return MusicCommandResult.Fail(MusicCommandKind.ConnectProvider, ex.Message);
            }
        }

        try
        {
            await provider.ConnectAsync(ct).ConfigureAwait(false);
            return MusicCommandResult.Ok(
                MusicCommandKind.ConnectProvider,
                providerId: provider.ProviderId,
                authStatus: provider.AuthStatus);
        }
        catch (Exception ex)
        {
            return MusicCommandResult.Fail(MusicCommandKind.ConnectProvider, ex.Message);
        }
    }

    private async Task<MusicCommandResult> DisconnectAsync(MusicCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ProviderId))
        {
            return MusicCommandResult.Fail(MusicCommandKind.DisconnectProvider, "Provider id is required.");
        }

        var provider = _musicService.Providers.FirstOrDefault(p =>
            string.Equals(p.ProviderId, command.ProviderId, StringComparison.Ordinal));
        if (provider is null)
        {
            return MusicCommandResult.Fail(MusicCommandKind.DisconnectProvider, "Music provider was not found.");
        }

        try
        {
            await provider.DisconnectAsync(ct).ConfigureAwait(false);
            return MusicCommandResult.Ok(
                MusicCommandKind.DisconnectProvider,
                providerId: provider.ProviderId,
                authStatus: provider.AuthStatus);
        }
        catch (Exception ex)
        {
            return MusicCommandResult.Fail(MusicCommandKind.DisconnectProvider, ex.Message);
        }
    }
}
