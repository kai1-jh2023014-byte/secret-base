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

    public const double SizeScaleMin = 0.75;
    public const double SizeScaleMax = 3.0;
    public const double DateScaleMin = 0.5;
    public const double DateScaleMax = 3.0;

    /// <summary>Relative scale for time typography (<see cref="SizeScaleMin"/>–<see cref="SizeScaleMax"/>).</summary>
    public double SizeScale { get; set; } = 1.0;

    /// <summary>Independent scale for date line (<see cref="DateScaleMin"/>–<see cref="DateScaleMax"/>).</summary>
    public double DateScale { get; set; } = 1.0;

    public static ClockWidgetConfiguration CreateDefault() => new();

    public static double ClampSizeScale(double value) =>
        Math.Clamp(value, SizeScaleMin, SizeScaleMax);

    public static double ClampDateScale(double value) =>
        Math.Clamp(value, DateScaleMin, DateScaleMax);

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
            result.SizeScale = ClampSizeScale(scaleValue);
        }

        if (configuration.TryGetValue(nameof(DateScale), out var dateScale) && dateScale.TryGetDouble(out var dateScaleValue))
        {
            result.DateScale = ClampDateScale(dateScaleValue);
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
            [nameof(SizeScale)] = JsonSerializer.SerializeToElement(ClampSizeScale(SizeScale)),
            [nameof(DateScale)] = JsonSerializer.SerializeToElement(ClampDateScale(DateScale))
        };
    }
}
