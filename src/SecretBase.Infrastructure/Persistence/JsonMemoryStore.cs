using System.Text.Json;
using SecretBase.Core.Memory;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonMemoryStore : IMemoryStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly MemoryStore _inner;
    private readonly object _gate = new();

    public JsonMemoryStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "memory.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _inner = new MemoryStore(LoadUnlocked().Entries);
    }

    public MemoryEntry Remember(MemoryEntry entry)
    {
        lock (_gate)
        {
            var saved = _inner.Remember(entry);
            PersistUnlocked();
            return saved;
        }
    }

    public void Forget(string id)
    {
        lock (_gate)
        {
            _inner.Forget(id);
            PersistUnlocked();
        }
    }

    public IReadOnlyList<MemoryEntry> Recall(
        DateTimeOffset now,
        MemoryScope? scope = null,
        string? projectId = null,
        string? query = null,
        int take = 12) =>
        _inner.Recall(now, scope, projectId, query, take);

    public IReadOnlyList<MemoryEntry> RecallRanked(
        DateTimeOffset now,
        string? query = null,
        string? projectName = null,
        int take = 8) =>
        _inner.RecallRanked(now, query, projectName, take);

    public MemoryEntry Update(string id, string? summary = null, string? detail = null, MemoryImportance? importance = null)
    {
        lock (_gate)
        {
            var updated = _inner.Update(id, summary, detail, importance);
            PersistUnlocked();
            return updated;
        }
    }

    public MemoryEntry Merge(MemoryEntry incoming)
    {
        lock (_gate)
        {
            var saved = _inner.Merge(incoming);
            PersistUnlocked();
            return saved;
        }
    }

    public MemoryDocument Snapshot() => _inner.Snapshot();

    public int Expire(DateTimeOffset now)
    {
        lock (_gate)
        {
            var removed = _inner.Expire(now);
            if (removed > 0)
            {
                PersistUnlocked();
            }

            return removed;
        }
    }

    private MemoryDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new MemoryDocument();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<MemoryDocument>(json, _options) ?? new MemoryDocument();
            document.Entries ??= [];
            document.Schema = MemoryDocument.SchemaVersion;
            return document;
        }
        catch (JsonException)
        {
            return new MemoryDocument();
        }
        catch (IOException)
        {
            return new MemoryDocument();
        }
    }

    private void PersistUnlocked()
    {
        var document = _inner.Snapshot();
        document.Schema = MemoryDocument.SchemaVersion;
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
