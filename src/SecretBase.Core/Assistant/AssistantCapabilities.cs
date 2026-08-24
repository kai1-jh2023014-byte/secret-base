namespace SecretBase.Core.Assistant;

/// <summary>
/// What the LLM may do with a registered tool.
/// HostAction is reserved for Platform launch after confirmation — never LLM-callable.
/// </summary>
public enum AssistantToolCapability
{
    /// <summary>Read-only observation. Auto-run without confirmation.</summary>
    ReadOnly = 0,

    /// <summary>Recommendation text only. Auto-run; never launches Host.</summary>
    Suggest = 1,

    /// <summary>May launch or mutate via Command. Confirmation required before execute.</summary>
    RequiresConfirmation = 2,

    /// <summary>OS/host-only. Must not appear as an LLM-callable tool.</summary>
    HostAction = 3
}

/// <summary>Thin response classification. Not a workflow engine.</summary>
public enum AssistantResponseKind
{
    Answer = 0,
    Suggest = 1,
    Plan = 2,
    RequestConfirmation = 3,
    Execute = 4,
    Error = 5
}

public enum AssistantActivityStatus
{
    Running = 0,
    Done = 1,
    Failed = 2,
    PendingConfirmation = 3
}

/// <summary>User utterance intent. Heuristic only — not an ML classifier.</summary>
public enum AssistantIntentKind
{
    Question = 0,
    RequestInfo = 1,
    Suggestion = 2,
    ActionRequest = 3
}

public enum AssistantPlanStepKind
{
    Read = 0,
    Suggest = 1,
    ConfirmAction = 2
}

public enum AssistantPlanStepStatus
{
    Pending = 0,
    Running = 1,
    Done = 2,
    Failed = 3,
    Cancelled = 4,
    AwaitingConfirmation = 5
}

[Flags]
public enum AssistantContextScope
{
    None = 0,
    Calendar = 1,
    Creative = 2,
    Apps = 4,
    Music = 8,
    Integrations = 16,
    Provider = 32,
    All = Calendar | Creative | Apps | Music | Integrations | Provider
}
