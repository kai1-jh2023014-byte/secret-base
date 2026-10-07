using System.Text.Json;

namespace SecretBase.Core.Widgets.Progress;

/// <summary>
/// Progress / Genesis widget settings. Optional HTTP URL overrides the local JSON store.
/// </summary>
public sealed class ProgressWidgetConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// When set, Infrastructure fetches the same JSON schema over HTTPS instead of local AppData.
    /// Empty = local JSON (<c>%LocalAppData%\SecretBase\settings\progress-genesis.json</c>).
    /// </summary>
    public string? RemoteUrl { get; set; }

    /// <summary>Auto-refresh interval in seconds (15–600). Default 60.</summary>
    public int RefreshSeconds { get; set; } = 60;

    /// <summary>Show Genesis milestones list (first incomplete + completed count).</summary>
    public bool ShowMilestones { get; set; } = true;

    public static ProgressWidgetConfiguration CreateDefault() => new();

    public static int ClampRefreshSeconds(int value) => Math.Clamp(value, 15, 600);

    public static ProgressWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(SchemaVersion), out var ver)
            && ver.ValueKind == JsonValueKind.Number
            && ver.TryGetInt32(out var schema))
        {
            result.SchemaVersion = schema < 1 ? CurrentSchemaVersion : schema;
        }

        if (configuration.TryGetValue(nameof(RemoteUrl), out var url)
            && url.ValueKind == JsonValueKind.String)
        {
            var raw = url.GetString()?.Trim();
            result.RemoteUrl = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(RefreshSeconds), out var refresh)
            && refresh.ValueKind == JsonValueKind.Number
            && refresh.TryGetInt32(out var seconds))
        {
            result.RefreshSeconds = ClampRefreshSeconds(seconds);
        }

        if (configuration.TryGetValue(nameof(ShowMilestones), out var milestones)
            && (milestones.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.ShowMilestones = milestones.GetBoolean();
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion),
            [nameof(RemoteUrl)] = JsonSerializer.SerializeToElement(RemoteUrl),
            [nameof(RefreshSeconds)] = JsonSerializer.SerializeToElement(ClampRefreshSeconds(RefreshSeconds)),
            [nameof(ShowMilestones)] = JsonSerializer.SerializeToElement(ShowMilestones)
        };
}
