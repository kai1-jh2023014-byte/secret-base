using System.Text.Json;

namespace SecretBase.Core.Assistant;

public static class AssistantConfirmationPolicy
{
    public static bool CanAutoExecute(AssistantToolDefinition tool) =>
        (tool.Capability is AssistantToolCapability.ReadOnly
            or AssistantToolCapability.Suggest
            or AssistantToolCapability.SafeAuto)
        && !tool.RequiresConfirmation;

    public static bool RequiresConfirmation(AssistantToolDefinition tool) =>
        tool.Capability == AssistantToolCapability.RequiresConfirmation
        || tool.RequiresConfirmation;

    public static bool IsHostActionOnly(AssistantToolDefinition tool) =>
        tool.Capability == AssistantToolCapability.HostAction;

    public static bool IsRisky(AssistantToolDefinition tool) =>
        tool.RiskLevel >= SecretBase.Core.Security.ActionPrivilege.UserConfirmationRequired
        || string.Equals(tool.Name, AssistantToolNames.CursorOpenProject, StringComparison.OrdinalIgnoreCase);

    public static string Prompt(string toolName, string argumentsJson) =>
        PromptForActions(
        [
            new AssistantPendingAction
            {
                ToolCallId = "single",
                ToolName = toolName,
                ArgumentsJson = argumentsJson,
                Label = Label(toolName, argumentsJson)
            }
        ]);

    public static string PromptForActions(IReadOnlyList<AssistantPendingAction> actions)
    {
        if (actions.Count == 0)
        {
            return "Run this Secret Base action?";
        }

        if (actions.Count == 1)
        {
            return actions[0].Label + "\n\n[Cancel] [Run]";
        }

        var lines = new List<string> { "Run the following actions?", string.Empty };
        foreach (var action in actions)
        {
            lines.Add("✓ " + action.Label);
        }

        lines.Add(string.Empty);
        lines.Add("[Cancel] [Run]");
        return string.Join('\n', lines);
    }

    public static string Label(string toolName, string argumentsJson)
    {
        AssistantToolArgumentValidator.TryParseObject(argumentsJson, out var root, out _);
        return toolName switch
        {
            AssistantToolNames.CreativeOpenProject when
                AssistantToolArgumentValidator.TryGetString(root, "project_id", required: false, out var pid, out _)
                && !string.IsNullOrWhiteSpace(pid) =>
                $"Open Creative Project '{pid}' in Secret Base.",
            AssistantToolNames.CursorOpenProject when
                AssistantToolArgumentValidator.TryGetString(root, "project_id", required: false, out var pid, out _)
                && !string.IsNullOrWhiteSpace(pid) =>
                $"Open project '{pid}' in Cursor.",
            AssistantToolNames.IntegrationOpen when
                AssistantToolArgumentValidator.TryGetString(root, "target", required: false, out var target, out _)
                && !string.IsNullOrWhiteSpace(target) =>
                $"Open {target}.",
            AssistantToolNames.AppsOpen when
                AssistantToolArgumentValidator.TryGetString(root, "name", required: false, out var appName, out _)
                && !string.IsNullOrWhiteSpace(appName) =>
                $"Launch registered app '{appName}'.",
            AssistantToolNames.AppsOpen when
                AssistantToolArgumentValidator.TryGetString(root, "app_id", required: false, out var appId, out _)
                && !string.IsNullOrWhiteSpace(appId) =>
                $"Launch registered app '{appId}'.",
            AssistantToolNames.MusicPlay =>
                "Play in Secret Base music, or open the Spotify page if Premium playback is unavailable.",
            AssistantToolNames.CreativeOpenProject => "Open this Creative Project.",
            AssistantToolNames.CursorOpenProject => "Open this project in Cursor.",
            AssistantToolNames.IntegrationOpen => "Open this integration.",
            AssistantToolNames.AppsOpen => "Launch this registered app.",
            AssistantToolNames.WorkspaceContinue => "Continue the prepared workspace (open the matched project).",
            AssistantToolNames.TodoAdd => "Add this task to Secret Base Todo.",
            AssistantToolNames.CalendarAddEvent => LabelCalendarAdd(root),
            _ => "Run this Secret Base action."
        };
    }

    private static string LabelCalendarAdd(JsonElement root)
    {
        if (root.TryGetProperty("events", out var events)
            && events.ValueKind == JsonValueKind.Array
            && events.GetArrayLength() > 0)
        {
            var parts = new List<string>();
            foreach (var item in events.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                AssistantToolArgumentValidator.TryGetString(item, "title", required: false, out var title, out _);
                AssistantToolArgumentValidator.TryGetInt(item, "hour", -1, -1, 23, out var hour, out _);
                AssistantToolArgumentValidator.TryGetInt(item, "minute", 0, 0, 59, out var minute, out _);
                if (string.IsNullOrWhiteSpace(title) || hour < 0)
                {
                    continue;
                }

                parts.Add(minute == 0 ? $"{hour:00}:00 {title}" : $"{hour:00}:{minute:00} {title}");
            }

            if (parts.Count > 0)
            {
                return $"Add {parts.Count} event(s) to today's local calendar: {string.Join(", ", parts)}.";
            }
        }

        if (AssistantToolArgumentValidator.TryGetString(root, "title", required: false, out var single, out _)
            && !string.IsNullOrWhiteSpace(single))
        {
            return $"Add '{single}' to today's local calendar.";
        }

        return "Add event(s) to today's local calendar.";
    }
}
