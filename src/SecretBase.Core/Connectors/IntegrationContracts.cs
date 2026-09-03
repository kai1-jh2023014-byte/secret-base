namespace SecretBase.Core.Connectors;

/// <summary>Declared capability with risk and safety lane. Not a free-form URL.</summary>
public interface IIntegrationCapability
{
    string Id { get; }

    string DisplayName { get; }

    CapabilityRiskKind Risk { get; }

    IntegrationActionLane Lane { get; }

    string? EndpointId { get; }
}

/// <summary>One registered integration. Execution still goes through the host Safety pipeline.</summary>
public interface IIntegrationConnector
{
    string IntegrationId { get; }

    string DisplayName { get; }

    IntegrationTransportKind Transport { get; }

    IntegrationHealthStatus Health { get; }

    IReadOnlyList<IIntegrationCapability> Capabilities { get; }
}

public interface IIntegrationAuthenticator
{
    bool TryApply(
        IntegrationRegistration registration,
        IDictionary<string, string> headers,
        out string? errorCategory);
}

public interface IIntegrationEventSource
{
    bool TryIngest(
        WebhookEnvelope envelope,
        DateTimeOffset now,
        out ExternalIntegrationEvent? normalized,
        out string error);
}

public interface IIntegrationActionExecutor
{
    Task<ConnectorOutcome> InvokeAsync(
        ConnectorInvocation invocation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IIntegrationHealthMonitor
{
    IntegrationHealthStatus Get(string integrationId);

    IReadOnlyList<IntegrationHealthRecord> Snapshot();
}

public sealed record IntegrationHealthRecord(
    string IntegrationId,
    string Name,
    IntegrationHealthStatus Status,
    string? Detail,
    DateTimeOffset? LastUsedAt);

public sealed class IntegrationCapabilityModel : IIntegrationCapability
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public CapabilityRiskKind Risk { get; init; }

    public IntegrationActionLane Lane { get; init; }

    public string? EndpointId { get; init; }

    public static IntegrationCapabilityModel From(
        IntegrationCapabilityDeclaration declaration,
        IntegrationEndpointDeclaration? endpoint) =>
        new()
        {
            Id = declaration.Id,
            DisplayName = string.IsNullOrWhiteSpace(declaration.DisplayName) ? declaration.Id : declaration.DisplayName,
            Risk = declaration.Risk,
            Lane = IntegrationSafety.Classify(declaration, endpoint),
            EndpointId = declaration.EndpointId
        };
}

public sealed class RegisteredConnector : IIntegrationConnector
{
    public RegisteredConnector(IntegrationRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        Registration = registration;
        Capabilities = registration.Manifest.Capabilities
            .Select(capability =>
            {
                var endpoint = registration.Manifest.Endpoints.FirstOrDefault(item =>
                    item.Id.Equals(capability.EndpointId, StringComparison.OrdinalIgnoreCase));
                return (IIntegrationCapability)IntegrationCapabilityModel.From(capability, endpoint);
            })
            .ToList();
    }

    public IntegrationRegistration Registration { get; }

    public string IntegrationId => Registration.Id;

    public string DisplayName => Registration.DisplayName;

    public IntegrationTransportKind Transport => Registration.Manifest.Transport;

    public IntegrationHealthStatus Health => Registration.Enabled ? Registration.Health : IntegrationHealthStatus.Disabled;

    public IReadOnlyList<IIntegrationCapability> Capabilities { get; }
}

public sealed class IntegrationAuthenticator : IIntegrationAuthenticator
{
    private readonly IIntegrationSecretResolver _secrets;

    public IntegrationAuthenticator(IIntegrationSecretResolver? secrets = null)
    {
        _secrets = secrets ?? new NullIntegrationSecretResolver();
    }

    public bool TryApply(
        IntegrationRegistration registration,
        IDictionary<string, string> headers,
        out string? errorCategory)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(headers);
        errorCategory = null;
        var kind = registration.Manifest.Authentication.Kind;
        if (kind == IntegrationAuthKind.None)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(registration.CredentialReference)
            || !_secrets.TryResolveHeader(
                registration.CredentialReference,
                registration.Manifest.Authentication.HeaderName,
                out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue))
        {
            errorCategory = "auth";
            return false;
        }

        headers[registration.Manifest.Authentication.HeaderName ?? "Authorization"] = headerValue;
        return true;
    }
}

/// <summary>Explicit registration and user-selected JSON only. No LAN or internet discovery.</summary>
public static class IntegrationDiscovery
{
    public static bool TryRegisterJson(
        IIntegrationRegistry registry,
        string json,
        bool approved,
        out string error) =>
        TryRegisterJson(registry, json, approved, IntegrationDiscoveryKind.UserSelectedManifest, out error);

    public static bool TryRegisterJson(
        IIntegrationRegistry registry,
        string json,
        bool approved,
        IntegrationDiscoveryKind discovery,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!IntegrationManifestParser.TryParse(json, out var manifest, out error) || manifest is null)
        {
            return false;
        }

        return registry.TryRegister(manifest, approved, discovery, out error);
    }
}
