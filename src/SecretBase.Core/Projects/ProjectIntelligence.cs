using SecretBase.Core.Activity;
using SecretBase.Core.Creative;
using SecretBase.Core.Memory;
using SecretBase.Core.Session;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Projects;

public sealed class ProjectIntelligenceSnapshot
{
    public string? ProjectId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string LastActivity { get; init; } = string.Empty;

    public string RecentSession { get; init; } = string.Empty;

    public string CurrentTask { get; init; } = string.Empty;

    public IReadOnlyList<string> RelatedFiles { get; init; } = [];

    public IReadOnlyList<string> RelatedApps { get; init; } = [];

    public string? Workspace { get; init; }

    public string NextLikelyStep { get; init; } = string.Empty;

    public string Format()
    {
        var lines = new List<string>
        {
            "Project: " + Name,
            "Last activity: " + (string.IsNullOrWhiteSpace(LastActivity) ? "(none)" : LastActivity),
            "Last session: " + (string.IsNullOrWhiteSpace(RecentSession) ? "(none)" : RecentSession),
            "Current task: " + (string.IsNullOrWhiteSpace(CurrentTask) ? "(none)" : CurrentTask),
            "Next: " + (string.IsNullOrWhiteSpace(NextLikelyStep) ? "Continue from the last registered files." : NextLikelyStep),
            "Workspace: " + (Workspace ?? "(none)")
        };
        if (RelatedFiles.Count > 0)
        {
            lines.Add("Files: " + string.Join(", ", RelatedFiles));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class ProjectIntelligence
{
    public static ProjectIntelligenceSnapshot? For(
        string name,
        IReadOnlyList<CreativeProject> projects,
        WorkspaceSession? workspace,
        WorkSession? session,
        IReadOnlyList<MeaningfulActivity> activities,
        IReadOnlyList<MemoryEntry> memories,
        TodoList todos)
    {
        projects ??= [];
        activities ??= [];
        memories ??= [];
        todos ??= new TodoList();
        var project = projects.FirstOrDefault(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (project is null && workspace is not null
            && string.Equals(workspace.ProjectName, name, StringComparison.OrdinalIgnoreCase))
        {
            project = new CreativeProject { Id = workspace.ProjectId ?? string.Empty, Name = name };
        }

        if (project is null)
        {
            return null;
        }

        var last = activities.LastOrDefault(item =>
            string.Equals(item.ProjectName, project.Name, StringComparison.OrdinalIgnoreCase));
        var memory = memories.FirstOrDefault(item =>
            string.Equals(item.ProjectName, project.Name, StringComparison.OrdinalIgnoreCase));
        var task = todos.Items.FirstOrDefault(item =>
            !item.IsDone
            && (string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains(project.Name, StringComparison.OrdinalIgnoreCase)));
        return new ProjectIntelligenceSnapshot
        {
            ProjectId = project.Id,
            Name = project.Name,
            Description = project.Description,
            LastActivity = last?.Title ?? string.Empty,
            RecentSession = session?.Summary ?? workspace?.LastSessionSummary ?? memory?.Summary ?? string.Empty,
            CurrentTask = task?.Title ?? workspace?.NextTask ?? string.Empty,
            RelatedFiles = project.Resources
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => item.Name)
                .Take(6)
                .ToList(),
            RelatedApps = workspace?.SuggestedAppNames ?? [],
            Workspace = workspace?.Title,
            NextLikelyStep = workspace?.NextTask ?? task?.Title ?? memory?.Detail ?? string.Empty
        };
    }
}
