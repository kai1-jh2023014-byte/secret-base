using System.Text.Json;

namespace SecretBase.Core.Widgets.Clock;

/// <summary>
/// Clock-specific settings. Supports multiple display styles and format toggles.
/// Styles: Base (status), Large digital, Minimal, Analog, Focus.
/// </summary>
public sealed class ClockWidgetConfiguration
{
    public const string StyleDigital = "digital";
    public const string StyleLarge = "large";
    public const string StyleMinimal = "minimal";
    public const string StyleAnalog = "analog";
    public const string StyleFocus = "focus";
    public const string StyleBase = "base";

    public static readonly IReadOnlyList<string> KnownStyles =
    [
        StyleBase,
        StyleLarge,
        StyleDigital,
        StyleMinimal,
        StyleAnalog,
        StyleFocus
    ];

    public string DisplayStyle { get; set; } = StyleBase;

    public bool Use24HourFormat { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public bool ShowDate { get; set; } = true;

    /// <summary>Relative scale hint for typography (0.75–1.5).</summary>
    public double SizeScale { get; set; } = 1.0;

    public static ClockWidgetConfiguration CreateDefault() => new();

    public static bool IsKnownStyle(string? style) =>
        !string.IsNullOrWhiteSpace(style)
        && KnownStyles.Contains(style.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>True for large typography faces (digital / large).</summary>
    public static bool IsLargeTypography(string? style) =>
        string.Equals(style, StyleLarge, StringComparison.Ordinal)
        || string.Equals(style, StyleDigital, StringComparison.Ordinal);

    public static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return StyleBase;
        }

        var normalized = style.Trim().ToLowerInvariant();
        // Older layouts used "digital" for the large typography face.
        if (normalized == StyleDigital)
        {
            return StyleLarge;
        }

        return IsKnownStyle(normalized) ? normalized : StyleBase;
    }

    public static ClockWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(DisplayStyle), out var style) && style.ValueKind == JsonValueKind.String)
        {
            result.DisplayStyle = NormalizeStyle(style.GetString());
        }

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

        if (configuration.TryGetValue(nameof(SizeScale), out var scale) && scale.TryGetDouble(out var scaleValue))
        {
            result.SizeScale = Math.Clamp(scaleValue, 0.75, 1.5);
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(DisplayStyle)] = JsonSerializer.SerializeToElement(NormalizeStyle(DisplayStyle)),
            [nameof(Use24HourFormat)] = JsonSerializer.SerializeToElement(Use24HourFormat),
            [nameof(ShowSeconds)] = JsonSerializer.SerializeToElement(ShowSeconds),
            [nameof(ShowDate)] = JsonSerializer.SerializeToElement(ShowDate),
            [nameof(SizeScale)] = JsonSerializer.SerializeToElement(SizeScale)
        };
    }
}
