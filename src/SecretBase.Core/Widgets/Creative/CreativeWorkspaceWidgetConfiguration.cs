using System.Text.Json;

namespace SecretBase.Core.Widgets.Creative;

/// <summary>
/// Widget-local settings only (UI preferences). Items live in Creative workspace.json.
/// </summary>
public sealed class CreativeWorkspaceWidgetConfiguration
{
    /// <summary>When true, Add Folder registers as Project.</summary>
    public bool AddFoldersAsProjects { get; set; }

    public static CreativeWorkspaceWidgetConfiguration CreateDefault() => new();

    public static CreativeWorkspaceWidgetConfiguration FromDictionary(
        IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(AddFoldersAsProjects), out var el)
            && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result.AddFoldersAsProjects = el.GetBoolean();
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(AddFoldersAsProjects)] = JsonSerializer.SerializeToElement(AddFoldersAsProjects)
        };
}
