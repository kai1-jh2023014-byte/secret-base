namespace SecretBase.Core.Connectors;

/// <summary>How Secret Base reaches an integration. Unknown kinds are rejected, not guessed.</summary>
public enum IntegrationTransportKind
{
    Local = 0,
    Http = 1,
    Https = 2,
    Webhook = 3,
    DeepLink = 4,
    /// <summary>Extension point only — not executable in v1.1.</summary>
    WebSocket = 5,
    /// <summary>Extension point only — not executable in v1.1.</summary>
    Grpc = 6,
    /// <summary>Extension point only — MCP servers are never auto-run.</summary>
    Mcp = 7
}

public enum IntegrationAuthKind
{
    None = 0,
    ApiKey = 1,
    OAuth2 = 2,
    OAuth2Pkce = 3
}

/// <summary>Capability risk declared in the manifest. Maps onto action lanes; learning cannot change this.</summary>
public enum CapabilityRiskKind
{
    Read = 0,
    Write = 1,
    Execute = 2,
    Destructive = 3,
    External = 4
}

/// <summary>Safety lane for one connector operation. CONFIRM never becomes SAFE_AUTO via learning.</summary>
public enum IntegrationActionLane
{
    Read = 0,
    SafeAuto = 1,
    Confirm = 2,
    Destructive = 3,
    Forbidden = 4
}

[Flags]
public enum IntegrationPermissionKind
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    ExternalCommunication = 8,
    Destructive = 16,
    BackgroundEvent = 32
}

public enum IntegrationHealthStatus
{
    Connected = 0,
    Disconnected = 1,
    Unavailable = 2,
    AuthenticationRequired = 3,
    PermissionDenied = 4,
    RateLimited = 5,
    Error = 6,
    Disabled = 7
}

public enum IntegrationDiscoveryKind
{
    ExplicitRegistration = 0,
    UserSelectedManifest = 1,
    KnownAppRegistration = 2
}

public enum IntegrationFreshness
{
    Fresh = 0,
    Stale = 1,
    Unavailable = 2
}
