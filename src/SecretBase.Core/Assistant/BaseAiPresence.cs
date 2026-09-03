using SecretBase.Core.Situation;
using SecretBase.Core.State;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Quiet opening for Base AI. Context → understanding → suggestion. Not a chatbot greeting dump.
/// </summary>
public static class BaseAiPresence
{
    public static string Format(
        AssistantProviderStatusInfo? provider,
        UserState? state,
        CurrentSituation? situation)
    {
        var lines = new List<string>
        {
            BaseAiStatusFormatter.FormatShort(provider),
            string.Empty,
            FinishSentence(string.IsNullOrWhiteSpace(state?.Greeting) ? BaseGreeting.For(DateTimeOffset.Now) : state!.Greeting)
        };

        var project = situation?.ProjectName ?? state?.CurrentProjectName;
        if (!string.IsNullOrWhiteSpace(project))
        {
            lines.Add($"You were working on {project} earlier.");
        }
        else if (!string.IsNullOrWhiteSpace(state?.RecentActivityLine))
        {
            lines.Add(state!.RecentActivityLine);
        }

        var next = situation?.NextTask ?? state?.ActiveTodo;
        if (!string.IsNullOrWhiteSpace(next))
        {
            lines.Add(next);
        }
        else if (!string.IsNullOrWhiteSpace(project))
        {
            lines.Add("Continue?");
        }
        else
        {
            lines.Add("Ask about today — launches still confirm.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FinishSentence(string text)
    {
        var value = text.Trim();
        return value.EndsWith('.') || value.EndsWith('?') || value.EndsWith('!') ? value : value + ".";
    }
}
