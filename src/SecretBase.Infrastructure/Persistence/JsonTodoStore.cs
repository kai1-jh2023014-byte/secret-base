using System.Text.Json;
using SecretBase.Core.Todo;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonTodoStore : ITodoStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonTodoStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "todos.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public TodoList LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = new TodoList();
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<TodoList>(json, _options) ?? new TodoList();
                list.Items ??= [];
                list.Schema = TodoList.SchemaVersion;
                return list;
            }
            catch (JsonException)
            {
                return new TodoList();
            }
            catch (IOException)
            {
                return new TodoList();
            }
        }
    }

    public void Save(TodoList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        lock (_gate)
        {
            SaveUnlocked(list);
        }
    }

    private void SaveUnlocked(TodoList list)
    {
        list.Items ??= [];
        list.Schema = TodoList.SchemaVersion;
        var json = JsonSerializer.Serialize(list, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
