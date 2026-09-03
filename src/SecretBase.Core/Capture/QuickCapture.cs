using SecretBase.Core.Memory;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Capture;

public enum CaptureDestination
{
    Idea = 0,
    Todo = 1,
    Note = 2,
    Memory = 3,
    Project = 4
}

public sealed class CaptureDraft
{
    public string Text { get; init; } = string.Empty;

    public CaptureDestination Destination { get; init; }

    public string? ProjectName { get; init; }

    public string Reason { get; init; } = string.Empty;
}

/// <summary>Heuristic classification only. Never writes until the caller persists.</summary>
public static class QuickCaptureClassifier
{
    public static CaptureDraft Classify(string text, IReadOnlyList<string>? projectNames = null)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return new CaptureDraft { Destination = CaptureDestination.Note, Reason = "Empty capture." };
        }

        var projects = projectNames ?? [];
        var matched = projects.FirstOrDefault(name =>
            !string.IsNullOrWhiteSpace(name) && raw.Contains(name, StringComparison.OrdinalIgnoreCase));
        var destination = LooksLikeIdea(raw)
            ? CaptureDestination.Idea
            : LooksLikeTodo(raw)
                ? CaptureDestination.Todo
                : !string.IsNullOrWhiteSpace(matched)
                    ? CaptureDestination.Project
                    : CaptureDestination.Note;
        return new CaptureDraft
        {
            Text = raw.Length <= 240 ? raw : raw[..240],
            Destination = destination,
            ProjectName = matched,
            Reason = destination switch
            {
                CaptureDestination.Todo => "Looks like a task.",
                CaptureDestination.Idea => "Looks like an idea to remember.",
                CaptureDestination.Project => "Mentions a registered project.",
                _ => "Saved as a note until you choose otherwise."
            }
        };
    }

    public static MemoryEntry ToMemory(CaptureDraft draft, DateTimeOffset now) =>
        new()
        {
            Scope = draft.Destination == CaptureDestination.Idea ? MemoryScope.Decision : MemoryScope.Session,
            Kind = draft.Destination switch
            {
                CaptureDestination.Idea => MemoryKind.Idea,
                CaptureDestination.Todo => MemoryKind.Todo,
                CaptureDestination.Project => MemoryKind.Project,
                _ => MemoryKind.Note
            },
            Key = "capture-" + now.ToUnixTimeSeconds(),
            Summary = draft.Text,
            ProjectName = draft.ProjectName,
            Source = "quick-capture",
            Confidence = 0.7,
            Importance = MemoryImportance.Normal,
            Retention = MemoryRetention.LongTerm,
            CreatedAt = now,
            UpdatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = MemoryPolicy.DefaultExpiry(MemoryScope.Session, now),
            Tags = string.IsNullOrWhiteSpace(draft.ProjectName) ? [] : [draft.ProjectName]
        };

    public static TodoItem ToTodo(CaptureDraft draft) => TodoItem.Create(draft.Text);

    private static bool LooksLikeTodo(string text) =>
        text.Contains("したい", StringComparison.Ordinal)
        || text.Contains("やる", StringComparison.Ordinal)
        || text.Contains("todo", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("add ", StringComparison.OrdinalIgnoreCase)
        || text.Contains("task", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeIdea(string text) =>
        text.Contains("アイデア", StringComparison.Ordinal)
        || text.Contains("idea", StringComparison.OrdinalIgnoreCase)
        || text.Contains("追加したい", StringComparison.Ordinal)
        || text.Contains("maybe", StringComparison.OrdinalIgnoreCase);
}
