namespace SecretBase.Core.Activity;

public enum ActivityKind
{
    ApplicationOpened = 0,
    ProjectOpened = 1,
    FileOpened = 2,
    WorkspaceChanged = 3,
    WorkspacePrepared = 4,
    TodoCreated = 5,
    TodoCompleted = 6,
    FocusStarted = 7,
    FocusEnded = 8,
    CalendarEventStarted = 9,
    CalendarEventEnded = 10,
    MusicChanged = 11,
    GitStateNoted = 12,
    SuggestionAccepted = 13,
    SuggestionDismissed = 14,
    SessionEnded = 15,
    IdleStarted = 16,
    IdleEnded = 17,
    SystemObserved = 18,
    ApplicationClosed = 19,
    ApplicationChanged = 20,
    ProjectClosed = 21,
    ProjectChanged = 22,
    WorkspaceOpened = 23,
    WorkspaceClosed = 24,
    FileChanged = 25,
    FileCreated = 26,
    FileRemoved = 27,
    TodoUpdated = 28,
    SecretBaseOpened = 29,
    CommandExecuted = 30,
    ConfirmationRequested = 31,
    ConfirmationAccepted = 32,
    ConfirmationRejected = 33
}

/// <summary>Normalized Base event. Not an OS hook. Names only — no paths or secrets.</summary>
public sealed class ActivityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public ActivityKind Kind { get; set; }

    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public string Title { get; set; } = string.Empty;

    public string? ProjectName { get; set; }

    public string? Detail { get; set; }

    public string Source { get; set; } = "base";

    public string? CorrelationId { get; set; }

    public double Confidence { get; set; } = 1;

    public string SafePayload => Title;
}

public sealed class MeaningfulActivity
{
    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? ProjectName { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset EndedAt { get; init; }

    public IReadOnlyList<ActivityKind> Kinds { get; init; } = [];
}

public sealed class ActivityDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<ActivityEvent> Events { get; set; } = [];
}

public interface IActivityLog
{
    void Record(ActivityEvent activity);

    IReadOnlyList<ActivityEvent> Recent(int take = 40);

    IReadOnlyList<MeaningfulActivity> Meaningful(DateTimeOffset now, TimeSpan? window = null);
}

public static class ActivityNormalizer
{
    public static IReadOnlyList<MeaningfulActivity> Aggregate(
        IReadOnlyList<ActivityEvent> events,
        DateTimeOffset now,
        TimeSpan? window = null)
    {
        events ??= [];
        var since = now - (window ?? TimeSpan.FromHours(12));
        var slice = events.Where(item => item.At >= since).OrderBy(item => item.At).ToList();
        if (slice.Count == 0)
        {
            return [];
        }

        var groups = new List<List<ActivityEvent>>();
        foreach (var item in slice)
        {
            var last = groups.LastOrDefault();
            var sameProject = last is not null
                              && string.Equals(last[0].ProjectName, item.ProjectName, StringComparison.OrdinalIgnoreCase)
                              && item.At - last[^1].At < TimeSpan.FromMinutes(45);
            if (sameProject && !string.IsNullOrWhiteSpace(item.ProjectName))
            {
                last!.Add(item);
            }
            else if (last is not null
                     && item.At - last[^1].At < TimeSpan.FromMinutes(20)
                     && IsWork(item.Kind) && last.Any(e => IsWork(e.Kind)))
            {
                last.Add(item);
            }
            else
            {
                groups.Add([item]);
            }
        }

        return groups.Select(Summarize).TakeLast(12).ToList();
    }

    public static bool IsWork(ActivityKind kind) =>
        kind is ActivityKind.ProjectOpened
            or ActivityKind.FileOpened
            or ActivityKind.WorkspacePrepared
            or ActivityKind.WorkspaceChanged
            or ActivityKind.ApplicationOpened
            or ActivityKind.FocusStarted
            or ActivityKind.GitStateNoted
            or ActivityKind.ApplicationChanged
            or ActivityKind.ProjectChanged
            or ActivityKind.WorkspaceOpened;

    private static MeaningfulActivity Summarize(List<ActivityEvent> group)
    {
        var project = group.Select(item => item.ProjectName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        var work = group.Any(item => IsWork(item.Kind));
        var title = work && !string.IsNullOrWhiteSpace(project)
            ? $"Worked on {project}"
            : group.Last().Title;
        var bits = new List<string>();
        if (group.Any(item => item.Kind == ActivityKind.ProjectOpened))
        {
            bits.Add("project opened");
        }

        if (group.Any(item => item.Kind == ActivityKind.FileOpened))
        {
            bits.Add("files");
        }

        if (group.Any(item => item.Kind == ActivityKind.FocusStarted))
        {
            bits.Add("focus");
        }

        if (group.Any(item => item.Kind == ActivityKind.GitStateNoted))
        {
            bits.Add("git noted");
        }

        return new MeaningfulActivity
        {
            Title = title,
            Summary = bits.Count == 0 ? group.Last().Title : string.Join(" · ", bits),
            ProjectName = project,
            StartedAt = group[0].At,
            EndedAt = group[^1].At,
            Kinds = group.Select(item => item.Kind).Distinct().ToList()
        };
    }
}

public sealed class ActivityLog : IActivityLog
{
    public const int MaxEvents = 500;
    private readonly object _gate = new();
    private readonly List<ActivityEvent> _events = [];

    public void Record(ActivityEvent activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        lock (_gate)
        {
            var last = _events.LastOrDefault();
            if (last is not null
                && last.Kind == activity.Kind
                && string.Equals(last.Title, activity.Title, StringComparison.OrdinalIgnoreCase)
                && activity.At - last.At < TimeSpan.FromSeconds(20))
            {
                return;
            }

            _events.Add(activity);
            if (_events.Count > MaxEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxEvents);
            }
        }
    }

    public IReadOnlyList<ActivityEvent> Recent(int take = 40)
    {
        lock (_gate)
        {
            return _events.TakeLast(Math.Clamp(take, 1, 100)).ToList();
        }
    }

    public IReadOnlyList<MeaningfulActivity> Meaningful(DateTimeOffset now, TimeSpan? window = null)
    {
        lock (_gate)
        {
            return ActivityNormalizer.Aggregate(_events, now, window);
        }
    }

    public ActivityDocument Snapshot()
    {
        lock (_gate)
        {
            return new ActivityDocument { Events = _events.ToList() };
        }
    }

    public void ReplaceAll(IEnumerable<ActivityEvent> events)
    {
        lock (_gate)
        {
            _events.Clear();
            _events.AddRange(events);
        }
    }
}
