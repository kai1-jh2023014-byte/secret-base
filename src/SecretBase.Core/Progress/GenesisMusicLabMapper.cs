namespace SecretBase.Core.Progress;

/// <summary>
/// Maps local Genesis / MusicLab presence + optional status JSON into a Genesis track.
/// Never invents percentage — percent stays 0 unless MusicLab reports it explicitly.
/// </summary>
public static class GenesisMusicLabMapper
{
    public const string DefaultStatusFileName = "genesis-status.json";

    public static GenesisTrack FromStatus(GenesisMusicLabStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var phase = string.IsNullOrWhiteSpace(status.Phase) ? "MusicLab" : status.Phase.Trim();
        var running = status.IsRunning;
        var installed = status.IsInstalled || running || status.HasDesktopLaunchers;

        // Honest percent: only use an explicit value from status JSON / API.
        var percent = status.Percent.HasValue
            ? Math.Clamp(status.Percent.Value, 0, 100)
            : 0;

        var stageCount = Math.Max(1, status.StageCount <= 0 ? 4 : status.StageCount);
        var stage = status.Stage is > 0
            ? Math.Clamp(status.Stage.Value, 1, stageCount)
            : 1;

        var trackStatus = !string.IsNullOrWhiteSpace(status.Status)
            ? status.Status.Trim()
            : running
                ? "MusicLab running"
                : installed
                    ? "Launchers ready — no status % yet"
                    : "Genesis / MusicLab not found";

        var milestones = status.Milestones.Count > 0
            ? status.Milestones
            :
            [
                GenesisMilestone.Create("Genesis tree present", status.IsInstalled),
                GenesisMilestone.Create("Desktop MusicLab launchers", status.HasDesktopLaunchers),
                GenesisMilestone.Create("MusicLab running", running),
                GenesisMilestone.Create("Status file with percent", status.HasStatusFile && status.Percent.HasValue)
            ];

        var track = new GenesisTrack
        {
            Title = "Genesis",
            Phase = phase,
            Stage = stage,
            StageCount = stageCount,
            Percent = percent,
            Status = trackStatus,
            Milestones = milestones.ToList()
        };
        track.Normalize();
        return track;
    }

    public static GenesisTrack CreateUnknown() =>
        FromStatus(new GenesisMusicLabStatus
        {
            Status = "Genesis / MusicLab status unknown",
            Percent = 0
        });

    public static ProgressGenesisSnapshot Combine(
        ProgressTrack progress,
        GenesisTrack genesis,
        DateTimeOffset? updatedAt = null)
    {
        var snapshot = new ProgressGenesisSnapshot
        {
            Schema = ProgressGenesisSnapshot.SchemaVersion,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            SourceKind = ProgressGenesisSourceKinds.PersonalSystems,
            Progress = progress,
            Genesis = genesis
        };
        snapshot.Normalize();
        return snapshot;
    }
}

/// <summary>Observed / reported state of the local Genesis MusicLab.</summary>
public sealed class GenesisMusicLabStatus
{
    public string Phase { get; init; } = "MusicLab";

    public string? Status { get; init; }

    /// <summary>Explicit percent from MusicLab status JSON/API. Null = unknown (display 0).</summary>
    public double? Percent { get; init; }

    public int? Stage { get; init; }

    public int StageCount { get; init; } = 4;

    public bool IsInstalled { get; init; }

    public bool IsRunning { get; init; }

    public bool HasDesktopLaunchers { get; init; }

    public bool HasStatusFile { get; init; }

    public List<GenesisMilestone> Milestones { get; init; } = [];
}
