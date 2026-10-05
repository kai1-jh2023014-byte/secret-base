using SecretBase.Core.Assistant;
using SecretBase.Core.Base;
using SecretBase.Core.Focus;
using SecretBase.Core.Todo;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Pomodoro;

namespace SecretBase.Core.Tests;

public class PomodoroFocusSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_UsesTwentyFiveMinuteFocus()
    {
        var store = new FocusSessionStore();
        var started = store.Start(T0);

        Assert.False(started.AlreadyRunning);
        Assert.True(started.Session.IsRunning);
        Assert.Equal(FocusPhase.Focus, started.Session.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), started.Session.Remaining(T0));
        Assert.Equal("Pomodoro", started.Session.Label);
    }

    [Fact]
    public void ConfigureDurations_CustomFocusAndBreak_UsedOnStartAndAdvance()
    {
        var store = new FocusSessionStore();
        store.ConfigureDurations(
            focus: TimeSpan.FromMinutes(40),
            shortBreak: TimeSpan.FromMinutes(8),
            longBreak: TimeSpan.FromMinutes(20));

        var started = store.Start(T0, duration: TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(1), started.Session.Remaining(T0));
        Assert.Equal(TimeSpan.FromMinutes(8), started.Session.ShortBreakDuration);

        var afterFocus = store.AdvanceIfComplete(T0.AddMinutes(1));
        Assert.Equal(FocusPhase.ShortBreak, afterFocus.Phase);
        Assert.Equal(TimeSpan.FromMinutes(8), afterFocus.Remaining(T0.AddMinutes(1)));
    }

    [Fact]
    public void FocusCompletionChime_WritesValidWav()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-chime-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = FocusCompletionChime.EnsureWavFile(dir);
            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 44);
            Assert.Equal((byte)'R', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void PomodoroConfiguration_RoundTripsCustomDurations()
    {
        var config = PomodoroWidgetConfiguration.CreateDefault();
        config.FocusMinutes = 45;
        config.ShortBreakMinutes = 10;
        config.SoundOnComplete = false;
        var restored = PomodoroWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.Equal(45, restored.FocusMinutes);
        Assert.Equal(10, restored.ShortBreakMinutes);
        Assert.False(restored.SoundOnComplete);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_DoesNotStack()
    {
        var store = new FocusSessionStore();
        store.Start(T0);
        var again = store.Start(T0.AddMinutes(1), TimeSpan.FromMinutes(40), "Other");

        Assert.True(again.AlreadyRunning);
        Assert.Equal("Pomodoro", again.Session.Label);
        Assert.Equal(TimeSpan.FromMinutes(24), again.Session.Remaining(T0.AddMinutes(1)));
    }

    [Fact]
    public void PauseAndResume_PreservesRemaining()
    {
        var store = new FocusSessionStore();
        store.Start(T0);
        store.Pause(T0.AddMinutes(5));
        Assert.True(store.Current.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(20), store.Current.Remaining(T0.AddMinutes(5)));

        var resumedAt = T0.AddMinutes(8);
        store.Resume(resumedAt);
        Assert.False(store.Current.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(20), store.Current.Remaining(resumedAt));
        Assert.Equal(TimeSpan.FromMinutes(19), store.Current.Remaining(resumedAt.AddMinutes(1)));
    }

    [Fact]
    public void Start_WhenPaused_ResumesInsteadOfRestarting()
    {
        var store = new FocusSessionStore();
        store.Start(T0);
        store.Pause(T0.AddMinutes(10));
        var result = store.Start(T0.AddMinutes(12));

        Assert.True(result.Resumed);
        Assert.False(result.AlreadyRunning);
        Assert.False(result.Session.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Session.Remaining(T0.AddMinutes(12)));
    }

    [Fact]
    public void AdvanceIfComplete_GoesToShortBreak_ThenNextFocus()
    {
        var store = new FocusSessionStore();
        store.Start(T0, TimeSpan.FromMinutes(1));

        var afterFocus = store.AdvanceIfComplete(T0.AddMinutes(1));
        Assert.Equal(FocusPhase.ShortBreak, afterFocus.Phase);
        Assert.Equal(1, afterFocus.CompletedFocusRounds);
        Assert.Equal(TimeSpan.FromMinutes(5), afterFocus.Remaining(T0.AddMinutes(1)));

        var afterBreak = store.AdvanceIfComplete(T0.AddMinutes(1).AddMinutes(5));
        Assert.Equal(FocusPhase.Focus, afterBreak.Phase);
        Assert.Equal(2, afterBreak.Round);
    }

    [Fact]
    public void AdvanceIfComplete_LongBreakAfterFourFocusRounds()
    {
        var store = new FocusSessionStore();
        store.Start(T0, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 3; i++)
        {
            store.AdvanceIfComplete(T0.AddMinutes(i * 10 + 1));
            store.AdvanceIfComplete(T0.AddMinutes(i * 10 + 6));
        }

        var fourthFocusDone = store.AdvanceIfComplete(T0.AddMinutes(31));
        Assert.Equal(FocusPhase.LongBreak, fourthFocusDone.Phase);
        Assert.Equal(4, fourthFocusDone.CompletedFocusRounds);
        Assert.Equal(TimeSpan.FromMinutes(15), fourthFocusDone.Remaining(T0.AddMinutes(31)));
    }

    [Fact]
    public void StatusLine_ShowsPhaseAndPause()
    {
        var store = new FocusSessionStore();
        store.Start(T0);
        Assert.Contains("Focus", store.Current.StatusLine(T0), StringComparison.Ordinal);
        store.Pause(T0.AddSeconds(30));
        Assert.Contains("paused", store.Current.StatusLine(T0.AddSeconds(30)), StringComparison.Ordinal);
    }
}

public class PomodoroAssistantToolTests
{
    [Fact]
    public async Task FocusStart_OpensPomodoroWidget_AndStarts()
    {
        var focus = new FocusSessionStore();
        var baseServices = new BaseExperienceServices(
            new MemoryTodoStore(),
            focus,
            () => [],
            () => [],
            () => [],
            () => new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: baseServices);

        var result = await executor.ExecuteAsync(AssistantToolNames.FocusStart, """{"minutes":25}""");

        Assert.True(result.Succeeded);
        Assert.Equal(WidgetTypes.Pomodoro, result.EnsureWidgetType);
        Assert.True(focus.Current.IsRunning);
        Assert.Contains("Pomodoro", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FocusStart_WhenAlreadyRunning_ShowsHonestState()
    {
        var now = new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);
        var focus = new FocusSessionStore();
        focus.Start(now);
        var baseServices = new BaseExperienceServices(
            new MemoryTodoStore(),
            focus,
            () => [],
            () => [],
            () => [],
            () => now.AddMinutes(3));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: baseServices);

        var result = await executor.ExecuteAsync(AssistantToolNames.FocusStart, """{"minutes":40}""");

        Assert.True(result.Succeeded);
        Assert.Equal(WidgetTypes.Pomodoro, result.EnsureWidgetType);
        Assert.Contains("already running", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMinutes(25), focus.Current.FocusDuration);
    }

    [Fact]
    public void Planner_MapsPomodoroNowPhrases()
    {
        foreach (var phrase in new[]
                 {
                     "ポモドーロしたい",
                     "今ポモドーロやって",
                     "Start a pomodoro now",
                     "集中タイマー開始"
                 })
        {
            var plan = AssistantPlanner.TryBuildFromIntent(
                AssistantIntentKind.ActionRequest,
                phrase,
                snapshot: null,
                maxSteps: 3);
            Assert.NotNull(plan);
            Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.FocusStart);
        }
    }

    [Fact]
    public void Catalog_IncludesPomodoro()
    {
        Assert.Contains(WidgetCatalog.Entries, e => e.WidgetType == WidgetTypes.Pomodoro);
        var widget = DefaultWidgetFactory.CreatePomodoro();
        Assert.Equal(WidgetTypes.Pomodoro, widget.Type);
    }
}
