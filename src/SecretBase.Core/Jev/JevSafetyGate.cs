using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;

namespace SecretBase.Core.Jev;

/// <summary>
/// Turns a Jev decision into a permission. Jev never executes tools.
/// <see cref="AutomationSafety"/> still decides what Secret Base may run.
/// </summary>
public static class JevSafetyGate
{
    public const double LowConfidence = 0.5;

    public static JevSafetyVerdict Evaluate(JevDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.IsValid)
        {
            return new JevSafetyVerdict
            {
                Kind = JevSafetyKind.Deny,
                Reason = decision.InvalidReason ?? "Jev decision failed validation."
            };
        }

        if (decision.Gate == JevGate.Deny)
        {
            return new JevSafetyVerdict
            {
                Kind = JevSafetyKind.Deny,
                Reason = "Jev gate is deny."
            };
        }

        if (decision.NextStep is JevNextStep.None or JevNextStep.Suggest)
        {
            return new JevSafetyVerdict
            {
                Kind = JevSafetyKind.SuggestOnly,
                Reason = "Jev asked for a suggestion only."
            };
        }

        if (decision.Gate == JevGate.Confirm || IsLowConfidence(decision))
        {
            return new JevSafetyVerdict
            {
                Kind = JevSafetyKind.NeedsConfirmation,
                Reason = decision.Gate == JevGate.Confirm
                    ? "Jev gate is confirm."
                    : "Jev confidence is below 0.5, so Secret Base will ask first."
            };
        }

        return new JevSafetyVerdict
        {
            Kind = JevSafetyKind.Allow,
            Reason = "Jev allowed a safe next step. Existing confirmation policy still applies."
        };
    }

    public static JevToolPermission PermissionFor(JevSafetyVerdict verdict, AssistantToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(tool);
        if (AutomationSafety.ForTool(tool) == AutomationSafetyLevel.Denied
            || AssistantConfirmationPolicy.IsHostActionOnly(tool))
        {
            return JevToolPermission.Refuse;
        }

        var observation = tool.Capability is AssistantToolCapability.ReadOnly or AssistantToolCapability.Suggest;
        if (verdict.Kind == JevSafetyKind.Deny)
        {
            return observation ? JevToolPermission.Run : JevToolPermission.Refuse;
        }

        if (AssistantConfirmationPolicy.RequiresConfirmation(tool)
            || AutomationSafety.ForTool(tool) >= AutomationSafetyLevel.ConfirmationRequired)
        {
            return JevToolPermission.Confirm;
        }

        if (verdict.Kind is JevSafetyKind.NeedsConfirmation or JevSafetyKind.SuggestOnly)
        {
            return observation ? JevToolPermission.Run : JevToolPermission.Confirm;
        }

        return AutomationSafety.CanRunWithoutPrompt(tool)
            ? JevToolPermission.Run
            : JevToolPermission.Confirm;
    }

    public static bool MayAutoExecute(JevSafetyVerdict verdict, AssistantToolDefinition tool) =>
        PermissionFor(verdict, tool) == JevToolPermission.Run;

    private static bool IsLowConfidence(JevDecision decision)
    {
        foreach (var value in new[] { decision.SituationConfidence, decision.NextStepConfidence, decision.GateConfidence })
        {
            if (value is < LowConfidence)
            {
                return true;
            }
        }

        return false;
    }
}
