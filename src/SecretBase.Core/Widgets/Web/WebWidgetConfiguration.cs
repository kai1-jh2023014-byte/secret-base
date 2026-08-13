using System.Text.Json;

namespace SecretBase.Core.Widgets.Web;

/// <summary>
/// Web Widget settings. The page itself is untrusted; this bag only stores the URL.
/// Colors/fonts come from <c>ThemeDefinition</c>, not here.
/// </summary>
public sealed class WebWidgetConfiguration
{
    public const string DefaultUrl = "https://www.youtube.com/";

    public string Url { get; set; } = DefaultUrl;

    public static WebWidgetConfiguration CreateDefault() => new();

    public static WebWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();

        if (configuration.TryGetValue(nameof(Url), out var url) &&
            url.ValueKind == JsonValueKind.String)
        {
            var raw = url.GetString();
            if (WebUrlValidator.TryNormalize(raw, out var normalized, out _))
            {
                result.Url = normalized!;
            }
            else
            {
                // Keep a safe default rather than persisting a dangerous scheme.
                result.Url = DefaultUrl;
            }
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        var url = WebUrlValidator.TryNormalize(Url, out var normalized, out _)
            ? normalized!
            : DefaultUrl;

        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(Url)] = JsonSerializer.SerializeToElement(url)
        };
    }
}
