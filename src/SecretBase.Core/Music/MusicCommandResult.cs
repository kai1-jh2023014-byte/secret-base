namespace SecretBase.Core.Music;

/// <summary>Result of executing a <see cref="MusicCommand"/> (no OS side effects outside providers).</summary>
public sealed class MusicCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public MusicCommandKind Kind { get; init; }

    public IReadOnlyList<MusicTrack> Tracks { get; init; } = Array.Empty<MusicTrack>();

    public MusicTrack? CurrentTrack { get; init; }

    public bool IsPlaying { get; init; }

    public long? ProgressMilliseconds { get; init; }

    public long? DurationMilliseconds { get; init; }

    public string? ProviderId { get; init; }

    public MusicAuthStatus AuthStatus { get; init; } = MusicAuthStatus.NotConfigured;

    public static MusicCommandResult Ok(
        MusicCommandKind kind,
        IReadOnlyList<MusicTrack>? tracks = null,
        MusicTrack? current = null,
        bool isPlaying = false,
        long? progressMs = null,
        long? durationMs = null,
        string? providerId = null,
        MusicAuthStatus authStatus = MusicAuthStatus.NotConfigured) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Tracks = tracks ?? Array.Empty<MusicTrack>(),
            CurrentTrack = current,
            IsPlaying = isPlaying,
            ProgressMilliseconds = progressMs ?? current?.ProgressMilliseconds,
            DurationMilliseconds = durationMs ?? current?.DurationMilliseconds,
            ProviderId = providerId,
            AuthStatus = authStatus
        };

    public static MusicCommandResult Fail(MusicCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
