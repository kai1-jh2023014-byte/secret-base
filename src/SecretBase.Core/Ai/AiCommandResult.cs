namespace SecretBase.Core.Ai;

/// <summary>Result of an AiCommand. Host performs OS open after validation.</summary>
public sealed class AiCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public AiCommandKind Kind { get; init; }

    public IReadOnlyList<AiToolDefinition> Tools { get; init; } = Array.Empty<AiToolDefinition>();

    public AiToolDefinition? Tool { get; init; }

    /// <summary>When true, host opens <see cref="Url"/> in the system browser.</summary>
    public bool ShouldOpenUrl { get; init; }

    public string? Url { get; init; }

    /// <summary>When true, host launches Cursor at <see cref="FolderPath"/> (narrow Platform API).</summary>
    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? FolderPath { get; init; }

    /// <summary>When true, host launches Cursor without a folder (app only).</summary>
    public bool ShouldOpenCursorApp { get; init; }

    /// <summary>When Cursor is unavailable, UI may offer opening the official website.</summary>
    public bool OfferCursorWebsiteFallback { get; init; }

    public static AiCommandResult Ok(
        AiCommandKind kind,
        IReadOnlyList<AiToolDefinition>? tools = null,
        AiToolDefinition? tool = null,
        bool shouldOpenUrl = false,
        string? url = null,
        bool shouldOpenCursorAtFolder = false,
        string? folderPath = null,
        bool shouldOpenCursorApp = false,
        bool offerCursorWebsiteFallback = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Tools = tools ?? Array.Empty<AiToolDefinition>(),
            Tool = tool,
            ShouldOpenUrl = shouldOpenUrl,
            Url = url,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            FolderPath = folderPath,
            ShouldOpenCursorApp = shouldOpenCursorApp,
            OfferCursorWebsiteFallback = offerCursorWebsiteFallback
        };

    public static AiCommandResult Fail(
        AiCommandKind kind,
        string error,
        bool offerCursorWebsiteFallback = false) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error,
            OfferCursorWebsiteFallback = offerCursorWebsiteFallback
        };
}
