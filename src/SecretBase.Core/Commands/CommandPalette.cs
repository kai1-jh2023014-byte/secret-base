using SecretBase.Core.Apps;
using SecretBase.Core.Creative;
using SecretBase.Core.Intent;
using SecretBase.Core.Search;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Commands;

public sealed record PaletteItem(
    string Id,
    string Title,
    string Subtitle,
    string Kind,
    string Action,
    double Score,
    string? ProjectName = null);

public static class CommandPalette
{
    public static IReadOnlyList<PaletteItem> Build(
        string query,
        UserState state,
        CurrentSituation situation,
        DetectedIntent intent,
        IReadOnlyList<SearchHit> search,
        IReadOnlyList<CreativeProject> projects,
        IReadOnlyList<CustomApp> apps,
        TodoList todos)
    {
        query ??= string.Empty;
        search ??= [];
        projects ??= [];
        apps ??= [];
        todos ??= new TodoList();
        var items = new List<PaletteItem>
        {
            new("continue", "Continue " + (situation.ProjectName ?? "where you left off"),
                situation.NextTask ?? intent.Rationale, "command", "continue", 1.1, situation.ProjectName),
            new("briefing", "Today's schedule", state.UpcomingCalendarTitle ?? "Daily briefing", "command", "briefing", 0.95),
            new("focus", "Start 30 minute focus", "Safe Auto — no apps launch", "command", "focus", 0.9),
            new("capture", "Quick capture", "Save an idea, todo, or note", "command", "capture", 0.85),
            new("search-memory", "Search memory", "What Secret Base remembers", "command", "memory", 0.8),
            new("timeline", "Today's activity", "Meaningful timeline", "command", "timeline", 0.78),
            new("explain", "Why this suggestion?", "Evidence and confidence", "command", "explain", 0.7),
            new("cleanup", "Review unused files", "Candidates only — never deletes", "command", "cleanup", 0.65),
            new("integrations", "My Integrations", "Registered apps and APIs", "command", "integrations", 0.6)
        };

        foreach (var project in projects.Take(8))
        {
            items.Add(new PaletteItem(
                "p-" + project.Id,
                "Open " + project.Name,
                project.Description ?? "Registered project",
                "project",
                "open-project",
                Match(query, project.Name) + 0.2,
                project.Name));
        }

        foreach (var app in apps.Take(8))
        {
            items.Add(new PaletteItem(
                "a-" + app.Id,
                "Open " + app.Name,
                "Registered app — confirmation required",
                "app",
                "open-app",
                Match(query, app.Name) + 0.15));
        }

        foreach (var todo in todos.Items.Where(item => !item.IsDone).Take(6))
        {
            items.Add(new PaletteItem("t-" + todo.Id, todo.Title, "Open task", "todo", "todo", Match(query, todo.Title) + 0.1));
        }

        foreach (var hit in search.Take(8))
        {
            items.Add(new PaletteItem("s-" + hit.Kind + hit.Title, hit.Title, hit.Kind + " · " + hit.Detail, hit.Kind, "search", hit.Score));
        }

        var q = query.Trim();
        IEnumerable<PaletteItem> ranked = items;
        if (q.Length > 0)
        {
            ranked = items
                .Select(item => item with { Score = item.Score + Match(q, item.Title) + Match(q, item.Subtitle) })
                .Where(item => item.Score > 0.05 || item.Kind == "command");
        }

        return ranked
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .Take(12)
            .ToList();
    }

    private static double Match(string query, string? text)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return text.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.55 : 0;
    }
}
