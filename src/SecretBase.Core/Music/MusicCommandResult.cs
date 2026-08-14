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

    public static MusicCommandResult Ok(
        MusicCommandKind kind,
        IReadOnlyList<MusicTrack>? tracks = null,
        MusicTrack? current = null,
        bool isPlaying = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Tracks = tracks ?? Array.Empty<MusicTrack>(),
            CurrentTrack = current,
            IsPlaying = isPlaying
        };

    public static MusicCommandResult Fail(MusicCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
