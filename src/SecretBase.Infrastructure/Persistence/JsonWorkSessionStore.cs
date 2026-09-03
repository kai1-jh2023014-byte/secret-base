using System.Text.Json;
using SecretBase.Core.Session;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonWorkSessionStore : IWorkSessionStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly WorkSessionStore _inner;
    private readonly object _gate = new();

    public JsonWorkSessionStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "sessions.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var loaded = LoadUnlocked();
        _inner = new WorkSessionStore(loaded.Sessions, loaded.CurrentId);
    }

    public WorkSession? Current => _inner.Current;

    public WorkSession StartOrContinue(
        DateTimeOffset now,
        string? projectId,
        string? projectName,
        string? workspaceTitle)
    {
        lock (_gate)
        {
            var session = _inner.StartOrContinue(now, projectId, projectName, workspaceTitle);
            PersistUnlocked();
            return session;
        }
    }

    public void Touch(string? activity, string? resource, string? unfinishedTask, bool focusStarted)
    {
        lock (_gate)
        {
            _inner.Touch(activity, resource, unfinishedTask, focusStarted);
            PersistUnlocked();
        }
    }

    public WorkSession? End(DateTimeOffset now)
    {
        lock (_gate)
        {
            var ended = _inner.End(now);
            PersistUnlocked();
            return ended;
        }
    }

    public IReadOnlyList<WorkSession> Recent(int take = 12) => _inner.Recent(take);

    public WorkSessionDocument Snapshot() => _inner.Snapshot();

    private WorkSessionDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new WorkSessionDocument();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<WorkSessionDocument>(json, _options) ?? new WorkSessionDocument();
            document.Sessions ??= [];
            document.Schema = WorkSessionDocument.SchemaVersion;
            return document;
        }
        catch (JsonException)
        {
            return new WorkSessionDocument();
        }
        catch (IOException)
        {
            return new WorkSessionDocument();
        }
    }

    private void PersistUnlocked()
    {
        var document = _inner.Snapshot();
        document.Schema = WorkSessionDocument.SchemaVersion;
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
