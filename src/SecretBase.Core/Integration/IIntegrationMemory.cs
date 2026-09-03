namespace SecretBase.Core.Integration;

/// <summary>
/// Remembers that the user connected Spotify / Google Calendar / Classroom.
/// Never stores tokens or secrets.
/// </summary>
public interface IIntegrationMemory
{
    IReadOnlyList<IntegrationMemoryEntry> List();

    IntegrationMemoryEntry? Find(string id);

    void RememberConnected(string id, string displayName, bool inAppExperience);

    void RememberDisconnected(string id);

    void RememberOpened(string id, string displayName, bool inAppExperience);
}
