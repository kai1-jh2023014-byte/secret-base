using Windows.Media.Control;
using Windows.Storage.Streams;
using SecretBase.Core.Music;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Reads the current Windows media session (SMTC), the same surface FluentFlyout uses.
/// Does not replace the taskbar, inject into a player, or call the Spotify Web API.
/// </summary>
public sealed class WindowsSystemNowPlayingSource : ISystemNowPlayingSource
{
    public const int MaxArtworkBytes = 1_500_000;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string? _artworkKey;
    private byte[]? _artwork;

    public async Task<SystemNowPlaying?> ReadCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var session = await CurrentSessionAsync();
            if (session is null)
            {
                return null;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            if (properties is null || string.IsNullOrWhiteSpace(properties.Title))
            {
                return null;
            }

            var playback = session.GetPlaybackInfo();
            var position = TimeSpan.Zero;
            var duration = TimeSpan.Zero;
            try
            {
                var timeline = session.GetTimelineProperties();
                position = timeline.Position < TimeSpan.Zero ? TimeSpan.Zero : timeline.Position;
                var length = timeline.EndTime - timeline.StartTime;
                duration = length < TimeSpan.Zero ? TimeSpan.Zero : length;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                position = TimeSpan.Zero;
                duration = TimeSpan.Zero;
            }

            var sourceId = string.Empty;
            try
            {
                sourceId = session.SourceAppUserModelId ?? string.Empty;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sourceId = string.Empty;
            }

            var artwork = await TryReadArtworkAsync(properties, sourceId, cancellationToken);
            return new SystemNowPlaying
            {
                Title = properties.Title.Trim(),
                Artist = properties.Artist?.Trim() ?? string.Empty,
                Album = string.IsNullOrWhiteSpace(properties.AlbumTitle) ? null : properties.AlbumTitle.Trim(),
                SourceAppId = sourceId,
                IsPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                PositionMilliseconds = (long)position.TotalMilliseconds,
                DurationMilliseconds = (long)duration.TotalMilliseconds,
                Artwork = artwork
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _manager = null;
            return null;
        }
    }

    public Task<bool> TryTogglePlayPauseAsync(CancellationToken cancellationToken = default) =>
        TrySessionAsync(async session => await session.TryTogglePlayPauseAsync(), cancellationToken);

    public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) =>
        TrySessionAsync(async session => await session.TrySkipNextAsync(), cancellationToken);

    public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) =>
        TrySessionAsync(async session => await session.TrySkipPreviousAsync(), cancellationToken);

    private async Task<bool> TrySessionAsync(
        Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var session = await CurrentSessionAsync();
            if (session is null)
            {
                return false;
            }

            return await action(session);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _manager = null;
            return false;
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> CurrentSessionAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return _manager.GetCurrentSession();
    }

    private async Task<byte[]?> TryReadArtworkAsync(
        GlobalSystemMediaTransportControlsSessionMediaProperties properties,
        string sourceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = sourceId + "\n" + properties.Title + "\n" + properties.Artist;
        if (string.Equals(key, _artworkKey, StringComparison.Ordinal))
        {
            return _artwork;
        }

        _artworkKey = key;
        _artwork = null;
        if (properties.Thumbnail is null)
        {
            return null;
        }

        try
        {
            using var stream = await properties.Thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > MaxArtworkBytes)
            {
                return null;
            }

            var length = (int)stream.Size;
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)length);
            var bytes = new byte[length];
            reader.ReadBytes(bytes);
            reader.DetachStream();
            _artwork = bytes;
            return bytes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
