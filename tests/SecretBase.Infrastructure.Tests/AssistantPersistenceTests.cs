using System.Net;
using System.Text;
using SecretBase.Core.Assistant;
using SecretBase.Infrastructure.Assistant;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class AssistantSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_AtomicWrite_NeverPersistsApiKey()
    {
        var path = Path.Combine(CreateTempDir(), "assistant.json");
        try
        {
            var store = new JsonAssistantSettingsStore(path);
            store.Save(new AssistantSettings
            {
                ProviderId = AssistantProviderIds.OpenAi,
                Model = "gpt-4o-mini"
            });
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));

            var json = File.ReadAllText(path);
            Assert.Contains("providerId", json, StringComparison.Ordinal);
            Assert.Contains("gpt-4o-mini", json, StringComparison.Ordinal);
            Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sk-", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);

            var restored = new JsonAssistantSettingsStore(path).LoadOrCreate();
            Assert.Equal(AssistantSettings.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Equal(AssistantProviderIds.OpenAi, restored.ProviderId);
            Assert.Equal("gpt-4o-mini", restored.Model);

            File.WriteAllText(path, "{ not-json");
            var recovered = new JsonAssistantSettingsStore(path).LoadOrCreate();
            Assert.Equal(AssistantProviderIds.OpenAi, recovered.ProviderId);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class AssistantCredentialAndProviderTests
{
    [Fact]
    public async Task Factory_UsesCredentialStore_NeverLogsKey_AndStubsGeminiLocal()
    {
        var secrets = new MemorySecureSecretStore();
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"pong"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            }
        };
        var factory = new AssistantProviderFactory(secrets, new HttpClient(handler));

        var gemini = factory.Create(new AssistantSettings { ProviderId = AssistantProviderIds.Gemini });
        var geminiReply = await gemini.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            "gemini");
        Assert.Equal(AiProviderStatus.Unavailable, geminiReply.Status);

        var local = factory.Create(new AssistantSettings { ProviderId = AssistantProviderIds.Local });
        var localReply = await local.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            "llama");
        Assert.Equal(AiProviderStatus.Unavailable, localReply.Status);

        var missing = factory.Create(new AssistantSettings());
        var missingReply = await missing.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            AssistantSettings.DefaultOpenAiModel);
        Assert.Equal(AiProviderStatus.NotConfigured, missingReply.Status);
        Assert.Equal(AssistantUserMessages.NotConfigured, missingReply.ErrorMessage);
        Assert.Null(handler.LastRequest);

        secrets.SetSecret(AssistantSecretKeys.OpenAiApiKey, "sk-test-not-for-git");
        var openai = factory.Create(new AssistantSettings());
        var ok = await openai.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "Reply with the single word pong." }],
            BuiltinAssistantToolRegistry.Instance.Tools,
            AssistantSettings.DefaultOpenAiModel);
        Assert.Equal(AiProviderStatus.Ok, ok.Status);
        Assert.Equal("pong", ok.Content);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Contains("calendar_get_today", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-not-for-git", ok.Content ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-not-for-git", ok.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProvider_Maps401ToNotConfigured_And500ToUnavailable()
    {
        var unauthorized = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"invalid_api_key"}}""", Encoding.UTF8, "application/json")
            }
        };
        var provider = new OpenAiAssistantProvider(new HttpClient(unauthorized), () => "sk-bad");
        var missing = await provider.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            "gpt-4o-mini");
        Assert.Equal(AiProviderStatus.NotConfigured, missing.Status);
        Assert.DoesNotContain("sk-bad", missing.ErrorMessage ?? string.Empty, StringComparison.Ordinal);

        var down = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream", Encoding.UTF8, "text/plain")
            }
        };
        var unavailable = new OpenAiAssistantProvider(new HttpClient(down), () => "sk-bad");
        var fail = await unavailable.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            "gpt-4o-mini");
        Assert.Equal(AiProviderStatus.Unavailable, fail.Status);
        Assert.Equal(AssistantUserMessages.Unavailable, fail.ErrorMessage);
        Assert.DoesNotContain("upstream", fail.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProvider_ParsesToolCalls()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"choices":[{"message":{"content":null,"tool_calls":[{"id":"call_1","function":{"name":"calendar_get_today","arguments":"{}"}}]}}]}
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };
        var provider = new OpenAiAssistantProvider(new HttpClient(handler), () => "sk-test");
        var reply = await provider.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "today" }],
            BuiltinAssistantToolRegistry.Instance.Tools,
            "gpt-4o-mini");
        Assert.Equal(AiProviderStatus.Ok, reply.Status);
        Assert.Single(reply.ToolCalls);
        Assert.Equal(AssistantToolNames.CalendarGetToday, reply.ToolCalls[0].Name);
        Assert.Equal("call_1", reply.ToolCalls[0].Id);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return Response;
        }
    }
}
