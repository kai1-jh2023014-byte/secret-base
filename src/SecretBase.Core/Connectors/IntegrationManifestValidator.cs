using System.Text.Json;
using System.Text.RegularExpressions;

namespace SecretBase.Core.Connectors;

public static class IntegrationManifestValidator
{
    public const int MaxIdLength = 64;
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 280;
    public const int MaxCapabilities = 32;
    public const int MaxEndpoints = 32;
    public const int MaxEvents = 32;

    private static readonly Regex IdPattern = new("^[a-z0-9]+([.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex CapabilityPattern = new("^[a-z]+\\.[a-z]+$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE"
    };

    private static readonly HashSet<IntegrationTransportKind> UnsupportedNow =
    [
        IntegrationTransportKind.WebSocket,
        IntegrationTransportKind.Grpc,
        IntegrationTransportKind.Mcp
    ];

    public static bool TryValidate(IntegrationManifest? manifest, out string error)
    {
        error = string.Empty;
        if (manifest is null)
        {
            error = "Manifest is missing.";
            return false;
        }

        if (manifest.SchemaVersionValue < 1)
        {
            manifest.SchemaVersionValue = IntegrationManifest.SchemaVersion;
        }

        if (manifest.SchemaVersionValue > IntegrationManifest.SchemaVersion)
        {
            error = "Unsupported manifest schema.";
            return false;
        }

        if (!IsValidId(manifest.Id))
        {
            error = "Manifest id is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Trim().Length > MaxNameLength)
        {
            error = "Manifest name is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            error = "Manifest version is required.";
            return false;
        }

        if (manifest.Description is { Length: > MaxDescriptionLength })
        {
            error = "Manifest description is too long.";
            return false;
        }

        if (UnsupportedNow.Contains(manifest.Transport))
        {
            error = "This transport is an extension point and is not executable yet.";
            return false;
        }

        if (!Enum.IsDefined(manifest.Transport))
        {
            error = "Unsupported transport.";
            return false;
        }

        if (!Enum.IsDefined(manifest.Authentication.Kind))
        {
            error = "Unsupported authentication.";
            return false;
        }

        if (manifest.AllowPrivateNetwork)
        {
            error = "Unconditional private-network access is not allowed.";
            return false;
        }

        if (manifest.Capabilities.Count == 0)
        {
            error = "At least one capability is required.";
            return false;
        }

        if (manifest.Capabilities.Count > MaxCapabilities)
        {
            error = "Too many capabilities.";
            return false;
        }

        if (manifest.Endpoints.Count > MaxEndpoints || manifest.Events.Count > MaxEvents)
        {
            error = "Too many endpoints or events.";
            return false;
        }

        var capabilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in manifest.Capabilities)
        {
            if (!IsValidCapabilityId(capability.Id) || !capabilityIds.Add(capability.Id))
            {
                error = "Invalid or duplicate capability.";
                return false;
            }

            if (!Enum.IsDefined(capability.Risk))
            {
                error = "Invalid capability risk.";
                return false;
            }

            if (LooksForbidden(capability.Id))
            {
                error = "Capability is forbidden.";
                return false;
            }
        }

        var endpointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in manifest.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Id) || !endpointIds.Add(endpoint.Id))
            {
                error = "Invalid or duplicate endpoint id.";
                return false;
            }

            if (!AllowedMethods.Contains(endpoint.Method))
            {
                error = "Endpoint method is not allowed.";
                return false;
            }

            if (!IsSafePath(endpoint.Path))
            {
                error = "Endpoint path is invalid.";
                return false;
            }

            endpoint.TimeoutMs = Math.Clamp(endpoint.TimeoutMs <= 0 ? 5_000 : endpoint.TimeoutMs, 250, 15_000);
            endpoint.MaxResponseBytes = Math.Clamp(
                endpoint.MaxResponseBytes <= 0 ? 65_536 : endpoint.MaxResponseBytes,
                256,
                262_144);
        }

        foreach (var capability in manifest.Capabilities)
        {
            if (!string.IsNullOrWhiteSpace(capability.EndpointId)
                && !endpointIds.Contains(capability.EndpointId))
            {
                error = "Capability references an unknown endpoint.";
                return false;
            }
        }

        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declared in manifest.Events)
        {
            if (!IsValidEventType(declared.Type) || !eventTypes.Add(declared.Type))
            {
                error = "Invalid or duplicate event type.";
                return false;
            }
        }

        if (NeedsHttp(manifest.Transport))
        {
            if (!TryValidateBaseUrl(manifest, out error))
            {
                return false;
            }
        }
        else if (manifest.Transport == IntegrationTransportKind.DeepLink)
        {
            if (manifest.DeepLink is null || !DeepLinkPolicy.IsValidDeclaration(manifest.DeepLink))
            {
                error = "Deep link declaration is invalid.";
                return false;
            }
        }

        if (manifest.Authentication.Kind == IntegrationAuthKind.ApiKey
            && string.IsNullOrWhiteSpace(manifest.Authentication.HeaderName))
        {
            manifest.Authentication.HeaderName = "Authorization";
        }

        manifest.Id = manifest.Id.Trim().ToLowerInvariant();
        manifest.Name = manifest.Name.Trim();
        manifest.Version = manifest.Version.Trim();
        manifest.Description = (manifest.Description ?? string.Empty).Trim();
        return true;
    }

    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && id.Trim().Length <= MaxIdLength
        && IdPattern.IsMatch(id.Trim());

    public static bool IsValidCapabilityId(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && id.Length <= 40
        && CapabilityPattern.IsMatch(id);

    public static bool IsValidEventType(string? type) =>
        !string.IsNullOrWhiteSpace(type)
        && type.Length <= 40
        && CapabilityPattern.IsMatch(type);

    public static bool LooksForbidden(string capabilityId)
    {
        var id = capabilityId.Trim().ToLowerInvariant();
        return id is "shell.run" or "process.start" or "os.delete" or "file.delete"
            || id.StartsWith("shell.", StringComparison.Ordinal)
            || id.StartsWith("process.", StringComparison.Ordinal)
            || id.Contains("arbitrary", StringComparison.Ordinal);
    }

    public static bool IsSafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
        {
            return false;
        }

        if (path.Contains("..", StringComparison.Ordinal)
            || path.Contains("//", StringComparison.Ordinal)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains('?', StringComparison.Ordinal)
            || path.Contains('#', StringComparison.Ordinal)
            || path.Length > 128)
        {
            return false;
        }

        return path.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '/' or '-' or '_' or '{' or '}' or '.');
    }

    private static bool NeedsHttp(IntegrationTransportKind transport) =>
        transport is IntegrationTransportKind.Http
            or IntegrationTransportKind.Https
            or IntegrationTransportKind.Local
            or IntegrationTransportKind.Webhook;

    private static bool TryValidateBaseUrl(IntegrationManifest manifest, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(manifest.BaseUrl)
            && manifest.Transport != IntegrationTransportKind.Webhook
            && manifest.Endpoints.Count > 0)
        {
            error = "Base URL is required for HTTP transports.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.BaseUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(manifest.BaseUrl, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Base URL is invalid.";
            return false;
        }

        var loopback = HttpEndpointPolicy.IsLoopbackHost(uri.Host);
        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!loopback)
            {
                error = "HTTP is only allowed for explicit loopback hosts.";
                return false;
            }

            if (!manifest.AllowLoopback && manifest.Transport != IntegrationTransportKind.Local)
            {
                error = "Loopback HTTP requires allowLoopback or local transport.";
                return false;
            }
        }
        else if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only http (loopback) or https is allowed.";
            return false;
        }

        if (manifest.Transport == IntegrationTransportKind.Https
            && uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !loopback)
        {
            error = "HTTPS transport cannot use a cleartext host.";
            return false;
        }

        if (manifest.Transport == IntegrationTransportKind.Local && !loopback)
        {
            error = "Local transport requires a loopback host.";
            return false;
        }

        if (!manifest.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            manifest.AllowedHosts.Add(uri.Host);
        }

        if (!manifest.AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            manifest.AllowedSchemes.Add(uri.Scheme);
        }

        return true;
    }
}

public static class IntegrationManifestParser
{
    public static bool TryParse(string? json, out IntegrationManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Manifest JSON is empty.";
            return false;
        }

        try
        {
            var options = CreateOptions();
            var parsed = JsonSerializer.Deserialize<ManifestDto>(json, options);
            if (parsed is null)
            {
                error = "Manifest JSON is empty.";
                return false;
            }

            manifest = parsed.ToManifest();
            if (!IntegrationManifestValidator.TryValidate(manifest, out error))
            {
                manifest = null;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Manifest JSON is invalid.";
            return false;
        }
    }

    public static string Serialize(IntegrationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(ManifestDto.From(manifest), CreateOptions());
    }

    internal static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class ManifestDto
    {
        public int SchemaVersion { get; set; } = IntegrationManifest.SchemaVersion;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public string Description { get; set; } = string.Empty;
        public string Transport { get; set; } = "https";
        public string? BaseUrl { get; set; }
        public List<CapabilityDto> Capabilities { get; set; } = [];
        public List<EventDto> Events { get; set; } = [];
        public AuthDto Authentication { get; set; } = new();
        public List<EndpointDto> Endpoints { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
        public List<string> AllowedHosts { get; set; } = [];
        public List<string> AllowedSchemes { get; set; } = [];
        public DeepLinkDto? DeepLink { get; set; }
        public string? LinkedAppId { get; set; }
        public bool AllowLoopback { get; set; }
        public bool AllowPrivateNetwork { get; set; }

        public IntegrationManifest ToManifest()
        {
            var transport = Enum.TryParse<IntegrationTransportKind>(Transport, ignoreCase: true, out var parsed)
                ? parsed
                : (IntegrationTransportKind)(-1);
            return new IntegrationManifest
            {
                SchemaVersionValue = SchemaVersion,
                Id = Id,
                Name = Name,
                Version = Version,
                Description = Description,
                Transport = transport,
                BaseUrl = BaseUrl,
                Capabilities = Capabilities.Select(item => item.ToModel()).ToList(),
                Events = Events.Select(item => item.ToModel()).ToList(),
                Authentication = Authentication.ToModel(),
                Endpoints = Endpoints.Select(item => item.ToModel()).ToList(),
                RequestedPermissions = ParsePermissions(Permissions),
                AllowedHosts = AllowedHosts,
                AllowedSchemes = AllowedSchemes,
                DeepLink = DeepLink?.ToModel(),
                LinkedAppId = LinkedAppId,
                AllowLoopback = AllowLoopback,
                AllowPrivateNetwork = AllowPrivateNetwork
            };
        }

        public static ManifestDto From(IntegrationManifest manifest) => new()
        {
            SchemaVersion = manifest.SchemaVersionValue,
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            Description = manifest.Description,
            Transport = manifest.Transport.ToString(),
            BaseUrl = manifest.BaseUrl,
            Capabilities = manifest.Capabilities.Select(CapabilityDto.From).ToList(),
            Events = manifest.Events.Select(EventDto.From).ToList(),
            Authentication = AuthDto.From(manifest.Authentication),
            Endpoints = manifest.Endpoints.Select(EndpointDto.From).ToList(),
            Permissions = FormatPermissions(manifest.RequestedPermissions),
            AllowedHosts = manifest.AllowedHosts,
            AllowedSchemes = manifest.AllowedSchemes,
            DeepLink = manifest.DeepLink is null ? null : DeepLinkDto.From(manifest.DeepLink),
            LinkedAppId = manifest.LinkedAppId,
            AllowLoopback = manifest.AllowLoopback,
            AllowPrivateNetwork = false
        };
    }

    private sealed class CapabilityDto
    {
        public string Id { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Risk { get; set; } = "read";
        public string? EndpointId { get; set; }
        public string? EventType { get; set; }

        public IntegrationCapabilityDeclaration ToModel() => new()
        {
            Id = Id,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            Risk = Enum.TryParse<CapabilityRiskKind>(Risk, ignoreCase: true, out var risk)
                ? risk
                : (CapabilityRiskKind)(-1),
            EndpointId = EndpointId,
            EventType = EventType
        };

        public static CapabilityDto From(IntegrationCapabilityDeclaration model) => new()
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            Risk = model.Risk.ToString(),
            EndpointId = model.EndpointId,
            EventType = model.EventType
        };
    }

    private sealed class EventDto
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public IntegrationEventDeclaration ToModel() => new() { Type = Type, Description = Description };

        public static EventDto From(IntegrationEventDeclaration model) => new()
        {
            Type = model.Type,
            Description = model.Description
        };
    }

    private sealed class AuthDto
    {
        public string Kind { get; set; } = "none";
        public string? HeaderName { get; set; }
        public string? TokenUrl { get; set; }
        public string? AuthorizationUrl { get; set; }
        public List<string> Scopes { get; set; } = [];

        public IntegrationAuthenticationDeclaration ToModel() => new()
        {
            Kind = Enum.TryParse<IntegrationAuthKind>(Kind, ignoreCase: true, out var kind)
                ? kind
                : (IntegrationAuthKind)(-1),
            HeaderName = HeaderName,
            TokenUrl = TokenUrl,
            AuthorizationUrl = AuthorizationUrl,
            Scopes = Scopes
        };

        public static AuthDto From(IntegrationAuthenticationDeclaration model) => new()
        {
            Kind = model.Kind.ToString(),
            HeaderName = model.HeaderName,
            TokenUrl = model.TokenUrl,
            AuthorizationUrl = model.AuthorizationUrl,
            Scopes = model.Scopes
        };
    }

    private sealed class EndpointDto
    {
        public string Id { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public int TimeoutMs { get; set; } = 5_000;
        public int MaxResponseBytes { get; set; } = 65_536;
        public JsonElement? RequestSchema { get; set; }
        public JsonElement? ResponseSchema { get; set; }

        public IntegrationEndpointDeclaration ToModel() => new()
        {
            Id = Id,
            Method = Method,
            Path = Path,
            TimeoutMs = TimeoutMs,
            MaxResponseBytes = MaxResponseBytes,
            RequestSchema = RequestSchema,
            ResponseSchema = ResponseSchema
        };

        public static EndpointDto From(IntegrationEndpointDeclaration model) => new()
        {
            Id = model.Id,
            Method = model.Method,
            Path = model.Path,
            TimeoutMs = model.TimeoutMs,
            MaxResponseBytes = model.MaxResponseBytes,
            RequestSchema = model.RequestSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } req
                ? req
                : null,
            ResponseSchema = model.ResponseSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } res
                ? res
                : null
        };
    }

    private sealed class DeepLinkDto
    {
        public string Scheme { get; set; } = string.Empty;
        public string? Host { get; set; }
        public string? PathPrefix { get; set; }

        public IntegrationDeepLinkDeclaration ToModel() => new()
        {
            Scheme = Scheme,
            Host = Host,
            PathPrefix = PathPrefix
        };

        public static DeepLinkDto From(IntegrationDeepLinkDeclaration model) => new()
        {
            Scheme = model.Scheme,
            Host = model.Host,
            PathPrefix = model.PathPrefix
        };
    }

    internal static IntegrationPermissionKind ParsePermissions(IEnumerable<string>? names)
    {
        var flags = IntegrationPermissionKind.None;
        foreach (var name in names ?? [])
        {
            if (Enum.TryParse<IntegrationPermissionKind>(name.Replace(" ", string.Empty), ignoreCase: true, out var flag)
                && flag != IntegrationPermissionKind.None)
            {
                flags |= flag;
            }
        }

        return flags == IntegrationPermissionKind.None ? IntegrationPermissionKind.Read : flags;
    }

    internal static List<string> FormatPermissions(IntegrationPermissionKind flags)
    {
        var names = new List<string>();
        foreach (IntegrationPermissionKind flag in Enum.GetValues<IntegrationPermissionKind>())
        {
            if (flag != IntegrationPermissionKind.None && flags.HasFlag(flag))
            {
                names.Add(flag.ToString());
            }
        }

        return names;
    }
}
