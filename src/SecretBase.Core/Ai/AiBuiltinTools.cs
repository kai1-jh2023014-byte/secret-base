using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Ai;

/// <summary>Built-in AI tool catalog (MVP). Not a plugin registry.</summary>
public static class AiBuiltinTools
{
    public const string CursorId = "cursor";
    public const string ChatGptId = "chatgpt";
    public const string ClaudeId = "claude";
    public const string GeminiId = "gemini";

    public const string CursorWebsite = "https://cursor.com/";
    public const string ChatGptWebsite = "https://chatgpt.com/";
    public const string ClaudeWebsite = "https://claude.ai/";
    public const string GeminiWebsite = "https://gemini.google.com/";

    private static readonly IReadOnlyList<AiToolDefinition> All =
    [
        new AiToolDefinition
        {
            Id = CursorId,
            DisplayName = "Cursor",
            Kind = AiToolKind.Cursor,
            OfficialWebsiteUrl = CursorWebsite,
            IsDesktopApp = true,
            Glyph = "💻"
        },
        new AiToolDefinition
        {
            Id = ChatGptId,
            DisplayName = "ChatGPT",
            Kind = AiToolKind.ChatGpt,
            OfficialWebsiteUrl = ChatGptWebsite,
            IsDesktopApp = false,
            Glyph = "🌐"
        },
        new AiToolDefinition
        {
            Id = ClaudeId,
            DisplayName = "Claude",
            Kind = AiToolKind.Claude,
            OfficialWebsiteUrl = ClaudeWebsite,
            IsDesktopApp = false,
            Glyph = "🧠"
        },
        new AiToolDefinition
        {
            Id = GeminiId,
            DisplayName = "Gemini",
            Kind = AiToolKind.Gemini,
            OfficialWebsiteUrl = GeminiWebsite,
            IsDesktopApp = false,
            Glyph = "✨"
        }
    ];

    public static IReadOnlyList<AiToolDefinition> Catalog => All;

    public static AiToolDefinition? FindById(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool TryGetOfficialWebsite(string? toolId, out string url, out string error)
    {
        url = string.Empty;
        error = string.Empty;
        var tool = FindById(toolId);
        if (tool is null)
        {
            error = "Unknown AI tool.";
            return false;
        }

        if (!WebUrlValidator.TryNormalize(tool.OfficialWebsiteUrl, out var normalized, out var urlError)
            || normalized is null)
        {
            error = urlError ?? WebUrlValidator.BlockedMessage;
            return false;
        }

        url = normalized;
        return true;
    }
}
