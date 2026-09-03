namespace SecretBase.Core.Session;

/// <summary>A stretch of work in Secret Base. Deterministic summary — no LLM required.</summary>
public sealed class WorkSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    public string? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public string? WorkspaceTitle { get; set; }

    public string PrimaryActivity { get; set; } = string.Empty;

    public List<string> OpenedResources { get; set; } = [];

    public List<string> CompletedTasks { get; set; } = [];

    public List<string> UnfinishedTasks { get; set; } = [];

    public int FocusCount { get; set; }

    public string LastState { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public sealed class WorkSessionDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<WorkSession> Sessions { get; set; } = [];

    public string? CurrentId { get; set; }
}

public static class WorkSessionSummarizer
{
    public static string Summarize(WorkSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var project = string.IsNullOrWhiteSpace(session.ProjectName) ? "Secret Base" : session.ProjectName;
        var activity = string.IsNullOrWhiteSpace(session.PrimaryActivity)
            ? "work"
            : session.PrimaryActivity;
        var next = session.UnfinishedTasks.FirstOrDefault();
        return string.IsNullOrWhiteSpace(next)
            ? $"{project}: {activity}"
            : $"{project}: {activity} → {next}";
    }
}

public interface IWorkSessionStore
{
    WorkSession? Current { get; }

    WorkSession StartOrContinue(
        DateTimeOffset now,
        string? projectId,
        string? projectName,
        string? workspaceTitle);

    void Touch(string? activity, string? resource, string? unfinishedTask, bool focusStarted);

    WorkSession? End(DateTimeOffset now);

    IReadOnlyList<WorkSession> Recent(int take = 12);

    WorkSessionDocument Snapshot();
}

public sealed class WorkSessionStore : IWorkSessionStore
{
    public const int MaxSessions = 40;
    private readonly object _gate = new();
    private readonly List<WorkSession> _sessions = [];
    private WorkSession? _current;

    public WorkSessionStore(IEnumerable<WorkSession>? seed = null, string? currentId = null)
    {
        if (seed is not null)
        {
            _sessions.AddRange(seed);
            if (!string.IsNullOrWhiteSpace(currentId))
            {
                _current = _sessions.FirstOrDefault(item => item.Id == currentId && item.EndedAt is null);
            }
        }
    }

    public WorkSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public WorkSession StartOrContinue(
        DateTimeOffset now,
        string? projectId,
        string? projectName,
        string? workspaceTitle)
    {
        lock (_gate)
        {
            if (_current is not null
                && _current.EndedAt is null
                && string.Equals(_current.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            {
                _current.WorkspaceTitle = workspaceTitle ?? _current.WorkspaceTitle;
                return _current;
            }

            if (_current is { EndedAt: null })
            {
                CloseUnlocked(_current, now);
            }

            _current = new WorkSession
            {
                StartedAt = now,
                ProjectId = projectId,
                ProjectName = projectName,
                WorkspaceTitle = workspaceTitle,
                LastState = "started"
            };
            _current.Summary = WorkSessionSummarizer.Summarize(_current);
            _sessions.Add(_current);
            TrimUnlocked();
            return _current;
        }
    }

    public void Touch(string? activity, string? resource, string? unfinishedTask, bool focusStarted)
    {
        lock (_gate)
        {
            if (_current is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(activity))
            {
                _current.PrimaryActivity = activity.Trim();
            }

            if (!string.IsNullOrWhiteSpace(resource)
                && !_current.OpenedResources.Contains(resource, StringComparer.OrdinalIgnoreCase))
            {
                _current.OpenedResources.Insert(0, resource.Trim());
                if (_current.OpenedResources.Count > 8)
                {
                    _current.OpenedResources.RemoveRange(8, _current.OpenedResources.Count - 8);
                }
            }

            if (!string.IsNullOrWhiteSpace(unfinishedTask)
                && !_current.UnfinishedTasks.Contains(unfinishedTask, StringComparer.OrdinalIgnoreCase))
            {
                _current.UnfinishedTasks.Insert(0, unfinishedTask.Trim());
            }

            if (focusStarted)
            {
                _current.FocusCount++;
            }

            _current.LastState = activity ?? _current.LastState;
            _current.Summary = WorkSessionSummarizer.Summarize(_current);
        }
    }

    public WorkSession? End(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_current is null)
            {
                return null;
            }

            CloseUnlocked(_current, now);
            var ended = _current;
            _current = null;
            return ended;
        }
    }

    public IReadOnlyList<WorkSession> Recent(int take = 12)
    {
        lock (_gate)
        {
            return _sessions.TakeLast(Math.Clamp(take, 1, 40)).Reverse().ToList();
        }
    }

    public WorkSessionDocument Snapshot()
    {
        lock (_gate)
        {
            return new WorkSessionDocument
            {
                Sessions = _sessions.ToList(),
                CurrentId = _current?.Id
            };
        }
    }

    public void ReplaceAll(IEnumerable<WorkSession> sessions, string? currentId)
    {
        lock (_gate)
        {
            _sessions.Clear();
            _sessions.AddRange(sessions);
            _current = _sessions.FirstOrDefault(item => item.Id == currentId && item.EndedAt is null);
        }
    }

    private void CloseUnlocked(WorkSession session, DateTimeOffset now)
    {
        session.EndedAt = now;
        session.LastState = "ended";
        session.Summary = WorkSessionSummarizer.Summarize(session);
    }

    private void TrimUnlocked()
    {
        if (_sessions.Count <= MaxSessions)
        {
            return;
        }

        _sessions.RemoveRange(0, _sessions.Count - MaxSessions);
    }
}
