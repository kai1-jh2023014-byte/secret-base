using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SecretBase.Core.Connectors;

/// <summary>
/// Builds an HTTP request only from a declared endpoint. Callers cannot supply a URL.
/// </summary>
public static class HttpEndpointPolicy
{
    public const int DefaultTimeoutMs = 5_000;
    public const int MaxTimeoutMs = 15_000;
    public const int DefaultMaxBytes = 65_536;
    public const int AbsoluteMaxBytes = 262_144;

    private static readonly Regex TokenPattern = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

    public static bool TryCreateRequest(
        IntegrationManifest manifest,
        IntegrationEndpointDeclaration endpoint,
        JsonElement arguments,
        out TransportRequest? request,
        out string error)
    {
        request = null;
        error = string.Empty;

        if (HasForbiddenCallerOverride(arguments))
        {
            error = "Arbitrary URL, host, method, or path is not allowed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.BaseUrl)
            || !Uri.TryCreate(manifest.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            error = "Base URL is not configured.";
            return false;
        }

        if (!IsHostAllowed(manifest, baseUri.Host))
        {
            error = "Host is not allowed.";
            return false;
        }

        if (!IsSchemeAllowed(manifest, baseUri.Scheme, baseUri.Host))
        {
            error = "Scheme is not allowed.";
            return false;
        }

        if (!TryBindPath(endpoint.Path, arguments, out var path, out error))
        {
            return false;
        }

        if (!Uri.TryCreate(baseUri, path, out var url)
            || !string.Equals(url.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(url.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(url.UserInfo)
            || url.PathAndQuery.Contains("..", StringComparison.Ordinal))
        {
            error = "Resolved URL is not allowed.";
            return false;
        }

        string? body = null;
        if (!string.Equals(endpoint.Method, "GET", StringComparison.OrdinalIgnoreCase)
            && arguments.ValueKind is JsonValueKind.Object)
        {
            body = arguments.GetRawText();
            if (Encoding.UTF8.GetByteCount(body) > endpoint.MaxResponseBytes)
            {
                error = "Request body is too large.";
                return false;
            }
        }

        request = new TransportRequest(
            IntegrationId: manifest.Id,
            Method: endpoint.Method.ToUpperInvariant(),
            Url: url,
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: body,
            Timeout: TimeSpan.FromMilliseconds(Math.Clamp(endpoint.TimeoutMs, 250, MaxTimeoutMs)),
            MaxResponseBytes: Math.Clamp(endpoint.MaxResponseBytes, 256, AbsoluteMaxBytes));
        return true;
    }

    public static bool IsHostAllowed(IntegrationManifest manifest, string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (!manifest.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsLoopbackHost(host))
        {
            return manifest.AllowLoopback || manifest.Transport == IntegrationTransportKind.Local;
        }

        // Named private hosts are allowed only when the user registered that exact host.
        // Wildcards and unresolved hosts are rejected.
        return host.Contains('.', StringComparison.Ordinal) || IPAddress.TryParse(host, out _);
    }

    public static bool IsSchemeAllowed(IntegrationManifest manifest, string scheme, string host)
    {
        if (string.IsNullOrWhiteSpace(scheme)
            || !manifest.AllowedSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && IsLoopbackHost(host);
    }

    public static bool IsMethodAllowed(IntegrationEndpointDeclaration endpoint, string method) =>
        string.Equals(endpoint.Method, method, StringComparison.OrdinalIgnoreCase);

    public static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }

    public static bool IsPrivateNetworkHost(string host)
    {
        if (IsLoopbackHost(host))
        {
            return true;
        }

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
               || bytes[0] == 127
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
               || (bytes[0] == 192 && bytes[1] == 168)
               || (bytes[0] == 169 && bytes[1] == 254);
    }

    public static bool HasForbiddenCallerOverride(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in arguments.EnumerateObject())
        {
            if (property.NameEquals("url")
                || property.NameEquals("uri")
                || property.NameEquals("href")
                || property.NameEquals("host")
                || property.NameEquals("hostname")
                || property.NameEquals("baseUrl")
                || property.NameEquals("method")
                || property.NameEquals("path")
                || property.NameEquals("scheme")
                || property.NameEquals("authorization")
                || property.NameEquals("apiKey")
                || property.NameEquals("token"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBindPath(
        string template,
        JsonElement arguments,
        out string path,
        out string error)
    {
        path = template;
        error = string.Empty;
        var matches = Regex.Matches(template, "\\{([A-Za-z0-9_]+)\\}");
        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (arguments.ValueKind != JsonValueKind.Object
                || !arguments.TryGetProperty(name, out var value)
                || value.ValueKind != JsonValueKind.String
                || !TokenPattern.IsMatch(value.GetString() ?? string.Empty))
            {
                error = "Path parameter is missing or invalid.";
                path = string.Empty;
                return false;
            }

            path = path.Replace("{" + name + "}", value.GetString(), StringComparison.Ordinal);
        }

        if (!IntegrationManifestValidator.IsSafePath(path.Contains('{') ? template : path) && path.Contains('{'))
        {
            error = "Path template is invalid.";
            return false;
        }

        if (path.Contains('{', StringComparison.Ordinal))
        {
            error = "Path parameter is missing or invalid.";
            return false;
        }

        return IntegrationManifestValidator.IsSafePath(path);
    }
}

public static class DeepLinkPolicy
{
    private static readonly HashSet<string> ForbiddenSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "javascript", "data", "vbscript", "ms-msdt", "ms-search", "shell", "cmd", "powershell"
    };

    public static bool IsValidDeclaration(IntegrationDeepLinkDeclaration declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration.Scheme)
            || declaration.Scheme.Length > 32
            || !declaration.Scheme.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-')
            || ForbiddenSchemes.Contains(declaration.Scheme)
            || declaration.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || declaration.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (declaration.PathPrefix is { Length: > 0 } && !declaration.PathPrefix.StartsWith('/'))
        {
            return false;
        }

        return true;
    }

    public static bool TryValidate(
        IntegrationManifest manifest,
        string? uri,
        out Uri? parsed,
        out string error)
    {
        parsed = null;
        error = string.Empty;
        if (manifest.DeepLink is null || !IsValidDeclaration(manifest.DeepLink))
        {
            error = "Deep link is not declared.";
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var candidate) || candidate.IsFile)
        {
            error = "Deep link is invalid.";
            return false;
        }

        if (!string.Equals(candidate.Scheme, manifest.DeepLink.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            error = "Deep link scheme is not allowed.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(manifest.DeepLink.Host)
            && !string.Equals(candidate.Host, manifest.DeepLink.Host, StringComparison.OrdinalIgnoreCase))
        {
            error = "Deep link host is not allowed.";
            return false;
        }

        var prefix = manifest.DeepLink.PathPrefix ?? "/";
        if (!candidate.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "Deep link path is not allowed.";
            return false;
        }

        parsed = candidate;
        return true;
    }
}
