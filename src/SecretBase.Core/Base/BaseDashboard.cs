using SecretBase.Core.Automation;
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

    public bool ShowContinue { get; init; }

    public string CalendarLine { get; init; } = string.Empty;

    public string TasksLine { get; init; } = string.Empty;

    public string MusicLine { get; init; } = string.Empty;

    public string AiLine { get; init; } = string.Empty;

    public string? SuggestionTitle { get; init; }

    public string? SuggestionDetail { get; init; }
}

public static class BaseDashboardComposer
{
    public static BaseDashboardSnapshot Compose(
        UserState state,
        ProjectContinuationContext? continuation = null,
        AutomationSuggestion? suggestion = null,
        int calendarCount = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        var project = continuation?.ProjectName ?? state.CurrentProjectName;
        var last = continuation?.LastSession ?? state.RecentActivityLine;
        var showContinue = !string.IsNullOrWhiteSpace(project) && state.Confidence >= 0.45;
        return new BaseDashboardSnapshot
        {
            Time = state.Now.ToString("HH:mm"),
            Greeting = state.Greeting.ToUpperInvariant(),
            ReadyLine = state.FocusRunning ? state.FocusLine : "Your Base is ready.",
            ContinuationTitle = project ?? string.Empty,
            ContinuationDetail = string.IsNullOrWhiteSpace(last)
                ? string.Empty
                : "Last session: " + last,
            ShowContinue = showContinue,
            CalendarLine = calendarCount > 0
                ? $"{calendarCount} event{(calendarCount == 1 ? string.Empty : "s")}"
                : state.UpcomingCalendarTitle is null
                    ? "Quiet"
                    : state.UpcomingCalendarTitle,
            TasksLine = state.OpenTodoCount == 0
                ? "Clear"
                : $"{state.OpenTodoCount} remaining",
            MusicLine = string.IsNullOrWhiteSpace(state.MusicLine) ? "Silent" : "Now Playing",
            AiLine = state.ProviderLine,
            SuggestionTitle = suggestion?.Title,
            SuggestionDetail = suggestion?.Detail
        };
    }
}
