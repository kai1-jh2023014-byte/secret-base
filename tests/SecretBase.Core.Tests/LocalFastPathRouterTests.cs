using SecretBase.Core.Ai;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Jev;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Ai;

namespace SecretBase.Core.Tests;

public class LocalFastPathRouterTests
{
    [Fact]
    public void TodayAgenda_IsLocalRun()
    {
        var match = LocalFastPathRouter.TryMatch("今日の予定を見せて", AssistantIntentKind.Question);
        Assert.Equal(LocalFastPathKind.RunTools, match.Kind);
        Assert.Equal(AssistantToolNames.CalendarGetToday, match.Steps[0].ToolName);
        Assert.False(LocalFastPathRouter.NeedsJev(match, AssistantIntentKind.Question, "今日の予定を見せて"));
        Assert.False(LocalFastPathRouter.NeedsConversationModel(match));
    }

    [Fact]
    public void ProjectSearch_WithUniqueHit_GetsProject()
    {
        var snapshot = new AssistantContextSnapshot
        {
            Projects =
            [
                new AssistantProjectSummary { Id = "p1", Name = "Secret Base" },
                new AssistantProjectSummary { Id = "p2", Name = "Other" }
            ]
        };
        var match = LocalFastPathRouter.TryMatch(
            "Secret Baseプロジェクトを検索して",
            AssistantIntentKind.ActionRequest,
            snapshot);
        Assert.Equal(LocalFastPathKind.RunTools, match.Kind);
        Assert.Equal(AssistantToolNames.CreativeGetProject, match.Steps[0].ToolName);
        Assert.Contains("p1", match.Steps[0].ArgumentsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoAdd_WithoutTitle_Clarifies()
    {
        var match = LocalFastPathRouter.TryMatch("Todoを追加して", AssistantIntentKind.ActionRequest);
        Assert.Equal(LocalFastPathKind.Clarify, match.Kind);
        Assert.False(string.IsNullOrWhiteSpace(match.ClarifyQuestion));
    }

    [Fact]
    public void SituationalEnvironment_NeedsJudgment()
    {
        var match = LocalFastPathRouter.TryMatch("いつもの勉強環境にして", AssistantIntentKind.ActionRequest);
        Assert.Equal(LocalFastPathKind.NeedsJudgment, match.Kind);
        Assert.True(LocalFastPathRouter.NeedsJev(match, AssistantIntentKind.ActionRequest, "いつもの勉強環境にして"));
    }

    [Fact]
    public void MusicPause_IsLocalRun()
    {
        var match = LocalFastPathRouter.TryMatch("音楽を止めて", AssistantIntentKind.ActionRequest);
        Assert.Equal(LocalFastPathKind.RunTools, match.Kind);
        Assert.Equal(AssistantToolNames.MusicPause, match.Steps[0].ToolName);
    }

    [Fact]
    public void ComplexQuestion_NeedsConversationModel_NotJev()
    {
        var match = LocalFastPathRouter.TryMatch("Pokemonについて教えて", AssistantIntentKind.Question);
        Assert.Equal(LocalFastPathKind.None, match.Kind);
        Assert.False(LocalFastPathRouter.NeedsJev(match, AssistantIntentKind.Question, "Pokemonについて教えて"));
        Assert.True(LocalFastPathRouter.NeedsConversationModel(match));
    }
}

public class LocalScheduleDayOffsetTests
{
    [Fact]
    public void ParsesTomorrowTimedEvent()
    {
        Assert.True(LocalScheduleParser.TryParse(
            "明日19時から英語の勉強を追加して",
            out var events));
        Assert.Single(events);
        Assert.Equal(1, events[0].DayOffset);
        Assert.Equal(19, events[0].Hour);
        Assert.Contains("英語", events[0].Title, StringComparison.Ordinal);
        Assert.Contains("day_offset", LocalScheduleParser.ToAddEventArgumentsJson(events), StringComparison.Ordinal);
    }

    [Fact]
    public void LooksLikeScheduleWrite_Tomorrow()
    {
        Assert.True(LocalScheduleParser.LooksLikeScheduleWrite("明日19時から英語の勉強を追加して"));
    }
}

public class AssistantLocalFastPathServiceTests
{
    [Fact]
    public async Task TodayAgenda_CompletesWithoutJevOrModel()
    {
        var day = new DateOnly(2026, 10, 10);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "Study",
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), offset)
            }
        ]);
        var calendar = new CalendarCommandService(
            new CalendarService([local]),
            new FixedNow(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));
        var counting = new CountingAiProvider();
        var jev = new CountingJev();
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => counting,
            () => new AssistantSettings { MaxSteps = 5 },
            jev: jev);

        var turn = await assistant.SendAsync("今日の予定を見せて");
        Assert.True(turn.Succeeded);
        Assert.Contains("Study", turn.AssistantText, StringComparison.Ordinal);
        Assert.Equal(0, counting.Calls);
        Assert.Equal(0, jev.Calls);
        Assert.NotNull(turn.RouteTrace);
        Assert.True(turn.RouteTrace!.CompletedLocally);
        Assert.False(turn.RouteTrace.CalledJev);
        Assert.False(turn.RouteTrace.CalledConversationModel);
    }

    [Fact]
    public async Task ProjectList_CompletesWithoutModel()
    {
        var store = new MemoryCreativeProjectStore();
        store.Save(new CreativeProjectDocument
        {
            Projects =
            [
                new CreativeProject { Id = "a", Name = "Alpha", ProjectType = CreativeProjectType.Other }
            ]
        });
        var projects = new CreativeProjectService(store);
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects,
            new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => true));
        var counting = new CountingAiProvider();
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, creative: creative),
            () => counting,
            () => new AssistantSettings { MaxSteps = 5 });

        var turn = await assistant.SendAsync("プロジェクト一覧を見せて");
        Assert.True(turn.Succeeded);
        Assert.Equal(0, counting.Calls);
        Assert.Contains("Alpha", turn.AssistantText ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousRequest_CallsJev_NotLocalFalseSuccess()
    {
        var counting = new CountingAiProvider
        {
            Reply = "I need more detail about which environment you mean."
        };
        var jev = new CountingJev();
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => counting,
            () => new AssistantSettings { MaxSteps = 5 },
            jev: jev);

        var turn = await assistant.SendAsync("いつもの勉強環境にして");
        Assert.True(jev.Calls >= 1);
        Assert.NotNull(turn.RouteTrace);
        Assert.True(turn.RouteTrace!.CalledJev);
    }

    [Fact]
    public async Task JevTimeout_DoesNotBlockLocalAgenda()
    {
        var day = new DateOnly(2026, 10, 10);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "Guitar",
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
            }
        ]);
        var calendar = new CalendarCommandService(
            new CalendarService([local]),
            new FixedNow(new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), offset)));
        var counting = new CountingAiProvider();
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => counting,
            () => new AssistantSettings { MaxSteps = 5 },
            jev: new ThrowingJev());

        var turn = await assistant.SendAsync("今日の予定を見せて");
        Assert.True(turn.Succeeded);
        Assert.Equal(0, counting.Calls);
        Assert.Contains("Guitar", turn.AssistantText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeminiUnavailable_DoesNotBlockLocalAgenda()
    {
        var day = new DateOnly(2026, 10, 10);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "Focus",
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(11, 0)), offset)
            }
        ]);
        var calendar = new CalendarCommandService(
            new CalendarService([local]),
            new FixedNow(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset)));
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => new BrokenGeminiProvider(),
            () => new AssistantSettings { ProviderId = AssistantProviderIds.Gemini, MaxSteps = 5 });

        var turn = await assistant.SendAsync("今日の予定を見せて");
        Assert.True(turn.Succeeded);
        Assert.Contains("Focus", turn.AssistantText, StringComparison.Ordinal);
        Assert.False(turn.RouteTrace!.CalledConversationModel);
    }

    [Fact]
    public async Task TomorrowSchedule_QueuesConfirmWithDayOffset_NoModel()
    {
        var counting = new CountingAiProvider();
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => counting,
            () => new AssistantSettings { MaxSteps = 5 });

        var turn = await assistant.SendAsync("明日19時から英語の勉強を追加して");
        Assert.True(turn.Succeeded);
        Assert.NotNull(turn.PendingConfirmation);
        Assert.Contains("day_offset", turn.PendingConfirmation!.Actions[0].ArgumentsJson, StringComparison.Ordinal);
        Assert.Equal(0, counting.Calls);
        Assert.True(turn.RouteTrace!.CompletedLocally);
    }

    private sealed class FixedNow(DateTimeOffset now) : ITimeProvider
    {
        public DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public DateTimeOffset GetLocalNow() => now;
    }

    private sealed class CountingAiProvider : IAiProvider
    {
        public int Calls { get; private set; }

        public string Reply { get; init; } = "model-should-not-run";

        public string ProviderId => AssistantProviderIds.Local;

        public string DisplayName => "Counting";

        public Task<AiProviderResponse> ChatAsync(
            IReadOnlyList<AiMessage> messages,
            IReadOnlyList<AssistantToolDefinition> tools,
            string? model,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(AiProviderResponse.Text(Reply));
        }
    }

    private sealed class BrokenGeminiProvider : IAiProvider
    {
        public string ProviderId => AssistantProviderIds.Gemini;

        public string DisplayName => "Broken Gemini";

        public Task<AiProviderResponse> ChatAsync(
            IReadOnlyList<AiMessage> messages,
            IReadOnlyList<AssistantToolDefinition> tools,
            string? model,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Gemini must not be called for local agenda.");
    }

    private sealed class CountingJev : IJevDecisionService
    {
        public int Calls { get; private set; }

        public bool IsConfigured => true;

        public Task<JevDecisionOutcome> DecideAsync(
            JevObservation observation,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var decision = new JevDecision
            {
                IsValid = true,
                Situation = JevSituation.Study,
                NextStep = JevNextStep.AskConfirmation,
                Gate = JevGate.Confirm,
                SituationConfidence = 0.9,
                NextStepConfidence = 0.9,
                GateConfidence = 0.9
            };
            return Task.FromResult(new JevDecisionOutcome
            {
                Decision = decision,
                Verdict = JevSafetyGate.Evaluate(decision),
                UsedFallback = false
            });
        }

        public Task<JevConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JevConnectionTest.Ok("jev-latest"));
    }

    private sealed class ThrowingJev : IJevDecisionService
    {
        public bool IsConfigured => true;

        public Task<JevDecisionOutcome> DecideAsync(
            JevObservation observation,
            CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Jev timed out");

        public Task<JevConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JevConnectionTest.Fail("timeout"));
    }
}
