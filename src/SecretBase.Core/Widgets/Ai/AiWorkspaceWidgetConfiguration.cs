using System.Text.Json;
using SecretBase.Core.Ai;

namespace SecretBase.Core.Widgets.Ai;

/// <summary>
/// AI Workspace widget prefs (enabled built-in tools). No credentials / tokens.
/// </summary>
public sealed class AiWorkspaceWidgetConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Enabled built-in tool ids (cursor, chatgpt, claude, gemini).</summary>
    public List<string> EnabledToolIds { get; set; } = AiBuiltinTools.Catalog.Select(t => t.Id).ToList();

    public static AiWorkspaceWidgetConfiguration CreateDefault() => new();

    public IReadOnlyList<AiToolDefinition> GetEnabledTools()
    {
        var enabled = new HashSet<string>(EnabledToolIds, StringComparer.OrdinalIgnoreCase);
        return AiBuiltinTools.Catalog.Where(t => enabled.Contains(t.Id)).ToList();
    }

    public static AiWorkspaceWidgetConfiguration FromDictionary(
        IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(SchemaVersion), out var ver)
            && ver.ValueKind == JsonValueKind.Number
            && ver.TryGetInt32(out var schema))
        {
            result.SchemaVersion = schema < 1 ? CurrentSchemaVersion : schema;
        }

        if (configuration.TryGetValue(nameof(EnabledToolIds), out var tools)
            && tools.ValueKind == JsonValueKind.Array)
        {
            var ids = new List<string>();
            foreach (var el in tools.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var id = el.GetString();
                if (!string.IsNullOrWhiteSpace(id) && AiBuiltinTools.FindById(id) is not null)
                {
                    ids.Add(id.Trim().ToLowerInvariant());
                }
            }

            if (ids.Count > 0)
            {
                result.EnabledToolIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion),
            [nameof(EnabledToolIds)] = JsonSerializer.SerializeToElement(EnabledToolIds)
        };
}
