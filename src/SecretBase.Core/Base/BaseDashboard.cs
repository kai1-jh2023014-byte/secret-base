using SecretBase.Core.Automation;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Base;

/// <summary>State-centered Personal Space card. Not a widget dashboard dump.</summary>
public sealed class BaseDashboardSnapshot
{
    public string Time { get; init; } = string.Empty;

    public string Greeting { get; init; } = string.Empty;

    public string ReadyLine { get; init; } = "Your Base is ready.";

    public string ContinuationTitle { get; init; } = string.Empty;

    public string ContinuationDetail { get; init; } = string.Empty;

    public string NextTaskLine { get; init; } = string.Empty;

    public bool ShowContinue { get; init; }

    public string CalendarLine { get; init; } = string.Empty;

    public string TasksLine { get; init; } = string.Empty;

    public string MusicLine { get; init; } = string.Empty;

    public string AiLine { get; init; } = string.Empty;

    public string? SuggestionTitle { get; init; }

    public string? SuggestionDetail { get; init; }

    public string AttentionLine { get; init; } = string.Empty;

    public string BriefingHeadline { get; init; } = string.Empty;
}

public static class BaseDashboardComposer
{
    public static BaseDashboardSnapshot Compose(
        UserState state,
        ProjectContinuationContext? continuation = null,
        AutomationSuggestion? suggestion = null,
        int calendarCount = 0,
        CurrentSituation? situation = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var project = continuation?.ProjectName ?? situation?.ProjectName ?? state.CurrentProjectName;
        var last = continuation?.LastSession ?? situation?.RecentActivity ?? state.RecentActivityLine;
        var next = continuation?.NextTask ?? situation?.NextTask;
        var calendarHeadline = state.CurrentCalendarTitle ?? state.UpcomingCalendarTitle ?? situation?.Calendar;
        var showContinue = !string.IsNullOrWhiteSpace(project) && state.Confidence >= 0.45;
        var ready = state.FocusRunning
            ? state.FocusLine
            : !string.IsNullOrWhiteSpace(calendarHeadline)
                ? calendarHeadline
                : showContinue
                    ? "Continue where you left off?"
                    : "Your Base is ready.";
        return new BaseDashboardSnapshot
        {
            Time = state.Now.ToString("HH:mm"),
            Greeting = state.Greeting.ToUpperInvariant(),
            ReadyLine = ready,
            ContinuationTitle = project ?? string.Empty,
            ContinuationDetail = string.IsNullOrWhiteSpace(last)
                ? string.Empty
                : "Last session: " + last,
            NextTaskLine = string.IsNullOrWhiteSpace(next) ? string.Empty : "Next: " + next,
            ShowContinue = showContinue,
            CalendarLine = calendarCount > 0
                ? $"{calendarCount} event{(calendarCount == 1 ? string.Empty : "s")}"
                : calendarHeadline ?? "Quiet",
            TasksLine = state.OpenTodoCount == 0
                ? "Clear"
                : $"{state.OpenTodoCount} remaining",
            MusicLine = string.IsNullOrWhiteSpace(state.MusicLine) ? "Silent" : "Now Playing",
            AiLine = state.ProviderLine,
            SuggestionTitle = suggestion?.Title,
            SuggestionDetail = suggestion?.Detail,
            AttentionLine = situation?.WorkingState == "focus"
                ? "Quiet — focus is on"
                : situation?.NextLikelyAction ?? string.Empty,
            BriefingHeadline = situation?.Calendar ?? continuation?.LastSession ?? string.Empty
        };
    }
}
