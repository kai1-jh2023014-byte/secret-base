using System.Text.Json;

namespace SecretBase.Core.Widgets.Workspace;

public sealed class WorkspaceWidgetConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public static WorkspaceWidgetConfiguration CreateDefault() => new();

    public static WorkspaceWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(SchemaVersion), out var ver)
            && ver.ValueKind == JsonValueKind.Number
            && ver.TryGetInt32(out var schema))
        {
            result.SchemaVersion = schema < 1 ? CurrentSchemaVersion : schema;
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion)
        };
}
