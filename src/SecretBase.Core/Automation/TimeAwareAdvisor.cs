using SecretBase.Core.Calendar;
using SecretBase.Core.Focus;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Automation;

public sealed record TimeAwareSuggestion(
    string Kind,
    string Title,
    string Detail,
    bool RequiresConfirmation);

/// <summary>
/// Heuristic, event-driven suggestions. Never launches apps or calls an LLM.
/// </summary>
public static class TimeAwareAdvisor
{
    public static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(15);

    public static TimeAwareSuggestion? Suggest(
        DateTimeOffset now,
        IReadOnlyList<CalendarEvent> events,
        FocusSession? focus = null,
        WorkspaceSession? workspace = null)
    {
        events ??= [];
        if (focus is { IsRunning: true } && !focus.IsComplete(now))
        {
            return null;
        }

        var current = events
            .Where(item => !item.IsAllDay && item.Start <= now && item.End > now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        if (current is not null && LooksLikeWork(current.Title) && workspace is null)
        {
            return new TimeAwareSuggestion(
                "prepare",
                current.Title,
                "A work block is on now. Prepare a workspace?",
                RequiresConfirmation: false);
        }

        var next = events
            .Where(item => !item.IsAllDay && item.Start > now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        if (next is null)
        {
            return null;
        }

        var until = next.Start - now;
        if (until > LeadTime)
        {
            return null;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(until.TotalMinutes));
        if (LooksLikeWork(next.Title) && workspace is null)
        {
            return new TimeAwareSuggestion(
                "prepare",
                next.Title,
                $"Starts in {minutes}m. Prepare a workspace?",
                RequiresConfirmation: false);
        }

        return new TimeAwareSuggestion(
            "upcoming",
            next.Title,
            $"Starts in {minutes}m",
            RequiresConfirmation: false);
    }

    public static bool LooksLikeWork(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return title.Contains("開発", StringComparison.Ordinal)
               || title.Contains("作業", StringComparison.Ordinal)
               || title.Contains("dev", StringComparison.OrdinalIgnoreCase)
               || title.Contains("code", StringComparison.OrdinalIgnoreCase)
               || title.Contains("focus", StringComparison.OrdinalIgnoreCase)
               || title.Contains("study", StringComparison.OrdinalIgnoreCase)
               || title.Contains("project", StringComparison.OrdinalIgnoreCase);
    }
}
