using SecretBase.Core.Activity;
using SecretBase.Core.Memory;
using SecretBase.Core.Session;
using SecretBase.Core.State;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Workspace;

public sealed class ProjectContinuationContext
{
    public string? ProjectId { get; init; }

    public string? ProjectName { get; init; }

    public string LastSession { get; init; } = string.Empty;

    public string NextTask { get; init; } = string.Empty;

    public IReadOnlyList<string> Relevant { get; init; } = [];

    public double Confidence { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public string Format()
    {
        var lines = new List<string>
        {
            "Likely project: " + (ProjectName ?? "(unknown)"),
            "Last session: " + (string.IsNullOrWhiteSpace(LastSession) ? "(none yet)" : LastSession),
            "Next task: " + (string.IsNullOrWhiteSpace(NextTask) ? "Continue from the last registered files." : NextTask),
            $"Confidence: {Confidence:0.00}"
        };
        if (Relevant.Count > 0)
        {
            lines.Add("Relevant: " + string.Join(", ", Relevant));
        }

        lines.Add("Git status requires confirmation — Secret Base will not run git.");
        return string.Join(Environment.NewLine, lines);
    }
}

public static class ProjectContinuation
{
    public static ProjectContinuationContext Build(
        UserState state,
        WorkspaceSession? workspace,
        IReadOnlyList<MemoryEntry> memories,
        IReadOnlyList<MeaningfulActivity> activities,
        TodoList? todos,
        WorkSession? lastSession = null)
    {
        memories ??= [];
        activities ??= [];
        todos ??= new TodoList();
        var projectName = state.CurrentProjectName ?? workspace?.ProjectName ?? lastSession?.ProjectName;
        var projectId = state.CurrentProjectId ?? workspace?.ProjectId ?? lastSession?.ProjectId;
        var sessionMemory = memories.FirstOrDefault(item => item.Scope == MemoryScope.Session)
                            ?? memories.FirstOrDefault(item => item.Scope == MemoryScope.Project);
        var last = lastSession?.Summary
                   ?? workspace?.LastSessionSummary
                   ?? sessionMemory?.Summary
                   ?? activities.LastOrDefault()?.Summary
                   ?? string.Empty;
        var next = lastSession?.UnfinishedTasks.FirstOrDefault()
                   ?? workspace?.NextTask
                   ?? todos.Items.FirstOrDefault(item => !item.IsDone)?.Title
                   ?? sessionMemory?.Detail
                   ?? string.Empty;
        var relevant = new List<string>();
        if (workspace is not null)
        {
            relevant.AddRange(workspace.SuggestedFileNames.Take(4));
        }

        foreach (var memory in memories.Where(item => item.Scope == MemoryScope.Decision).Take(2))
        {
            relevant.Add(memory.Summary);
        }

        foreach (var activity in activities.TakeLast(3))
        {
            if (!string.IsNullOrWhiteSpace(activity.Title)
                && !relevant.Contains(activity.Title, StringComparer.OrdinalIgnoreCase))
            {
                relevant.Add(activity.Title);
            }
        }

        if (lastSession is not null)
        {
            relevant.AddRange(lastSession.OpenedResources.Take(3));
        }

        var evidence = new List<string>();
        if (!string.IsNullOrWhiteSpace(last))
        {
            evidence.Add("Last session: " + last);
        }

        if (!string.IsNullOrWhiteSpace(next))
        {
            evidence.Add("Next: " + next);
        }

        return new ProjectContinuationContext
        {
            ProjectId = projectId,
            ProjectName = projectName,
            LastSession = last,
            NextTask = next ?? string.Empty,
            Relevant = relevant.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList(),
            Confidence = state.Confidence,
            Evidence = evidence
        };
    }
}
