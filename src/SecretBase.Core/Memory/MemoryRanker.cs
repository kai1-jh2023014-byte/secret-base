namespace SecretBase.Core.Memory;

/// <summary>
/// Rank recall by relevance, recency, importance, and confidence. Never returns the full store.
/// </summary>
public static class MemoryRanker
{
    public static IReadOnlyList<MemoryEntry> Rank(
        IReadOnlyList<MemoryEntry> entries,
        DateTimeOffset now,
        string? query = null,
        string? projectName = null,
        int take = 8)
    {
        entries ??= [];
        return entries
            .Select(item => (Item: item, Score: Score(item, now, query, projectName)))
            .Where(row => row.Score > 0.05)
            .OrderByDescending(row => row.Score)
            .Take(Math.Clamp(take, 1, 20))
            .Select(row => row.Item)
            .ToList();
    }

    public static double Score(
        MemoryEntry item,
        DateTimeOffset now,
        string? query,
        string? projectName)
    {
        ArgumentNullException.ThrowIfNull(item);
        var importance = (int)item.Importance / 3.0;
        var ageHours = Math.Max(0, (now - item.UpdatedAt.ToUniversalTime()).TotalHours);
        if (item.UpdatedAt == default)
        {
            ageHours = Math.Max(0, (now - item.CreatedAt.ToUniversalTime()).TotalHours);
        }

        var recency = Math.Clamp(1 - (ageHours / (24 * 30)), 0.05, 1);
        var relevance = 0.2;
        if (!string.IsNullOrWhiteSpace(projectName)
            && string.Equals(item.ProjectName, projectName, StringComparison.OrdinalIgnoreCase))
        {
            relevance += 0.45;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (Contains(item.Summary, query) || Contains(item.Key, query) || Contains(item.ProjectName, query))
            {
                relevance += 0.35;
            }
            else if (Contains(item.Detail, query))
            {
                relevance += 0.2;
            }
        }

        return Math.Clamp(
            (importance * 0.3) + (recency * 0.25) + (relevance * 0.3) + (item.Confidence * 0.15),
            0,
            1);
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrWhiteSpace(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
