using System.Net;
using System.Text;
using SecretBase.Core.Assistant;
using SecretBase.Core.Jev;
using SecretBase.Infrastructure.Assistant;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Jev;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class JevApiClientTests
{
    [Fact]
    public async Task HostedKey_PostsBearerToDecideEndpoint_AndOmitsKeyFromBody()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK, """{"model":"jev-1.13.0","answers":{"ready":{"type":"noul","noul":0.1}}}""");
        var client = new JevApiClient(new HttpClient(handler));
        var result = await client.DecideAsync("jv_live_secret", JevDecisionContract.BuildConnectionCheckRequest());

        Assert.True(result.Succeeded);
        Assert.Equal("https://jevtypesafeai.com/api/v1/decide", handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("jv_live_secret", handler.AuthorizationValue);
        Assert.DoesNotContain("jv_live_secret", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfficialKey_PostsToSystemOne()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK, """{"answers":{"ready":{"type":"noul","noul":1}}}""");
        var client = new JevApiClient(new HttpClient(handler));
        await client.DecideAsync("typesafe-secret", JevDecisionContract.BuildConnectionCheckRequest());
        Assert.Equal("https://api.typesafe.ai/v1/systemone", handler.RequestUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, JevClientFailure.Authentication)]
    [InlineData(HttpStatusCode.PaymentRequired, JevClientFailure.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden, JevClientFailure.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway, JevClientFailure.Unavailable)]
    public async Task MapsDocumentedErrorStatus(HttpStatusCode status, JevClientFailure expected)
    {
        var handler = new CaptureHandler(status, """{"error":"jv_live_secret leaked","code":"insufficient_credits"}""");
        var client = new JevApiClient(new HttpClient(handler));
        var result = await client.DecideAsync("jv_live_secret", JevDecisionContract.BuildConnectionCheckRequest());
        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Failure);
        Assert.DoesNotContain("jv_live_secret", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversationFactory_DoesNotTreatJevAsAChatProvider()
    {
        var factory = new AssistantProviderFactory(new MemorySecureSecretStore());
        var provider = factory.CreateForProviderId("jev", new AssistantSettings());
        Assert.Equal(AssistantProviderIds.OpenAi, provider.ProviderId);
    }

    [Fact]
    public void LegacyAssistantSettings_StillLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"), "assistant.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(
                path,
                """
                { "schemaVersion": 2, "providerId": "gemini", "model": "gemini-2.0-flash", "maxSteps": 4, "requireConfirmationForActions": true }
                """);
            var restored = new JsonAssistantSettingsStore(path).LoadOrCreate();
            Assert.Equal(AssistantProviderIds.Gemini, restored.ProviderId);
            Assert.Equal("gemini-2.0-flash", restored.Model);
            Assert.Equal(4, restored.MaxSteps);
            Assert.Equal(AssistantSettings.CurrentSchemaVersion, restored.SchemaVersion);
            var json = File.ReadAllText(path);
            Assert.DoesNotContain("jev", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
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

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public CaptureHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public string? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationValue { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationValue = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
