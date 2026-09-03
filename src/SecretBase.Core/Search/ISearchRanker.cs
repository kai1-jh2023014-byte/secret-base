namespace SecretBase.Core.Search;

/// <summary>Optional semantic re-ranker. Default is identity — keyword scores already applied.</summary>
public interface ISearchRanker
{
    IReadOnlyList<SearchHit> Rank(IReadOnlyList<SearchHit> hits, string query);
}

public sealed class KeywordSearchRanker : ISearchRanker
{
    public IReadOnlyList<SearchHit> Rank(IReadOnlyList<SearchHit> hits, string query)
    {
        hits ??= [];
        return hits
            .OrderByDescending(hit => hit.Score)
            .ThenByDescending(hit => hit.At ?? DateTimeOffset.MinValue)
            .ToList();
    }
}

public sealed class SearchQuery
{
    public string Text { get; init; } = string.Empty;

    public string? Kind { get; init; }

    public string? ProjectName { get; init; }

    public DateTimeOffset? Since { get; init; }

    public static SearchQuery Parse(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        string? kind = null;
        string? project = null;
        DateTimeOffset? since = null;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = new List<string>();
        foreach (var part in parts)
        {
            if (part.StartsWith("type:", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("kind:", StringComparison.OrdinalIgnoreCase))
            {
                kind = part[(part.IndexOf(':') + 1)..];
                continue;
            }

            if (part.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
            {
                project = part[(part.IndexOf(':') + 1)..].Replace('_', ' ');
                continue;
            }

            if (part.StartsWith("since:", StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.TryParse(part[(part.IndexOf(':') + 1)..], out var parsed))
            {
                since = parsed;
                continue;
            }

            kept.Add(part);
        }

        return new SearchQuery
        {
            Text = kept.Count == 0 ? text : string.Join(' ', kept),
            Kind = string.IsNullOrWhiteSpace(kind) ? null : kind,
            ProjectName = string.IsNullOrWhiteSpace(project) ? null : project,
            Since = since
        };
    }
}
