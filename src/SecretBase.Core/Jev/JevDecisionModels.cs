using SecretBase.Core.Assistant;

namespace SecretBase.Core.Jev;

/// <summary>What the user seems to be doing. Closed set sent as a Jev choice question.</summary>
public enum JevSituation
{
    Unknown = 0,
    Study,
    Coding,
    Creative,
    Music,
    Communication,
    Entertainment,
    Break
}

/// <summary>What Secret Base may prepare. Not a shell command and not a file path.</summary>
public enum JevNextStep
{
    None = 0,
    Suggest,
    PrepareWorkspace,
    ContinueWorkspace,
    StartFocus,
    AskConfirmation
}

/// <summary>Whether automation may be offered. Never an execution command.</summary>
public enum JevGate
{
    Allow = 0,
    Confirm,
    Deny
}

public enum JevSafetyKind
{
    Allow = 0,
    SuggestOnly,
    NeedsConfirmation,
    Deny
}

public enum JevToolPermission
{
    Run = 0,
    Confirm,
    Refuse
}

public enum JevClientFailure
{
    None = 0,
    NotConfigured,
    Authentication,
    PaymentRequired,
    Forbidden,
    InvalidRequest,
    Unavailable,
    Timeout,
    Network
}

/// <summary>Minimal context for one decision. No secrets and no file paths.</summary>
public sealed class JevObservation
{
    public DateTimeOffset LocalNow { get; init; }

    public AssistantIntentKind Intent { get; init; }

    public string Message { get; init; } = string.Empty;

    public int? CalendarTodayCount { get; init; }

    public int? TodoCount { get; init; }

    public string? WorkspaceName { get; init; }
}

/// <summary>Validated decision. Invalid results must not be executed.</summary>
public sealed class JevDecision
{
    public bool IsValid { get; init; }

    public string? InvalidReason { get; init; }

    public string? RejectedValue { get; init; }

    public JevSituation Situation { get; init; }

    public JevNextStep NextStep { get; init; }

    public JevGate Gate { get; init; }

    public double? SituationConfidence { get; init; }

    public double? NextStepConfidence { get; init; }

    public double? GateConfidence { get; init; }

    public string? Model { get; init; }

    public bool FromFallback { get; init; }

    public string Explain()
    {
        if (!IsValid)
        {
            return "Jev returned a decision outside the allowed set. Secret Base will not run it.";
        }

        var source = FromFallback ? "Local rule (Jev unavailable)" : "Jev";
        return $"{source}: situation={ToWire(Situation)}, next={ToWire(NextStep)}, gate={ToWire(Gate)}. "
               + "This does not run the PC. Secret Base still applies its safety gate.";
    }

    internal static string ToWire(JevSituation value) => value switch
    {
        JevSituation.Study => "study",
        JevSituation.Coding => "coding",
        JevSituation.Creative => "creative",
        JevSituation.Music => "music",
        JevSituation.Communication => "communication",
        JevSituation.Entertainment => "entertainment",
        JevSituation.Break => "break",
        _ => "unknown"
    };

    internal static string ToWire(JevNextStep value) => value switch
    {
        JevNextStep.Suggest => "suggest",
        JevNextStep.PrepareWorkspace => "prepare_workspace",
        JevNextStep.ContinueWorkspace => "continue_workspace",
        JevNextStep.StartFocus => "start_focus",
        JevNextStep.AskConfirmation => "ask_confirmation",
        _ => "none"
    };

    internal static string ToWire(JevGate value) => value switch
    {
        JevGate.Confirm => "confirm",
        JevGate.Deny => "deny",
        _ => "allow"
    };
}

public sealed class JevSafetyVerdict
{
    public required JevSafetyKind Kind { get; init; }

    public required string Reason { get; init; }
}

public sealed class JevDecisionOutcome
{
    public required JevDecision Decision { get; init; }

    public required JevSafetyVerdict Verdict { get; init; }

    public bool UsedFallback { get; init; }

    public string? Error { get; init; }

    public string Summary =>
        UsedFallback
            ? "Jev unavailable — local rule, no automatic action"
            : Decision.IsValid
                ? $"Jev {JevDecision.ToWire(Decision.Situation)} / {JevDecision.ToWire(Decision.NextStep)} / {JevDecision.ToWire(Decision.Gate)}"
                : "Jev decision rejected";
}

public sealed class JevClientResult
{
    public bool Succeeded { get; init; }

    public int? StatusCode { get; init; }

    public string? Body { get; init; }

    public string? ErrorMessage { get; init; }

    public JevClientFailure Failure { get; init; }

    public static JevClientResult Ok(string body) =>
        new() { Succeeded = true, StatusCode = 200, Body = body };

    public static JevClientResult Fail(JevClientFailure failure, string message, int? statusCode = null) =>
        new()
        {
            Succeeded = false,
            Failure = failure,
            ErrorMessage = message,
            StatusCode = statusCode
        };
}

public sealed class JevConnectionTest
{
    public bool Succeeded { get; init; }

    public bool NeedsConfiguration { get; init; }

    public string Message { get; init; } = string.Empty;

    public static JevConnectionTest Ok(string? model) =>
        new()
        {
            Succeeded = true,
            Message = string.IsNullOrWhiteSpace(model)
                ? JevUserMessages.Connected
                : JevUserMessages.Connected + " Model " + model + "."
        };

    public static JevConnectionTest NotConfigured() =>
        new() { NeedsConfiguration = true, Message = JevUserMessages.NotConfigured };

    public static JevConnectionTest Fail(string message) =>
        new() { Message = message };
}
