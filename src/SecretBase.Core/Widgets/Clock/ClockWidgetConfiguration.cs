using System.Text.Json;

namespace SecretBase.Core.Widgets.Clock;

/// <summary>
/// Clock-specific settings. v0.1 keeps 24-hour format fixed in the UI,
/// but the model already reserves toggles for later.
/// </summary>
public sealed class ClockWidgetConfiguration
{
    public bool Use24HourFormat { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public bool ShowDate { get; set; } = true;

    public static ClockWidgetConfiguration CreateDefault() => new();

    public static ClockWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(Use24HourFormat), out var use24) &&
            (use24.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.Use24HourFormat = use24.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(ShowSeconds), out var showSeconds) &&
            (showSeconds.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.ShowSeconds = showSeconds.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(ShowDate), out var showDate) &&
            (showDate.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.ShowDate = showDate.GetBoolean();
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(Use24HourFormat)] = JsonSerializer.SerializeToElement(Use24HourFormat),
            [nameof(ShowSeconds)] = JsonSerializer.SerializeToElement(ShowSeconds),
            [nameof(ShowDate)] = JsonSerializer.SerializeToElement(ShowDate)
        };
    }
}
