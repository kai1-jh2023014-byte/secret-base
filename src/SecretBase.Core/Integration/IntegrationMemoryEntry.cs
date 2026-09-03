namespace SecretBase.Core.Integration;

/// <summary>Non-secret memory of a connected integration. Tokens stay in ISecureSecretStore.</summary>
public sealed class IntegrationMemoryEntry
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Connected { get; set; }

    /// <summary>True when the user can finish the flow inside Secret Base UI (not the browser as primary).</summary>
    public bool InAppExperience { get; set; }

    public DateTimeOffset? LastConnectedAt { get; set; }

    public DateTimeOffset? LastOpenedAt { get; set; }

    public string ToContextLabel()
    {
        var name = string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
        var status = Connected
            ? (InAppExperience ? "connected, in-app" : "connected")
            : LastOpenedAt is not null
                ? (InAppExperience ? "opened, in-app" : "opened, web widget")
                : "not connected";
        return $"{Id}: {name} ({status})";
    }
}
