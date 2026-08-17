using SecretBase.Core.Security;

namespace SecretBase.Core.Integration;

/// <summary>Catalog metadata for one allowed operation. Not an executor.</summary>
public sealed class IntegrationCommandDescriptor
{
    public required string Id { get; init; }

    public required string Domain { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public ActionPrivilege Privilege { get; init; } = ActionPrivilege.Observation;

    public TrustBoundary Trust { get; init; } = TrustBoundary.BuiltInWidget;
}
