using System.Text.Json;

namespace SecretBase.Core.Assistant;

/// <summary>Validates tool JSON arguments. Rejects paths, schemes, and unknown tools.</summary>
public static class AssistantToolArgumentValidator
{
    public const int MaxJsonLength = 4000;
    public const int MaxStringLength = 200;

    public static bool TryParseObject(string? json, out JsonElement root, out string error)
    {
        root = default;
        error = string.Empty;
        var raw = string.IsNullOrWhiteSpace(json) ? "{}" : json.Trim();
        if (raw.Length > MaxJsonLength)
        {
            error = AssistantUserMessages.ToolUnavailable;
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Tool arguments must be a JSON object.";
                return false;
            }

            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            error = "Tool arguments are not valid JSON.";
            return false;
        }
    }

    public static bool TryGetString(JsonElement root, string name, bool required, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                error = $"Missing {name}.";
                return false;
            }

            return true;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            error = $"{name} must be a string.";
            return false;
        }

        var text = el.GetString()?.Trim() ?? string.Empty;
        if (text.Length > MaxStringLength)
        {
            error = $"{name} is too long.";
            return false;
        }

        if (LooksLikePathOrCommand(text))
        {
            error = AssistantUserMessages.ToolUnavailable;
            return false;
        }

        value = text;
        return true;
    }

    public static bool TryGetInt(JsonElement root, string name, int fallback, int min, int max, out int value, out string error)
    {
        value = fallback;
        error = string.Empty;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            value = Math.Clamp(n, min, max);
            return true;
        }

        error = $"{name} must be an integer.";
        return false;
    }

    public static bool LooksLikePathOrCommand(string text) =>
        text.Contains("://", StringComparison.Ordinal)
        || text.Contains("..", StringComparison.Ordinal)
        || text.StartsWith('\\')
        || text.StartsWith('/')
        || text.Contains('|')
        || text.Contains('&')
        || text.Contains(".exe ", StringComparison.OrdinalIgnoreCase);
}
