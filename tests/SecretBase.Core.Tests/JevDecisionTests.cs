using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Jev;
using SecretBase.Core.Security;

namespace SecretBase.Core.Tests;

public class JevDecisionTests
{
    [Fact]
    public void ApiKey_IsSeparateFromConversationProviders()
    {
        Assert.NotEqual(AssistantSecretKeys.OpenAiApiKey, JevSecretKeys.ApiKey);
        Assert.NotEqual(AssistantSecretKeys.GeminiApiKey, JevSecretKeys.ApiKey);
        Assert.DoesNotContain("OpenAI", JevSecretKeys.ApiKey, StringComparison.Ordinal);
        Assert.DoesNotContain("Gemini", JevSecretKeys.ApiKey, StringComparison.Ordinal);
        Assert.Null(typeof(AssistantSettings).GetProperty("ApiKey"));
        Assert.Null(typeof(AssistantSettings).GetProperty("JevApiKey"));
        Assert.DoesNotContain(
            typeof(AssistantProviderIds).GetFields().Select(field => field.GetValue(null)?.ToString()),
            id => string.Equals(id, "jev", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KeyStore_DoesNotReuseConversationSecrets()
    {
        var store = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AssistantSecretKeys.OpenAiApiKey] = "sk-conversation",
            [AssistantSecretKeys.GeminiApiKey] = "AIza-conversation"
        };
        var client = new RecordingJevClient();
        var service = new JevDecisionService(client, () =>
            store.TryGetValue(JevSecretKeys.ApiKey, out var value) ? value : null);

        Assert.False(service.IsConfigured);
        store[JevSecretKeys.ApiKey] = "jv_live_decision";
        Assert.True(service.IsConfigured);
        Assert.Equal("sk-conversation", store[AssistantSecretKeys.OpenAiApiKey]);
        Assert.Equal("AIza-conversation", store[AssistantSecretKeys.GeminiApiKey]);
        Assert.Equal("jv_live_decision", store[JevSecretKeys.ApiKey]);
    }

    [Fact]
    public void Contract_UsesDocumentedEndpointsAndChoiceQuestions()
    {
        Assert.Equal(
            "https://jevtypesafeai.com/api/v1/decide",
            JevDecisionContract.ResolveEndpoint("jv_live_example"));
        Assert.Equal(
            "https://api.typesafe.ai/v1/systemone",
            JevDecisionContract.ResolveEndpoint("typesafe-key"));

        var json = JevDecisionContract.BuildDecisionRequest("time: Monday");
        Assert.Contains("\"model\":\"jev-latest\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"choice\"", json, StringComparison.Ordinal);
        Assert.Contains("\"study\"", json, StringComparison.Ordinal);
        Assert.Contains("\"prepare_workspace\"", json, StringComparison.Ordinal);
        Assert.Contains("\"deny\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("jv_live_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Observation_OmitsSecretsAndPaths()
    {
        var state = JevObservationFormatter.Format(new JevObservation
        {
            LocalNow = new DateTimeOffset(2026, 10, 4, 5, 0, 0, TimeSpan.FromHours(9)),
            Intent = AssistantIntentKind.ActionRequest,
            Message = "use sk-secret to open C:\\Users\\me",
            WorkspaceName = @"D:\src\private",
            CalendarTodayCount = 1,
            TodoCount = 2
        });

        Assert.Contains("calendar_today_count: 1", state, StringComparison.Ordinal);
        Assert.Contains("todo_count: 2", state, StringComparison.Ordinal);
        Assert.Contains("message: [redacted]", state, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret", state, StringComparison.Ordinal);
        Assert.DoesNotContain(@"D:\src\private", state, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace:", state, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_ReadsDocumentedChoiceAnswers()
    {
        var decision = JevDecisionParser.Parse(
            """
            {
              "model": "jev-1.13.0",
              "answers": {
                "situation": { "type": "choice", "choice": "coding", "confidence": 0.91 },
                "next_step": { "type": "choice", "choice": "prepare_workspace", "confidence": 0.8 },
                "gate": { "type": "choice", "choice": "allow", "confidence": 0.77 }
              }
            }
            """);

        Assert.True(decision.IsValid);
        Assert.Equal(JevSituation.Coding, decision.Situation);
        Assert.Equal(JevNextStep.PrepareWorkspace, decision.NextStep);
        Assert.Equal(JevGate.Allow, decision.Gate);
        Assert.Equal("jev-1.13.0", decision.Model);
    }

    [Fact]
    public void Parser_RejectsActionOutsideTheContract()
    {
        var decision = JevDecisionParser.Parse(
            """
            {
              "answers": {
                "situation": { "type": "choice", "choice": "coding", "confidence": 1 },
                "next_step": { "type": "choice", "choice": "delete_file", "confidence": 1 },
                "gate": { "type": "choice", "choice": "allow", "confidence": 1 }
              }
            }
            """);

        Assert.False(decision.IsValid);
        Assert.Equal("delete_file", decision.RejectedValue);
        var verdict = JevSafetyGate.Evaluate(decision);
        Assert.Equal(JevSafetyKind.Deny, verdict.Kind);
        Assert.False(JevSafetyGate.MayAutoExecute(verdict, SafeAutoTool()));
    }

    [Fact]
    public void SafetyGate_ConfirmDoesNotAutoExecute()
    {
        var decision = Valid("music", "start_focus", "confirm", 0.95);
        var verdict = JevSafetyGate.Evaluate(decision);
        Assert.Equal(JevSafetyKind.NeedsConfirmation, verdict.Kind);
        Assert.False(JevSafetyGate.MayAutoExecute(verdict, SafeAutoTool()));
        Assert.Equal(JevToolPermission.Confirm, JevSafetyGate.PermissionFor(verdict, SafeAutoTool()));
    }

    [Fact]
    public void SafetyGate_AllowStillUsesExistingAutomationGate()
    {
        var decision = Valid("coding", "prepare_workspace", "allow", 0.9);
        var verdict = JevSafetyGate.Evaluate(decision);
        Assert.Equal(JevSafetyKind.Allow, verdict.Kind);
        Assert.True(JevSafetyGate.MayAutoExecute(verdict, SafeAutoTool()));
        Assert.False(JevSafetyGate.MayAutoExecute(verdict, DeleteTool()));
        Assert.Equal(AutomationSafetyLevel.ExplicitConfirmationRequired, AutomationSafety.ForTool(DeleteTool()));
        Assert.Equal(JevToolPermission.Confirm, JevSafetyGate.PermissionFor(verdict, DeleteTool()));
    }

    [Fact]
    public void SafetyGate_LowConfidenceAsksFirst()
    {
        var decision = Valid("coding", "prepare_workspace", "allow", 0.2);
        var verdict = JevSafetyGate.Evaluate(decision);
        Assert.Equal(JevSafetyKind.NeedsConfirmation, verdict.Kind);
        Assert.False(JevSafetyGate.MayAutoExecute(verdict, SafeAutoTool()));
    }

    [Fact]
    public void Fallback_DoesNotAutoRunWhenJevIsMissing()
    {
        var decision = JevDecisionFallback.FromObservation(new JevObservation
        {
            Intent = AssistantIntentKind.ActionRequest,
            Message = "曲を変えて"
        });
        Assert.True(decision.FromFallback);
        Assert.Equal(JevSituation.Music, decision.Situation);
        Assert.Equal(JevNextStep.AskConfirmation, decision.NextStep);
        Assert.Equal(JevGate.Confirm, decision.Gate);
        Assert.False(JevSafetyGate.MayAutoExecute(JevSafetyGate.Evaluate(decision), SafeAutoTool()));
    }

    [Fact]
    public async Task Service_MissingKey_DoesNotCallJev_AndDoesNotThrow()
    {
        var client = new RecordingJevClient();
        var service = new JevDecisionService(client, () => null);
        var outcome = await service.DecideAsync(new JevObservation
        {
            Intent = AssistantIntentKind.Question,
            Message = "hello"
        });

        Assert.Equal(0, client.Calls);
        Assert.True(outcome.UsedFallback);
        Assert.Equal(JevNextStep.None, outcome.Decision.NextStep);
        var test = await service.TestConnectionAsync();
        Assert.True(test.NeedsConfiguration);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Service_ClientFailure_FallsBackWithoutCrashing()
    {
        var client = new RecordingJevClient { Throw = true };
        var service = new JevDecisionService(client, () => "jv_live_example");
        var outcome = await service.DecideAsync(new JevObservation
        {
            Intent = AssistantIntentKind.ActionRequest,
            Message = "open the project"
        });

        Assert.True(outcome.UsedFallback);
        Assert.Equal(JevGate.Confirm, outcome.Decision.Gate);
        Assert.Equal(JevUserMessages.Unavailable, outcome.Error);
    }

    [Fact]
    public async Task Service_InvalidDecision_IsDenied()
    {
        var client = new RecordingJevClient
        {
            Result = JevClientResult.Ok(
                """
                {"answers":{"situation":{"type":"choice","choice":"coding"},"next_step":{"type":"choice","choice":"delete_file"},"gate":{"type":"choice","choice":"allow"}}}
                """)
        };
        var service = new JevDecisionService(client, () => "jv_live_example");
        var outcome = await service.DecideAsync(new JevObservation { Message = "delete it" });
        Assert.False(outcome.UsedFallback);
        Assert.False(outcome.Decision.IsValid);
        Assert.Equal(JevSafetyKind.Deny, outcome.Verdict.Kind);
    }

    [Fact]
    public async Task Service_TestConnection_ReadsNoulAnswer()
    {
        var client = new RecordingJevClient
        {
            Result = JevClientResult.Ok(
                """
                {"model":"jev-1.13.0","answers":{"ready":{"type":"noul","noul":0.2}}}
                """)
        };
        var service = new JevDecisionService(client, () => "typesafe-key");
        var test = await service.TestConnectionAsync();
        Assert.True(test.Succeeded);
        Assert.Contains("jev-1.13.0", test.Message, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"noul\"", client.LastRequest!, StringComparison.Ordinal);
        Assert.DoesNotContain("typesafe-key", client.LastRequest!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assistant_DenyStopsSafeAuto_ConversationProviderStillAnswers()
    {
        var executor = new CountingExecutor();
        var registry = new SingleToolRegistry(SafeAutoTool());
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.WorkspacePrepare, ArgumentsJson = "{}" }
            ]),
            AiProviderResponse.Text("I will not run that.")
        ]);
        var decision = JevDecisionParser.Parse(
            """
            {"answers":{"situation":{"type":"choice","choice":"coding","confidence":1},"next_step":{"type":"choice","choice":"delete_file","confidence":1},"gate":{"type":"choice","choice":"allow","confidence":1}}}
            """);
        var service = new AssistantService(
            registry,
            executor,
            () => provider,
            jev: new FixedJev(decision));

        var result = await service.SendAsync("prepare my workspace");
        Assert.True(result.Succeeded);
        Assert.Equal(0, executor.Calls);
        Assert.Contains("I will not run that.", result.AssistantText, StringComparison.Ordinal);
        Assert.Contains(result.Activities, activity => activity.Text.Contains("denied", StringComparison.OrdinalIgnoreCase)
                                                       || activity.Domain == "Jev");
    }

    [Fact]
    public async Task Assistant_ConfirmDoesNotAutoRunSafeAuto()
    {
        var executor = new CountingExecutor();
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.WorkspacePrepare, ArgumentsJson = "{}" }
            ])
        ]);
        var decision = Valid("coding", "prepare_workspace", "confirm", 0.99);
        var service = new AssistantService(
            new SingleToolRegistry(SafeAutoTool()),
            executor,
            () => provider,
            jev: new FixedJev(decision));

        var result = await service.SendAsync("prepare");
        Assert.NotNull(result.PendingConfirmation);
        Assert.Equal(0, executor.Calls);
        Assert.False(result.ShouldLaunch);
    }

    [Fact]
    public async Task Assistant_WithoutJev_StillRunsConversation()
    {
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new ScriptedAiProvider([AiProviderResponse.Text("pong")]));

        var result = await service.SendAsync("hello");
        Assert.True(result.Succeeded);
        Assert.Equal("pong", result.AssistantText);
    }

    [Fact]
    public async Task Assistant_FallbackDoesNotChangeExistingAutoRun()
    {
        var executor = new CountingExecutor();
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.WorkspacePrepare, ArgumentsJson = "{}" }
            ]),
            AiProviderResponse.Text("prepared")
        ]);
        var service = new AssistantService(
            new SingleToolRegistry(SafeAutoTool()),
            executor,
            () => provider,
            jev: new FixedJev(JevDecisionFallback.FromObservation(new JevObservation
            {
                Intent = AssistantIntentKind.ActionRequest,
                Message = "prepare"
            }),
            usedFallback: true));

        var result = await service.SendAsync("prepare the workspace");
        Assert.True(result.Succeeded);
        Assert.Equal(1, executor.Calls);
        Assert.Null(result.PendingConfirmation);
    }

    private static JevDecision Valid(string situation, string next, string gate, double confidence) =>
        JevDecisionParser.Parse(
            $$"""
            {
              "answers": {
                "situation": { "type": "choice", "choice": "{{situation}}", "confidence": {{confidence}} },
                "next_step": { "type": "choice", "choice": "{{next}}", "confidence": {{confidence}} },
                "gate": { "type": "choice", "choice": "{{gate}}", "confidence": {{confidence}} }
              }
            }
            """);

    private static AssistantToolDefinition SafeAutoTool() => new()
    {
        Name = AssistantToolNames.WorkspacePrepare,
        Description = "Prepare workspace display.",
        RiskLevel = ActionPrivilege.SafeAction,
        Capability = AssistantToolCapability.SafeAuto
    };

    private static AssistantToolDefinition DeleteTool() => new()
    {
        Name = AssistantToolNames.FilesDelete,
        Description = "Unregister a Secret Base item.",
        RiskLevel = ActionPrivilege.UserConfirmationRequired,
        Capability = AssistantToolCapability.RequiresConfirmation
    };

    private sealed class RecordingJevClient : IJevDecisionClient
    {
        public int Calls { get; private set; }

        public bool Throw { get; init; }

        public JevClientResult Result { get; init; } = JevClientResult.Fail(JevClientFailure.Unavailable, "down");

        public string? LastRequest { get; private set; }

        public Task<JevClientResult> DecideAsync(string apiKey, string requestJson, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = requestJson;
            if (Throw)
            {
                throw new InvalidOperationException("offline");
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FixedJev : IJevDecisionService
    {
        private readonly JevDecisionOutcome _outcome;

        public FixedJev(JevDecision decision, bool usedFallback = false)
        {
            _outcome = new JevDecisionOutcome
            {
                Decision = decision,
                Verdict = JevSafetyGate.Evaluate(decision),
                UsedFallback = usedFallback
            };
        }

        public bool IsConfigured => true;

        public Task<JevDecisionOutcome> DecideAsync(JevObservation observation, CancellationToken cancellationToken = default) =>
            Task.FromResult(_outcome);

        public Task<JevConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JevConnectionTest.Ok("jev-latest"));
    }

    private sealed class SingleToolRegistry(AssistantToolDefinition tool) : IAiToolRegistry
    {
        public IReadOnlyList<AssistantToolDefinition> Tools { get; } = [tool];

        public AssistantToolDefinition? Find(string? name) =>
            string.Equals(name, tool.Name, StringComparison.Ordinal) ? tool : null;
    }

    private sealed class CountingExecutor : IAiToolExecutor
    {
        public int Calls { get; private set; }

        public Task<AssistantToolResult> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(AssistantToolResult.Ok("ran", activity: "done"));
        }
    }
}
