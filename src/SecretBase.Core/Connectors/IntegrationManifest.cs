using System.Text.Json;

namespace SecretBase.Core.Connectors;

public sealed class IntegrationManifest
{
    public const int SchemaVersion = 1;

    public int SchemaVersionValue { get; set; } = SchemaVersion;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";

    public string Description { get; set; } = string.Empty;

    public IntegrationTransportKind Transport { get; set; } = IntegrationTransportKind.Https;

    public string? BaseUrl { get; set; }

    public List<IntegrationCapabilityDeclaration> Capabilities { get; set; } = [];

    public List<IntegrationEventDeclaration> Events { get; set; } = [];

    public IntegrationAuthenticationDeclaration Authentication { get; set; } = new();

    public List<IntegrationEndpointDeclaration> Endpoints { get; set; } = [];

    public IntegrationPermissionKind RequestedPermissions { get; set; } =
        IntegrationPermissionKind.Read;

    public List<string> AllowedHosts { get; set; } = [];

    public List<string> AllowedSchemes { get; set; } = [];

    public IntegrationDeepLinkDeclaration? DeepLink { get; set; }

    public string? LinkedAppId { get; set; }

    public bool AllowLoopback { get; set; }

    /// <summary>Always false in v1.1 — private LAN hosts must be named in AllowedHosts, never a wildcard.</summary>
    public bool AllowPrivateNetwork { get; set; }
}

public sealed class IntegrationCapabilityDeclaration
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public CapabilityRiskKind Risk { get; set; } = CapabilityRiskKind.Read;

    public string? EndpointId { get; set; }

    public string? EventType { get; set; }
}

public sealed class IntegrationEventDeclaration
{
    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class IntegrationAuthenticationDeclaration
{
    public IntegrationAuthKind Kind { get; set; } = IntegrationAuthKind.None;

    /// <summary>Header name for API keys (e.g. Authorization). Never the secret value.</summary>
    public string? HeaderName { get; set; }

    public string? TokenUrl { get; set; }

    public string? AuthorizationUrl { get; set; }

    public List<string> Scopes { get; set; } = [];
}

public sealed class IntegrationEndpointDeclaration
{
    public string Id { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Path { get; set; } = "/";

    public int TimeoutMs { get; set; } = 5_000;

    public int MaxResponseBytes { get; set; } = 65_536;

    public JsonElement? RequestSchema { get; set; }

    public JsonElement? ResponseSchema { get; set; }
}

public sealed class IntegrationDeepLinkDeclaration
{
    public string Scheme { get; set; } = string.Empty;

    public string? Host { get; set; }

    public string? PathPrefix { get; set; }
}

public static class IntegrationCapabilityIds
{
    public const string StateRead = "state.read";
    public const string StatsRead = "stats.read";
    public const string ProjectRead = "project.read";
    public const string ProjectWrite = "project.write";
    public const string AppOpen = "app.open";
    public const string FileOpen = "file.open";
    public const string DataCreate = "data.create";
    public const string DataUpdate = "data.update";
    public const string DataDelete = "data.delete";
    public const string EventReceive = "event.receive";
    public const string CalendarList = "calendar.list";
}

public static class IntegrationIds
{
    public const string LocalTest = "demo.local-test";
    public const string UserApp = "demo.user-app";
    public const string Calendar = "secretbase.calendar";
}
