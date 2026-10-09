namespace SecretBase.Core.Progress;

/// <summary>
/// One append-only log entry capturing Progress + Genesis at a point in time.
/// Used so startup can restore the previous latest values before a live refresh.
/// </summary>
public sealed class ProgressGenesisHistoryEntry
{
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProgressGenesisSnapshot Snapshot { get; set; } = ProgressGenesisSnapshot.CreateEmpty();

    public void Normalize()
    {
        if (RecordedAt == default)
        {
            RecordedAt = DateTimeOffset.UtcNow;
        }

        Snapshot ??= ProgressGenesisSnapshot.CreateEmpty();
        Snapshot.Normalize();
    }
}

/// <summary>Persisted history document for Progress / Genesis advancement.</summary>
public sealed class ProgressGenesisHistoryDocument
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Maximum retained entries (oldest dropped).</summary>
    public const int DefaultMaxEntries = 120;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ProgressGenesisHistoryEntry> Entries { get; set; } = [];

    public void Normalize(int maxEntries = DefaultMaxEntries)
    {
        SchemaVersion = SchemaVersion < 1 ? CurrentSchemaVersion : SchemaVersion;
        Entries ??= [];
        foreach (var entry in Entries)
        {
            entry.Normalize();
        }

        if (Entries.Count > maxEntries)
        {
            Entries = Entries
                .OrderByDescending(e => e.RecordedAt)
                .Take(maxEntries)
                .OrderBy(e => e.RecordedAt)
                .ToList();
        }
    }

    public ProgressGenesisSnapshot? LatestSnapshot()
    {
        Normalize();
        var latest = Entries.OrderByDescending(e => e.RecordedAt).FirstOrDefault();
        return latest?.Snapshot;
    }
}

/// <summary>Append-only Progress / Genesis history for startup restore + change tracking.</summary>
public interface IProgressGenesisHistoryStore
{
    ProgressGenesisHistoryDocument LoadOrCreate();

    ProgressGenesisSnapshot? LoadLatest();

    /// <summary>
    /// Appends <paramref name="snapshot"/> when it differs from the latest entry
    /// (percent/status/phase) or when the log is empty. Returns true when appended.
    /// </summary>
    bool AppendIfChanged(ProgressGenesisSnapshot snapshot);
}

/// <summary>Pure helpers for history comparison (no I/O).</summary>
public static class ProgressGenesisHistoryComparer
{
    public static bool IsMeaningfullyDifferent(ProgressGenesisSnapshot? previous, ProgressGenesisSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(next);
        next.Normalize();
        if (previous is null)
        {
            return true;
        }

        previous.Normalize();
        return !NearlyEqual(previous.Progress.Percent, next.Progress.Percent)
               || !string.Equals(previous.Progress.Status, next.Progress.Status, StringComparison.Ordinal)
               || !NearlyEqual(previous.Genesis.Percent, next.Genesis.Percent)
               || !string.Equals(previous.Genesis.Status, next.Genesis.Status, StringComparison.Ordinal)
               || !string.Equals(previous.Genesis.Phase, next.Genesis.Phase, StringComparison.Ordinal)
               || previous.Genesis.Stage != next.Genesis.Stage;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.05;
}
