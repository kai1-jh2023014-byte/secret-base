using System.Text.Json;

namespace SecretBase.Core.Widgets.Text;

/// <summary>
/// Text Widget settings. Colors/fonts come from <c>ThemeDefinition</c>, not here.
/// </summary>
public sealed class TextWidgetConfiguration
{
    public const string DefaultPlaceholderText = "Double-click to edit";

    public string Text { get; set; } = DefaultPlaceholderText;

    public double FontSize { get; set; } = 18;

    public TextWidgetAlignment TextAlignment { get; set; } = TextWidgetAlignment.Left;

    public static TextWidgetConfiguration CreateDefault() => new();

    public static TextWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();

        if (configuration.TryGetValue(nameof(Text), out var text) &&
            text.ValueKind == JsonValueKind.String)
        {
            result.Text = text.GetString() ?? DefaultPlaceholderText;
        }

        if (configuration.TryGetValue(nameof(FontSize), out var fontSize) &&
            fontSize.ValueKind == JsonValueKind.Number)
        {
            var size = fontSize.GetDouble();
            if (size > 0)
            {
                result.FontSize = size;
            }
        }

        if (configuration.TryGetValue(nameof(TextAlignment), out var alignment))
        {
            if (alignment.ValueKind == JsonValueKind.String &&
                Enum.TryParse<TextWidgetAlignment>(alignment.GetString(), ignoreCase: true, out var parsed))
            {
                result.TextAlignment = parsed;
            }
            else if (alignment.ValueKind == JsonValueKind.Number &&
                     Enum.IsDefined(typeof(TextWidgetAlignment), alignment.GetInt32()))
            {
                result.TextAlignment = (TextWidgetAlignment)alignment.GetInt32();
            }
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(Text)] = JsonSerializer.SerializeToElement(Text),
            [nameof(FontSize)] = JsonSerializer.SerializeToElement(FontSize),
            [nameof(TextAlignment)] = JsonSerializer.SerializeToElement(TextAlignment.ToString())
        };
    }
}
