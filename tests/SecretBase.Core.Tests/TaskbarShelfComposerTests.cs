using SecretBase.Core.Calendar;
using SecretBase.Core.Desktop;
using SecretBase.Core.Focus;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Tests;

public class TaskbarShelfComposerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 14, 5, 0, TimeSpan.Zero);

    [Fact]
    public void Compose_ShowsClockDateAndIdleFocus()
    {
        var snap = TaskbarShelfComposer.Compose(Now, focus: null, openTodos: [], events: []);

        Assert.Equal("14:05", snap.Clock);
        Assert.Contains("3", snap.Date);
        Assert.Equal("Focus idle", snap.Focus);
        Assert.Equal("Nothing queued", snap.Next);
    }

    [Fact]
    public void Compose_PrefersImminentEventOverTodo()
    {
        var events = new[]
        {
            new CalendarEvent
            {
                Title = "Design review",
                Start = Now.AddMinutes(40)
            }
        };
        var todos = new[] { TodoItem.Create("Write notes") };

        var snap = TaskbarShelfComposer.Compose(Now, focus: null, todos, events);

        Assert.Equal("14:45 Design review", snap.Next);
    }

    [Fact]
    public void Compose_UsesTodoWhenEventIsFar()
    {
        var events = new[]
        {
            new CalendarEvent
            {
                Title = "Tomorrow",
                Start = Now.AddHours(8)
            }
        };
        var todos = new[] { TodoItem.Create("Ship the shelf") };

        var snap = TaskbarShelfComposer.Compose(Now, focus: null, todos, events);

        Assert.Equal("Ship the shelf", snap.Next);
    }

    [Fact]
    public void Compose_ShowsRunningFocus()
    {
        var focus = new FocusSession
        {
            IsRunning = true,
            Label = "Pomodoro",
            StartedAt = Now.AddMinutes(-5),
            Duration = TimeSpan.FromMinutes(25)
        };

        var snap = TaskbarShelfComposer.Compose(Now, focus, [], []);

        Assert.Contains("Pomodoro", snap.Focus);
        Assert.Contains("20:00", snap.Focus);
    }
}
