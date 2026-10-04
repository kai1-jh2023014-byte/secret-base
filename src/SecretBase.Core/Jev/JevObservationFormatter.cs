using System.Text;

namespace SecretBase.Core.Jev;

/// <summary>Builds the Jev <c>state</c> string. Omits secrets, paths, and chat history.</summary>
public static class JevObservationFormatter
{
    public const int MaxMessageChars = 240;

    public const int MaxStateChars = 800;

    public static string Format(JevObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var builder = new StringBuilder();
        var local = observation.LocalNow;
        builder.Append("time: ").Append(local.ToString("yyyy-MM-dd HH:mm")).Append('\n');
        builder.Append("intent: ").Append(observation.Intent).Append('\n');
        if (observation.CalendarTodayCount is int calendar)
        {
            builder.Append("calendar_today_count: ").Append(calendar).Append('\n');
        }

        if (observation.TodoCount is int todos)
        {
            builder.Append("todo_count: ").Append(todos).Append('\n');
        }

        var workspace = SanitizeLabel(observation.WorkspaceName);
        if (workspace is not null)
        {
            builder.Append("workspace: ").Append(workspace).Append('\n');
        }

        builder.Append("message: ").Append(SanitizeMessage(observation.Message));
        var text = builder.ToString();
        return text.Length <= MaxStateChars ? text : text[..MaxStateChars];
    }

    public static string SanitizeMessage(string? message)
    {
        var text = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || LooksSensitive(text))
        {
            return "[redacted]";
        }

        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= MaxMessageChars ? text : text[..MaxMessageChars];
    }

    public static string? SanitizeLabel(string? label)
    {
        var text = label?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || LooksSensitive(text) || LooksLikePath(text))
        {
            return null;
        }

        return text.Length <= 80 ? text : text[..80];
    }

    private static bool LooksLikePath(string text) =>
        text.Contains('\\', StringComparison.Ordinal)
        || text.Contains("://", StringComparison.Ordinal)
        || text.Contains('/', StringComparison.Ordinal)
        || text.Contains(':', StringComparison.Ordinal);

    internal static bool LooksSensitive(string text) =>
        text.Contains("sk-", StringComparison.OrdinalIgnoreCase)
        || text.Contains("AIza", StringComparison.Ordinal)
        || text.Contains("jv_live_", StringComparison.Ordinal)
        || text.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
        || text.Contains("api_key", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Bearer ", StringComparison.Ordinal)
        || text.Contains("password", StringComparison.OrdinalIgnoreCase)
        || text.Contains("-----BEGIN", StringComparison.Ordinal);
}
