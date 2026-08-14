namespace SecretBase.Core.Ai;

/// <summary>
/// Built-in AI tool definition. Web tools open official https; Cursor uses Platform launch.
/// </summary>
public sealed class AiToolDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required AiToolKind Kind { get; init; }

    /// <summary>Official website (http/https). Used for web tools and Cursor fallback.</summary>
    public required string OfficialWebsiteUrl { get; init; }

    /// <summary>When true, Open may launch a local app (Cursor) instead of only the website.</summary>
    public bool IsDesktopApp { get; init; }

    public string Glyph { get; init; } = "🤖";
}
