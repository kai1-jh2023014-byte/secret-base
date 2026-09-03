using SecretBase.Core.Automation;
using SecretBase.Core.Intent;
using SecretBase.Core.Situation;

namespace SecretBase.Core.Explain;

/// <summary>Deterministic "why" — never a diagnosis, never certainty about the user's life.</summary>
public static class IntentExplainer
{
    public static string Explain(DetectedIntent intent, CurrentSituation? situation = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var lines = new List<string>
        {
            intent.Kind == DetectedIntentKind.Unknown
                ? "There is not enough signal to guess what you want next."
                : $"I think you may want to {Phrase(intent)} because:"
        };

        var evidence = intent.Evidence.Count > 0
            ? intent.Evidence
            : situation?.Evidence.Select(item => item.Fact).ToList() ?? [];
        if (evidence.Count == 0)
        {
            lines.Add("• Not enough recent activity, calendar, or session evidence.");
        }
        else
        {
            foreach (var fact in evidence.Take(5))
            {
                lines.Add("• " + fact);
            }
        }

        lines.Add(string.Empty);
        lines.Add($"Confidence: {intent.Confidence:P0}");
        lines.Add("This is a suggestion, not a certainty. Nothing will launch until you confirm.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Phrase(DetectedIntent intent) =>
        intent.Kind switch
        {
            DetectedIntentKind.ContinueProject => "continue "
                + (intent.ProjectName ?? "your current project"),
            DetectedIntentKind.ResumePreviousSession => "resume the previous session"
                + (string.IsNullOrWhiteSpace(intent.ProjectName) ? string.Empty : " (" + intent.ProjectName + ")"),
            DetectedIntentKind.StartFocus => "start a focus session",
            DetectedIntentKind.PrepareWorkspace => "prepare a workspace",
            DetectedIntentKind.ReviewTasks => "review open tasks",
            DetectedIntentKind.ReviewCalendar => "review today's calendar",
            DetectedIntentKind.OpenProject => "open " + (intent.ProjectName ?? "a registered project"),
            DetectedIntentKind.SearchInformation => "search Secret Base",
            DetectedIntentKind.OrganizeFiles => "review unused registered files",
            DetectedIntentKind.TakeBreak => "take a break",
            DetectedIntentKind.EndWork => "wrap up for now",
            DetectedIntentKind.MusicListening => "keep music in the background",
            DetectedIntentKind.QuickCapture => "capture a note",
            _ => "pause — I am not sure"
        };
}

public sealed class SuggestionAudit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public string What { get; set; } = string.Empty;

    public string Why { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string? Action { get; set; }

    public string Status { get; set; } = "noted";

    public IReadOnlyList<string> Evidence { get; set; } = [];
}
