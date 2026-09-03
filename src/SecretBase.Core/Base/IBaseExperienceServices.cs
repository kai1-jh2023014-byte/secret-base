using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Focus;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Base;

/// <summary>
/// Composition of Base domains for Assistant tools. Not a widget state bag.
/// </summary>
public interface IBaseExperienceServices
{
    TodoList LoadTodos();

    void SaveTodos(TodoList list);

    FocusSessionStore Focus { get; }

    WorkspaceSession? CurrentWorkspace { get; set; }

    IReadOnlyList<CreativeProject> ListProjects();

    IReadOnlyList<CustomApp> ListApps();

    IReadOnlyList<CalendarEvent> ListUpcomingEvents();

    DateTimeOffset Now { get; }
}

public sealed class BaseExperienceServices : IBaseExperienceServices
{
    private readonly ITodoStore _todos;
    private readonly Func<IReadOnlyList<CreativeProject>> _projects;
    private readonly Func<IReadOnlyList<CustomApp>> _apps;
    private readonly Func<IReadOnlyList<CalendarEvent>> _events;
    private readonly Func<DateTimeOffset> _now;

    public BaseExperienceServices(
        ITodoStore todos,
        FocusSessionStore focus,
        Func<IReadOnlyList<CreativeProject>> projects,
        Func<IReadOnlyList<CustomApp>> apps,
        Func<IReadOnlyList<CalendarEvent>> events,
        Func<DateTimeOffset> now)
    {
        _todos = todos ?? throw new ArgumentNullException(nameof(todos));
        Focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _apps = apps ?? throw new ArgumentNullException(nameof(apps));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _now = now ?? throw new ArgumentNullException(nameof(now));
    }

    public FocusSessionStore Focus { get; }

    public WorkspaceSession? CurrentWorkspace { get; set; }

    public TodoList LoadTodos() => _todos.LoadOrCreate();

    public void SaveTodos(TodoList list) => _todos.Save(list);

    public IReadOnlyList<CreativeProject> ListProjects() => _projects();

    public IReadOnlyList<CustomApp> ListApps() => _apps();

    public IReadOnlyList<CalendarEvent> ListUpcomingEvents() => _events();

    public DateTimeOffset Now => _now();
}
