namespace SecretBase.Core.Progress;

/// <summary>
/// Overall Progress track — percent complete plus a short status line.
/// </summary>
public sealed class ProgressTrack
{
    public string Title { get; set; } = "Progress";

    /// <summary>0–100 completion.</summary>
    public double Percent { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public static ProgressTrack CreateDefault() => new()
    {
        Title = "Progress",
        Percent = 0,
        Status = "Not started",
        Detail = null
    };

    public void Normalize()
    {
        Title = string.IsNullOrWhiteSpace(Title) ? "Progress" : Title.Trim();
        Percent = Math.Clamp(Percent, 0, 100);
        Status = Status?.Trim() ?? string.Empty;
        Detail = string.IsNullOrWhiteSpace(Detail) ? null : Detail.Trim();
    }
}

/// <summary>One Genesis milestone within the genesis advancement track.</summary>
public sealed class GenesisMilestone
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Label { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public static GenesisMilestone Create(string label, bool isComplete = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new GenesisMilestone
        {
            Id = Guid.NewGuid().ToString("N"),
            Label = label.Trim(),
            IsComplete = isComplete
        };
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }

        Label = Label?.Trim() ?? string.Empty;
    }
}

/// <summary>
/// Genesis advancement — phase/stage progress plus optional milestones.
/// </summary>
public sealed class GenesisTrack
{
    public string Title { get; set; } = "Genesis";

    /// <summary>Named phase within Genesis (e.g. Foundation, Expansion).</summary>
    public string Phase { get; set; } = "Foundation";

    /// <summary>1-based stage index within the current genesis cycle.</summary>
    public int Stage { get; set; } = 1;

    /// <summary>Total stages in the cycle (minimum 1).</summary>
    public int StageCount { get; set; } = 5;

    /// <summary>0–100 completion within the Genesis track.</summary>
    public double Percent { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<GenesisMilestone> Milestones { get; set; } = [];

    public static GenesisTrack CreateDefault() => new()
    {
        Title = "Genesis",
        Phase = "Foundation",
        Stage = 1,
        StageCount = 5,
        Percent = 0,
        Status = "Awaiting start",
        Milestones = []
    };

    public void Normalize()
    {
        Title = string.IsNullOrWhiteSpace(Title) ? "Genesis" : Title.Trim();
        Phase = string.IsNullOrWhiteSpace(Phase) ? "Foundation" : Phase.Trim();
        StageCount = Math.Max(1, StageCount);
        Stage = Math.Clamp(Stage, 1, StageCount);
        Percent = Math.Clamp(Percent, 0, 100);
        Status = Status?.Trim() ?? string.Empty;
        Milestones ??= [];
        foreach (var milestone in Milestones)
        {
            milestone.Normalize();
        }

        Milestones.RemoveAll(m => string.IsNullOrWhiteSpace(m.Label));
    }

    public int CompletedMilestoneCount => Milestones.Count(m => m.IsComplete);
}

/// <summary>
/// Combined Progress + Genesis snapshot fetched by <see cref="IProgressGenesisProvider"/>.
/// </summary>
public sealed class ProgressGenesisSnapshot
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Where the snapshot came from (local-json, http, memory, demo).</summary>
    public string SourceKind { get; set; } = ProgressGenesisSourceKinds.LocalJson;

    public ProgressTrack Progress { get; set; } = ProgressTrack.CreateDefault();

    public GenesisTrack Genesis { get; set; } = GenesisTrack.CreateDefault();

    public static ProgressGenesisSnapshot CreateEmpty(string sourceKind = ProgressGenesisSourceKinds.LocalJson) =>
        new()
        {
            Schema = SchemaVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            SourceKind = sourceKind,
            Progress = ProgressTrack.CreateDefault(),
            Genesis = GenesisTrack.CreateDefault()
        };

    /// <summary>Seeded demo used when the local store is missing — keeps the widget useful on first open.</summary>
    public static ProgressGenesisSnapshot CreateDemoSeed()
    {
        var snapshot = new ProgressGenesisSnapshot
        {
            Schema = SchemaVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            SourceKind = ProgressGenesisSourceKinds.LocalJson,
            Progress = new ProgressTrack
            {
                Title = "Progress",
                Percent = 28,
                Status = "On track",
                Detail = "Base Experience — widgets & overlay"
            },
            Genesis = new GenesisTrack
            {
                Title = "Genesis",
                Phase = "Foundation",
                Stage = 2,
                StageCount = 5,
                Percent = 35,
                Status = "Building",
                Milestones =
                [
                    GenesisMilestone.Create("Define Progress + Genesis model", isComplete: true),
                    GenesisMilestone.Create("Local JSON provider", isComplete: true),
                    GenesisMilestone.Create("Desktop widget surface", isComplete: false),
                    GenesisMilestone.Create("Optional HTTP sync", isComplete: false)
                ]
            }
        };
        snapshot.Normalize();
        return snapshot;
    }

    public void Normalize()
    {
        Schema = Schema < 1 ? SchemaVersion : Schema;
        if (UpdatedAt == default)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        SourceKind = string.IsNullOrWhiteSpace(SourceKind)
            ? ProgressGenesisSourceKinds.LocalJson
            : SourceKind.Trim();
        Progress ??= ProgressTrack.CreateDefault();
        Genesis ??= GenesisTrack.CreateDefault();
        Progress.Normalize();
        Genesis.Normalize();
    }
}

public static class ProgressGenesisSourceKinds
{
    public const string LocalJson = "local-json";
    public const string Http = "http";
    public const string Memory = "memory";

    /// <summary>Live Agent Arena (Base Sepolia) mint / duel counters (optional).</summary>
    public const string AgentArena = "agent-arena";

    /// <summary>
    /// Personal Progress learning API + local Genesis / MusicLab
    /// (<c>kai1-jh2023014-byte/progress</c> + <c>~/genesis</c>).
    /// </summary>
    public const string PersonalSystems = "personal-systems";
}
