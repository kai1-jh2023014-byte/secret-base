namespace SecretBase.Core.Assistant;

public static class AssistantConfirmationPolicy
{
    public static bool CanAutoExecute(AssistantToolDefinition tool) =>
        tool.Capability == AssistantToolCapability.ReadOnly && !tool.RequiresConfirmation;

    public static bool RequiresConfirmation(AssistantToolDefinition tool) =>
        tool.Capability == AssistantToolCapability.RequiresConfirmation
        || tool.RequiresConfirmation;

    public static bool IsHostActionOnly(AssistantToolDefinition tool) =>
        tool.Capability == AssistantToolCapability.HostAction;

    public static string Prompt(string toolName, string argumentsJson)
    {
        AssistantToolArgumentValidator.TryParseObject(argumentsJson, out var root, out _);
        return toolName switch
        {
            AssistantToolNames.CreativeOpenProject when
                AssistantToolArgumentValidator.TryGetString(root, "project_id", required: false, out var pid, out _)
                && !string.IsNullOrWhiteSpace(pid) =>
                $"Open Creative Project '{pid}' in Secret Base?",
            AssistantToolNames.CursorOpenProject when
                AssistantToolArgumentValidator.TryGetString(root, "project_id", required: false, out var pid, out _)
                && !string.IsNullOrWhiteSpace(pid) =>
                $"Open project '{pid}' in Cursor?",
            AssistantToolNames.IntegrationOpen when
                AssistantToolArgumentValidator.TryGetString(root, "target", required: false, out var target, out _)
                && !string.IsNullOrWhiteSpace(target) =>
                $"Open {target}?",
            AssistantToolNames.AppsOpen when
                AssistantToolArgumentValidator.TryGetString(root, "app_id", required: false, out var appId, out _)
                && !string.IsNullOrWhiteSpace(appId) =>
                $"Launch registered app '{appId}'?",
            AssistantToolNames.MusicPlay => "Play this track in the Secret Base music catalog?",
            AssistantToolNames.CreativeOpenProject => "Open this Creative Project?",
            AssistantToolNames.CursorOpenProject => "Open this project in Cursor?",
            AssistantToolNames.IntegrationOpen => "Open this integration?",
            AssistantToolNames.AppsOpen => "Launch this registered app?",
            _ => "Run this Secret Base action?"
        };
    }
}
