using SecretBase.Core.Ai;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Time;

namespace SecretBase.Core.Tests;

public class LocalScheduleParserTests
{
    [Fact]
    public void ParsesJapaneseTimedDayPlan()
    {
        Assert.True(LocalScheduleParser.TryParse(
            "19時勉強、20時食事と風呂、21時勉強　22時30分ギターをいれてください。",
            out var events));
        Assert.Equal(4, events.Count);
        Assert.Equal("勉強", events[0].Title);
        Assert.Equal(19, events[0].Hour);
        Assert.Equal(60, events[0].DurationMinutes);
        Assert.Equal("食事と風呂", events[1].Title);
        Assert.Equal(20, events[1].Hour);
        Assert.Equal("勉強", events[2].Title);
        Assert.Equal(21, events[2].Hour);
        Assert.Equal("ギター", events[3].Title);
        Assert.Equal(22, events[3].Hour);
        Assert.Equal(30, events[3].Minute);
    }

    [Fact]
    public void ParsesHalfHourShortcut()
    {
        Assert.True(LocalScheduleParser.TryParse("22時半ギターをいれて", out var events));
        Assert.Single(events);
        Assert.Equal(22, events[0].Hour);
        Assert.Equal(30, events[0].Minute);
        Assert.Equal("ギター", events[0].Title);
    }

    [Fact]
    public void LooksLikeScheduleWrite_WithoutYoteiWord()
    {
        Assert.True(LocalScheduleParser.LooksLikeScheduleWrite(
            "19時勉強、20時食事、風呂、21時勉強 22時半ギター　をいれてください。"));
        Assert.False(LocalScheduleParser.LooksLikeScheduleWrite("今日の天気は？"));
        Assert.False(LocalScheduleParser.LooksLikeScheduleWrite("いつもの予定をいれて"));
    }

    [Fact]
    public void Planner_MatchesTimedListWithoutYotei()
    {
        var plan = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "19時勉強、20時食事と風呂、21時勉強　22時30分ギターをいれてください。",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(plan);
        Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.CalendarAddEvent);
    }

    [Fact]
    public async Task Assistant_QueuesLocalConfirmation_WithoutCallingModel()
    {
        var day = new DateOnly(2026, 10, 8);
        var offset = TimeSpan.FromHours(9);
        var local = new SecretBase.Core.Calendar.LocalCalendarProvider();
        var calendar = new SecretBase.Core.Calendar.CalendarCommandService(
            new SecretBase.Core.Calendar.CalendarService([local]),
            new FixedNow(new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), offset)));
        var executor = new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar);
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            executor,
            () => new NeverCallAiProvider(),
            () => new AssistantSettings { MaxSteps = 5 });

        var turn = await assistant.SendAsync(
            "19時勉強、20時食事と風呂、21時勉強　22時30分ギターをいれてください。");
        Assert.True(turn.Succeeded);
        Assert.NotNull(turn.PendingConfirmation);
        Assert.Contains("local calendar", turn.PendingConfirmation!.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ギター", turn.PendingConfirmation.Prompt, StringComparison.Ordinal);

        var confirmed = await assistant.ConfirmPendingAsync();
        Assert.True(confirmed.Succeeded);
        Assert.Equal(4, local.ListAll().Count);
        Assert.Contains(local.ListAll(), e => e.Title == "ギター" && e.Start.Hour == 22 && e.Start.Minute == 30);
    }

    private sealed class FixedNow(DateTimeOffset now) : SecretBase.Core.Time.ITimeProvider
    {
        public DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public DateTimeOffset GetLocalNow() => now;
    }

    private sealed class NeverCallAiProvider : IAiProvider
    {
        public string ProviderId => "never";

        public string DisplayName => "Never";

        public Task<AiProviderResponse> ChatAsync(
            IReadOnlyList<AiMessage> messages,
            IReadOnlyList<AssistantToolDefinition> tools,
            string? model,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Remote model must not be called for local schedule parse.");
    }
}
