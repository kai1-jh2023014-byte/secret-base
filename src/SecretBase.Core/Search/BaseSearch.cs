using SecretBase.Core.Activity;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Memory;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Search;

public sealed record SearchHit(
    string Kind,
    string Title,
    string Detail,
    double Score);

/// <summary>Deterministic metadata search. No LLM. No disk crawl. Names only.</summary>
public static class BaseSearch
{
    public static IReadOnlyList<SearchHit> Query(
        string query,
        IReadOnlyList<MemoryEntry> memories,
        IReadOnlyList<ActivityEvent> activities,
        IReadOnlyList<CreativeProject> projects,
        IReadOnlyList<CalendarEvent> events,
        TodoList? todos,
        WorkspaceSession? workspace)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return [];
        }

        var hits = new List<SearchHit>();
        Score(hits, "memory", memories.Select(item => (item.Summary, item.Detail ?? item.Key, item.ProjectName ?? string.Empty)), q, 1.0);
        Score(hits, "activity", activities.Select(item => (item.Title, item.Detail ?? string.Empty, item.ProjectName ?? string.Empty)), q, 0.9);
        Score(hits, "project", projects.Select(item => (item.Name, item.Description ?? item.Notes ?? string.Empty, item.Name)), q, 1.1);
        foreach (var project in projects)
        {
            foreach (var resource in project.Resources.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
            {
                var score = Match(resource.Name, q);
                if (score > 0)
                {
                    hits.Add(new SearchHit("file", resource.Name, project.Name, score * 0.85));
                }
            }
        }

        Score(hits, "calendar", events.Select(item => (item.Title, item.Description ?? string.Empty, item.Title)), q, 0.8);
        if (todos is not null)
        {
            Score(hits, "todo", todos.Items.Select(item => (item.Title, item.IsDone ? "done" : "open", item.Title)), q, 0.85);
        }

        if (workspace is not null)
        {
            var score = Math.Max(Match(workspace.Title, q), Match(workspace.LastSessionSummary, q));
            if (score > 0)
            {
                hits.Add(new SearchHit("workspace", workspace.Title, workspace.LastSessionSummary, score));
            }
        }

        if (LooksRelative(q))
        {
            foreach (var item in activities.TakeLast(8).Reverse())
            {
                hits.Add(new SearchHit("activity", item.Title, item.Detail ?? item.ProjectName ?? string.Empty, 0.4));
            }
        }

        return hits
            .GroupBy(hit => hit.Kind + "|" + hit.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(hit => hit.Score).First())
            .OrderByDescending(hit => hit.Score)
            .Take(16)
            .ToList();
    }

    public static string Format(IReadOnlyList<SearchHit> hits)
    {
        if (hits.Count == 0)
        {
            return "No matches in Secret Base memory, activity, projects, calendar, or todos.";
        }

        return string.Join(
            Environment.NewLine,
            hits.Select(hit => $"{hit.Kind}: {hit.Title}" + (string.IsNullOrWhiteSpace(hit.Detail) ? string.Empty : $" — {hit.Detail}")));
    }

    private static void Score(
        List<SearchHit> hits,
        string kind,
        IEnumerable<(string Title, string Detail, string Extra)> rows,
        string query,
        double weight)
    {
        foreach (var row in rows)
        {
            var score = Math.Max(Match(row.Title, query), Match(row.Detail, query)) * weight;
            if (score <= 0)
            {
                continue;
            }

            hits.Add(new SearchHit(kind, row.Title, row.Detail, score));
        }
    }

    private static double Match(string? text, string query)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0.8;
        }

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hits = tokens.Count(token => token.Length > 1 && text.Contains(token, StringComparison.OrdinalIgnoreCase));
        return hits == 0 ? 0 : 0.35 + (0.15 * hits);
    }

    private static bool LooksRelative(string query) =>
        query.Contains("昨日", StringComparison.Ordinal)
        || query.Contains("前回", StringComparison.Ordinal)
        || query.Contains("最近", StringComparison.Ordinal)
        || query.Contains("yesterday", StringComparison.OrdinalIgnoreCase)
        || query.Contains("last time", StringComparison.OrdinalIgnoreCase)
        || query.Contains("recent", StringComparison.OrdinalIgnoreCase);
}
