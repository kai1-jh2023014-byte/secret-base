namespace SecretBase.Core.Assistant;

/// <summary>
/// What the LLM may do with a registered tool.
/// HostAction is reserved for Platform launch after confirmation — never LLM-callable.
/// </summary>
public enum AssistantToolCapability
{
    /// <summary>Read-only observation. Auto-run without confirmation.</summary>
    ReadOnly = 0,

    /// <summary>May launch or mutate via Command. Confirmation required before execute.</summary>
    RequiresConfirmation = 1,

    /// <summary>OS/host-only. Must not appear as an LLM-callable tool.</summary>
    HostAction = 2
}

/// <summary>Thin response classification. Not a workflow engine.</summary>
public enum AssistantResponseKind
{
    Answer = 0,
    Suggest = 1,
    RequestConfirmation = 2,
    Execute = 3,
    Error = 4
}

public enum AssistantActivityStatus
{
    Running = 0,
    Done = 1,
    Failed = 2,
    PendingConfirmation = 3
}
