namespace SecretBase.Core.Integration;

/// <summary>In-memory integration memory for tests and hosts without persistence.</summary>
public sealed class MemoryIntegrationMemory : IIntegrationMemory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IntegrationMemoryEntry> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IntegrationMemoryEntry> List()
    {
        lock (_gate)
        {
            return _items.Values
                .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public IntegrationMemoryEntry? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_gate)
        {
            return _items.TryGetValue(id.Trim(), out var entry) ? Clone(entry) : null;
        }
    }

    public void RememberConnected(string id, string displayName, bool inAppExperience) =>
        Upsert(id, displayName, connected: true, inAppExperience, opened: true);

    public void RememberDisconnected(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lock (_gate)
        {
            if (!_items.TryGetValue(id.Trim(), out var entry))
            {
                return;
            }

            entry.Connected = false;
        }
    }

    public void RememberOpened(string id, string displayName, bool inAppExperience) =>
        Upsert(id, displayName, connected: false, inAppExperience, opened: true);

    private void Upsert(string id, string displayName, bool connected, bool inAppExperience, bool opened)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var key = id.Trim();
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var entry))
            {
                entry = new IntegrationMemoryEntry { Id = key };
                _items[key] = entry;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                entry.DisplayName = displayName.Trim();
            }
            else if (string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                entry.DisplayName = key;
            }

            entry.InAppExperience = inAppExperience;
            if (connected)
            {
                entry.Connected = true;
                entry.LastConnectedAt = DateTimeOffset.UtcNow;
            }

            if (opened)
            {
                entry.LastOpenedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private static IntegrationMemoryEntry Clone(IntegrationMemoryEntry source) =>
        new()
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Connected = source.Connected,
            InAppExperience = source.InAppExperience,
            LastConnectedAt = source.LastConnectedAt,
            LastOpenedAt = source.LastOpenedAt
        };
}
