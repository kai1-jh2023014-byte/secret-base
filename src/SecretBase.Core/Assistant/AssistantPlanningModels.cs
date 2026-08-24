namespace SecretBase.Core.Assistant;

/// <summary>One planned step. Not a workflow engine node.</summary>
public sealed class AssistantPlanStep
{
    public int Index { get; init; }

    public required string Title { get; init; }

    public AssistantPlanStepKind Kind { get; init; }

    public string? ToolName { get; init; }

    public bool RequiresConfirmation { get; init; }

    public AssistantPlanStepStatus Status { get; init; } = AssistantPlanStepStatus.Pending;
}

/// <summary>Thin plan shown in the widget. Cap steps via <see cref="AssistantSettings.MaxSteps"/>.</summary>
public sealed class AssistantPlan
{
    public required string Summary { get; init; }

    public IReadOnlyList<AssistantPlanStep> Steps { get; init; } = Array.Empty<AssistantPlanStep>();

    public string FormatForUi()
    {
        var lines = new List<string> { "Plan:", Summary };
        foreach (var step in Steps)
        {
            var kind = step.Kind switch
            {
                AssistantPlanStepKind.Read => "[Read]",
                AssistantPlanStepKind.Suggest => "[Suggest]",
                AssistantPlanStepKind.ConfirmAction => "[Action]",
                _ => "[Step]"
            };
            var mark = step.Status switch
            {
                AssistantPlanStepStatus.Done => "✓",
                AssistantPlanStepStatus.Failed => "✗",
                AssistantPlanStepStatus.AwaitingConfirmation => "→",
                AssistantPlanStepStatus.Running => "…",
                AssistantPlanStepStatus.Cancelled => "–",
                _ => "·"
            };
            var confirm = step.RequiresConfirmation ? " (confirm)" : string.Empty;
            lines.Add($"{mark} {step.Index}. {kind} {step.Title}{confirm}");
        }

        return string.Join('\n', lines);
    }
}

/// <summary>User-facing result of one confirmed tool. No secrets.</summary>
public sealed class AssistantActionResult
{
    public required string ToolName { get; init; }

    public required string Label { get; init; }

    public bool Succeeded { get; init; }

    public required string Message { get; init; }

    public string? Reason { get; init; }

    public bool CanRetry { get; init; }
}

/// <summary>One action waiting for Cancel/Run.</summary>
public sealed class AssistantPendingAction
{
    public required string ToolCallId { get; init; }

    public required string ToolName { get; init; }

    public required string ArgumentsJson { get; init; }

    public required string Label { get; init; }
}
