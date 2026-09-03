using SecretBase.Core.Widgets.Web;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Opens https URLs with <c>/usr/bin/open</c> after Core scheme validation.
/// </summary>
public static class MacHttpsLauncher
{
    public static bool TryOpen(string url, Func<string, IReadOnlyList<string>, bool>? start = null)
    {
        if (!WebUrlValidator.TryNormalize(url, out var normalized, out _) || normalized is null)
        {
            return false;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        var launch = start ?? DefaultStart;
        return launch(MacTargetLaunchService.OpenExecutable, new[] { uri.AbsoluteUri });
    }

    private static bool DefaultStart(string fileName, IReadOnlyList<string> arguments)
    {
        var result = MacTargetLaunchService.DefaultStart(fileName, arguments);
        return result.Succeeded;
    }
}
