using System.Text.Json;
using SecretBase.Core.Integration;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Integration;

internal sealed class IntegrationMemoryDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<IntegrationMemoryEntry> Integrations { get; set; } = [];
}

/// <summary>Persists connected-integration flags. Tokens stay in ISecureSecretStore.</summary>
public sealed class JsonIntegrationMemoryStore : IIntegrationMemory
{
    public const int CurrentSchemaVersion = 1;

    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonIntegrationMemoryStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "integrations.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public IReadOnlyList<IntegrationMemoryEntry> List()
    {
        lock (_gate)
        {
            return LoadUnlocked().Integrations
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
            var hit = LoadUnlocked().Integrations
                .FirstOrDefault(e => string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            return hit is null ? null : Clone(hit);
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
            var doc = LoadUnlocked();
            var entry = doc.Integrations.FirstOrDefault(e =>
                string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return;
            }

            entry.Connected = false;
            SaveUnlocked(doc);
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
            var doc = LoadUnlocked();
            var entry = doc.Integrations.FirstOrDefault(e =>
                string.Equals(e.Id, key, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                entry = new IntegrationMemoryEntry { Id = key };
                doc.Integrations.Add(entry);
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

            SaveUnlocked(doc);
        }
    }

    private IntegrationMemoryDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new IntegrationMemoryDocument { SchemaVersion = CurrentSchemaVersion };
        }

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<IntegrationMemoryDocument>(json, _options)
                      ?? new IntegrationMemoryDocument();
            doc.Integrations ??= [];
            doc.SchemaVersion = CurrentSchemaVersion;
            return doc;
        }
        catch (JsonException)
        {
            return new IntegrationMemoryDocument { SchemaVersion = CurrentSchemaVersion };
        }
        catch (IOException)
        {
            return new IntegrationMemoryDocument { SchemaVersion = CurrentSchemaVersion };
        }
    }

    private void SaveUnlocked(IntegrationMemoryDocument doc)
    {
        doc.SchemaVersion = CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(doc, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
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
