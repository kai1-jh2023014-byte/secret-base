using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Workspace;

/// <summary>
/// Heuristic workspace preparation from local Secret Base stores.
/// Never launches processes or reads the filesystem.
/// </summary>
public static class WorkspacePreparer
{
    public static WorkspaceSession Prepare(
        string intent,
        IReadOnlyList<CreativeProject> projects,
        IReadOnlyList<CustomApp> apps,
        TodoList todos,
        IReadOnlyList<CalendarEvent> events,
        DateTimeOffset now)
    {
        projects ??= [];
        apps ??= [];
        todos ??= new TodoList();
        events ??= [];

        var project = MatchProject(intent, projects);
        var openTodos = todos.Items.Where(item => !item.IsDone).Take(3).ToList();
        var nextEvent = events
            .Where(item => item.Start >= now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();

        var suggestedApps = SuggestApps(project, apps);
        var suggestedFiles = project is null
            ? new List<string>()
            : project.Resources
                .Where(resource => !string.IsNullOrWhiteSpace(resource.Name))
                .Take(6)
                .Select(resource => resource.Name)
                .ToList();

        var checks = new List<string>();
        if (project is not null)
        {
            checks.Add("Project");
        }

        if (suggestedApps.Count > 0)
        {
            checks.Add("Apps (registered)");
        }

        if (suggestedFiles.Count > 0)
        {
            checks.Add("Relevant files (registered)");
        }

        checks.Add("Git status requires confirmation");
        if (openTodos.Count > 0)
        {
            checks.Add("Open tasks");
        }

        var lastSession = project is null
            ? "No matching project yet. Register one in Projects."
            : $"{project.Name} · {project.Resources.Count} registered resource(s)";

        var nextTask = openTodos.Count > 0
            ? openTodos[0].Title
            : nextEvent is null
                ? "Continue from the last registered files."
                : nextEvent.Title;

        var title = string.IsNullOrWhiteSpace(intent)
            ? "Continue"
            : Truncate(intent.Trim(), 48);

        return new WorkspaceSession
        {
            Title = title,
            ProjectId = project?.Id,
            ProjectName = project?.Name,
            LastSessionSummary = lastSession,
            SuggestedAppNames = suggestedApps.ToList(),
            SuggestedFileNames = suggestedFiles.ToList(),
            PreparedChecks = checks,
            NextTask = nextTask,
            PreparedAt = now
        };
    }

    public static CreativeProject? MatchProject(string intent, IReadOnlyList<CreativeProject> projects)
    {
        if (projects.Count == 0)
        {
            return null;
        }

        var haystack = (intent ?? string.Empty).Trim();
        if (haystack.Length > 0)
        {
            foreach (var project in projects)
            {
                if (string.IsNullOrWhiteSpace(project.Name))
                {
                    continue;
                }

                if (haystack.Contains(project.Name, StringComparison.OrdinalIgnoreCase)
                    || project.Name.Contains(haystack, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }
        }

        return projects.FirstOrDefault(project => project.IsFavorite)
               ?? projects[0];
    }

    public static IReadOnlyList<string> SuggestApps(CreativeProject? project, IReadOnlyList<CustomApp> apps)
    {
        var names = new List<string>();
        if (project is not null)
        {
            foreach (var app in apps)
            {
                if (!string.IsNullOrWhiteSpace(app.CreativeProjectId)
                    && string.Equals(app.CreativeProjectId, project.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(app.Name))
                {
                    names.Add(app.Name);
                }
            }
        }

        foreach (var app in apps.Take(4))
        {
            if (!string.IsNullOrWhiteSpace(app.Name)
                && !names.Contains(app.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(app.Name);
            }
        }

        return names.Take(5).ToList();
    }

    public static string FormatCard(WorkspaceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var lines = new List<string>
        {
            session.Title,
            string.Empty,
            "Last session:",
            session.LastSessionSummary,
            string.Empty,
            "Prepared:"
        };
        foreach (var check in session.PreparedChecks)
        {
            lines.Add("✓ " + check);
        }

        if (session.SuggestedAppNames.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Apps: " + string.Join(", ", session.SuggestedAppNames));
        }

        if (session.SuggestedFileNames.Count > 0)
        {
            lines.Add("Files: " + string.Join(", ", session.SuggestedFileNames));
        }

        if (!string.IsNullOrWhiteSpace(session.NextTask))
        {
            lines.Add(string.Empty);
            lines.Add("Next: " + session.NextTask);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
