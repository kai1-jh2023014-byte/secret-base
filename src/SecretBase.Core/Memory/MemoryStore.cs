namespace SecretBase.Core.Memory;

public enum MemoryScope
{
    Session = 0,
    Project = 1,
    Workspace = 2,
    Workflow = 3,
    Decision = 4,
    Preference = 5
}

public enum MemoryImportance
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// A durable fact useful for future decisions. Never stores secrets, API keys, or raw file contents.
/// </summary>
public sealed class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public MemoryScope Scope { get; set; } = MemoryScope.Session;

    public string Key { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string Source { get; set; } = "base";

    public double Confidence { get; set; } = 0.5;

    public MemoryImportance Importance { get; set; } = MemoryImportance.Normal;

    public string? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class MemoryDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<MemoryEntry> Entries { get; set; } = [];
}

public static class MemoryPolicy
{
    public const int MaxEntries = 200;
    public static readonly TimeSpan DefaultSessionTtl = TimeSpan.FromDays(14);
    public static readonly TimeSpan DefaultProjectTtl = TimeSpan.FromDays(180);
    public static readonly TimeSpan DefaultDecisionTtl = TimeSpan.FromDays(365);

    public static DateTimeOffset? DefaultExpiry(MemoryScope scope, DateTimeOffset now) =>
        scope switch
        {
            MemoryScope.Session => now + DefaultSessionTtl,
            MemoryScope.Workspace => now + TimeSpan.FromDays(60),
            MemoryScope.Workflow => now + TimeSpan.FromDays(90),
            MemoryScope.Preference => null,
            MemoryScope.Decision => now + DefaultDecisionTtl,
            _ => now + DefaultProjectTtl
        };

    public static bool LooksSensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("sk-", StringComparison.OrdinalIgnoreCase)
               || text.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Bearer ", StringComparison.Ordinal)
               || text.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikePath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(":\\", StringComparison.Ordinal)
               || text.StartsWith('/') && text.Contains('/', StringComparison.Ordinal)
               && (text.Contains(".exe", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("/Users/", StringComparison.Ordinal)
                   || text.Contains("/home/", StringComparison.Ordinal));
    }
}

public interface IMemoryStore : IMemoryWriter, IMemoryRetriever
{
    MemoryDocument Snapshot();
}

public interface IMemoryWriter
{
    MemoryEntry Remember(MemoryEntry entry);

    void Forget(string id);
}

public interface IMemoryRetriever
{
    IReadOnlyList<MemoryEntry> Recall(
        DateTimeOffset now,
        MemoryScope? scope = null,
        string? projectId = null,
        string? query = null,
        int take = 12);
}

public sealed class MemoryStore : IMemoryStore
{
    private readonly object _gate = new();
    private readonly List<MemoryEntry> _entries = [];

    public MemoryStore(IEnumerable<MemoryEntry>? seed = null)
    {
        if (seed is not null)
        {
            _entries.AddRange(seed);
        }
    }

    public MemoryEntry Remember(MemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (MemoryPolicy.LooksSensitive(entry.Summary)
            || MemoryPolicy.LooksSensitive(entry.Detail)
            || MemoryPolicy.LooksLikePath(entry.Summary)
            || MemoryPolicy.LooksLikePath(entry.Detail))
        {
            throw new InvalidOperationException("Memory refused a sensitive or path-like payload.");
        }

        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                entry.Id = Guid.NewGuid().ToString("N");
            }

            entry.Confidence = Math.Clamp(entry.Confidence, 0, 1);
            entry.LastAccessedAt = entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.LastAccessedAt;
            var existing = _entries.FindIndex(item =>
                item.Scope == entry.Scope
                && string.Equals(item.Key, entry.Key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ProjectId, entry.ProjectId, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                entry.Id = _entries[existing].Id;
                entry.CreatedAt = _entries[existing].CreatedAt;
                _entries[existing] = entry;
            }
            else
            {
                _entries.Add(entry);
            }

            PruneUnlocked(entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.CreatedAt);
            return entry;
        }
    }

    public void Forget(string id)
    {
        lock (_gate)
        {
            _entries.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        }
    }

    public IReadOnlyList<MemoryEntry> Recall(
        DateTimeOffset now,
        MemoryScope? scope = null,
        string? projectId = null,
        string? query = null,
        int take = 12)
    {
        lock (_gate)
        {
            PruneUnlocked(now);
            IEnumerable<MemoryEntry> q = _entries;
            if (scope is not null)
            {
                q = q.Where(item => item.Scope == scope);
            }

            if (!string.IsNullOrWhiteSpace(projectId))
            {
                q = q.Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                q = q.Where(item =>
                    Contains(item.Summary, query)
                    || Contains(item.Detail, query)
                    || Contains(item.Key, query)
                    || Contains(item.ProjectName, query));
            }

            var list = q
                .OrderByDescending(item => item.Importance)
                .ThenByDescending(item => item.LastAccessedAt)
                .Take(Math.Clamp(take, 1, 40))
                .ToList();
            foreach (var item in list)
            {
                item.LastAccessedAt = now;
            }

            return list;
        }
    }

    public MemoryDocument Snapshot()
    {
        lock (_gate)
        {
            return new MemoryDocument
            {
                Entries = _entries.Select(Clone).ToList()
            };
        }
    }

    public void ReplaceAll(IEnumerable<MemoryEntry> entries)
    {
        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(entries);
        }
    }

    private void PruneUnlocked(DateTimeOffset now)
    {
        _entries.RemoveAll(item => item.ExpiresAt is not null && item.ExpiresAt <= now);
        if (_entries.Count <= MemoryPolicy.MaxEntries)
        {
            return;
        }

        var extra = _entries.Count - MemoryPolicy.MaxEntries;
        var drop = _entries
            .OrderBy(item => item.Importance)
            .ThenBy(item => item.LastAccessedAt)
            .Take(extra)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        _entries.RemoveAll(item => drop.Contains(item.Id));
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrWhiteSpace(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static MemoryEntry Clone(MemoryEntry item) => new()
    {
        Id = item.Id,
        Scope = item.Scope,
        Key = item.Key,
        Summary = item.Summary,
        Detail = item.Detail,
        Source = item.Source,
        Confidence = item.Confidence,
        Importance = item.Importance,
        ProjectId = item.ProjectId,
        ProjectName = item.ProjectName,
        CreatedAt = item.CreatedAt,
        LastAccessedAt = item.LastAccessedAt,
        ExpiresAt = item.ExpiresAt
    };
}
