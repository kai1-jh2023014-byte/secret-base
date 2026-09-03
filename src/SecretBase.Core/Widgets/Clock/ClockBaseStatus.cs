using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Calendar;
using SecretBase.Core.Focus;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Widgets.Clock;

/// <summary>Extra Clock lines for the Base style. Keep to two facts.</summary>
public sealed record ClockBaseStatus(
    string NextLine,
    string StatusLine);

public static class ClockBaseStatusComposer
{
    public static ClockBaseStatus Compose(
        DateTimeOffset now,
        IReadOnlyList<CalendarEvent> events,
        WorkspaceSession? workspace,
        FocusSession? focus,
        AssistantProviderStatusInfo? provider,
        TimeAwareSuggestion? suggestion = null)
    {
        events ??= [];
        var next = events
            .Where(item => item.Start >= now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();

        string nextLine;
        if (next is null)
        {
            nextLine = string.IsNullOrWhiteSpace(workspace?.NextTask)
                ? string.Empty
                : "Next  " + workspace!.NextTask;
        }
        else
        {
            var until = next.Start - now;
            nextLine = until <= TimeSpan.Zero
                ? $"Now  {next.Title}"
                : until.TotalHours >= 1
                    ? $"Next  {next.Start:HH:mm} — {next.Title}"
                    : $"Next  {next.Title} · {(int)Math.Ceiling(until.TotalMinutes)}m";
        }

        if (suggestion is not null && string.Equals(suggestion.Kind, "prepare", StringComparison.Ordinal))
        {
            nextLine = $"Next  {suggestion.Title} · prepare?";
        }

        string statusLine;
        if (focus is { IsRunning: true })
        {
            statusLine = focus.StatusLine(now);
        }
        else if (workspace is not null)
        {
            statusLine = workspace.StatusLine;
        }
        else
        {
            statusLine = BaseAiStatusFormatter.Format(provider);
        }

        return new ClockBaseStatus(nextLine, statusLine);
    }
}
