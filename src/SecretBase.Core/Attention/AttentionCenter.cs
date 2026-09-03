using SecretBase.Core.Activity;
using SecretBase.Core.Automation;
using SecretBase.Core.Calendar;
using SecretBase.Core.Files;
using SecretBase.Core.Intent;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Attention;

public sealed class AttentionItem
{
    public string Kind { get; init; } = "note";

    public string Title { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public double Importance { get; init; }

    public bool RequiresConfirmation { get; init; }
}

/// <summary>Low-value noise is dropped. Quiet by default.</summary>
public static class AttentionCenter
{
    public static IReadOnlyList<AttentionItem> Compose(
        UserState state,
        CurrentSituation situation,
        DetectedIntent intent,
        AutomationSuggestion? suggestion,
        TodoList todos,
        IReadOnlyList<CalendarEvent> events,
        IReadOnlyList<FileCleanupCandidate>? files = null,
        InterventionMode mode = InterventionMode.Suggest)
    {
        ArgumentNullException.ThrowIfNull(state);
        todos ??= new TodoList();
        events ??= [];
        files ??= [];
        if (mode is InterventionMode.Silent)
        {
            return [];
        }

        var items = new List<AttentionItem>();
        if (suggestion is not null && mode is InterventionMode.Suggest or InterventionMode.Confirm)
        {
            items.Add(new AttentionItem
            {
                Kind = "continuation",
                Title = suggestion.Title,
                Detail = suggestion.Detail,
                Importance = suggestion.Confidence,
                RequiresConfirmation = suggestion.RequiresConfirmation
            });
        }
        else if (intent.Kind is DetectedIntentKind.ContinueProject or DetectedIntentKind.ResumePreviousSession
                 && !string.IsNullOrWhiteSpace(situation.ProjectName)
                 && mode is not InterventionMode.Passive)
        {
            items.Add(new AttentionItem
            {
                Kind = "continuation",
                Title = "Continue " + situation.ProjectName,
                Detail = situation.NextTask ?? situation.RecentActivity ?? string.Empty,
                Importance = intent.Confidence,
                RequiresConfirmation = true
            });
        }

        var next = events
            .Where(item => item.Start > state.Now && item.Start - state.Now <= TimeSpan.FromHours(2))
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        if (next is not null)
        {
            items.Add(new AttentionItem
            {
                Kind = "calendar",
                Title = next.Title,
                Detail = next.Start.ToString("HH:mm"),
                Importance = 0.8
            });
        }

        var overdue = todos.Items.Count(item => !item.IsDone);
        if (overdue >= 3)
        {
            items.Add(new AttentionItem
            {
                Kind = "todo",
                Title = $"{overdue} open tasks",
                Detail = todos.Items.First(item => !item.IsDone).Title,
                Importance = 0.55
            });
        }

        var unused = files.Count(item => item.Kind is FileCandidateKind.Unused or FileCandidateKind.Temporary);
        if (unused >= 4 && mode is InterventionMode.Suggest or InterventionMode.Confirm)
        {
            items.Add(new AttentionItem
            {
                Kind = "files",
                Title = "Registered files to review",
                Detail = unused + " unused or temporary candidates. Nothing will be deleted.",
                Importance = 0.4,
                RequiresConfirmation = true
            });
        }

        return items
            .OrderByDescending(item => item.Importance)
            .Take(5)
            .ToList();
    }

    public static string Format(IReadOnlyList<AttentionItem> items) =>
        items.Count == 0
            ? "Quiet. Nothing that needs you right now."
            : string.Join(Environment.NewLine, items.Select(item => $"{item.Title} — {item.Detail}"));
}
