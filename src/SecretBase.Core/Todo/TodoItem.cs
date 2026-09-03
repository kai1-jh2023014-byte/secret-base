namespace SecretBase.Core.Todo;

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ProjectId { get; set; }

    public static TodoItem Create(string title, string? projectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title.Trim(),
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim()
        };
    }
}

public sealed class TodoList
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<TodoItem> Items { get; set; } = [];

    public IReadOnlyList<TodoItem> OpenItems =>
        Items.Where(item => !item.IsDone).ToList();
}

public interface ITodoStore
{
    TodoList LoadOrCreate();

    void Save(TodoList list);
}

public sealed class MemoryTodoStore : ITodoStore
{
    private readonly object _gate = new();
    private TodoList _list = new();

    public TodoList LoadOrCreate()
    {
        lock (_gate)
        {
            _list.Items ??= [];
            return _list;
        }
    }

    public void Save(TodoList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        lock (_gate)
        {
            list.Items ??= [];
            list.Schema = TodoList.SchemaVersion;
            _list = list;
        }
    }
}
