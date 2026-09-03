using SecretBase.Core.Assistant;
using SecretBase.Core.Security;

namespace SecretBase.Core.Automation;

/// <summary>
/// Secret Base automation lanes. Maps onto existing confirmation / step-budget tools.
/// </summary>
public enum AutomationSafetyLevel
{
    /// <summary>Display, context, timer, suggestions, workspace_prepare.</summary>
    SafeAuto = 0,

    /// <summary>App launch, open file, git, external writes.</summary>
    ConfirmationRequired = 1,

    /// <summary>Delete, send, purchase, critical settings.</summary>
    ExplicitConfirmationRequired = 2,

    Denied = 3
}

public static class AutomationSafety
{
    public static AutomationSafetyLevel ForTool(AssistantToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (AssistantConfirmationPolicy.IsHostActionOnly(tool)
            || tool.RiskLevel == ActionPrivilege.RestrictedAction)
        {
            return AutomationSafetyLevel.Denied;
        }

        if (string.Equals(tool.Name, AssistantToolNames.FilesDelete, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tool.Name, AssistantToolNames.WorkspaceRemove, StringComparison.OrdinalIgnoreCase)
            || tool.RiskLevel >= ActionPrivilege.UserConfirmationRequired)
        {
            return AutomationSafetyLevel.ExplicitConfirmationRequired;
        }

        if (tool.Capability == AssistantToolCapability.RequiresConfirmation
            || tool.RequiresConfirmation)
        {
            return AutomationSafetyLevel.ConfirmationRequired;
        }

        return AutomationSafetyLevel.SafeAuto;
    }

    public static bool CanRunWithoutPrompt(AssistantToolDefinition tool) =>
        ForTool(tool) == AutomationSafetyLevel.SafeAuto
        && AssistantConfirmationPolicy.CanAutoExecute(tool);
}
