using System.Net;
using System.Net.Http;
using System.Text;
using SecretBase.Core.Connectors;
using SecretBase.Infrastructure.Integration;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class HttpIntegrationTransportTests
{
    [Fact]
    public async Task AllowedHost_Get_ReturnsBody()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json")
        });
        var transport = new HttpIntegrationTransport(handler);
        var response = await transport.SendAsync(CreateRequest("https://api.example.invalid/v1/project"));
        Assert.True(response.Succeeded);
        Assert.Contains("ok", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeniedCleartextNonLoopback_Rejected()
    {
        var transport = new HttpIntegrationTransport(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        var response = await transport.SendAsync(CreateRequest("http://api.example.invalid/v1/project"));
        Assert.False(response.Succeeded);
        Assert.Equal("denied-scheme", response.ErrorCategory);
    }

    [Fact]
    public async Task Redirect_Refused()
    {
        var transport = new HttpIntegrationTransport(new StubHandler(new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://evil.example/") }
        }));
        var response = await transport.SendAsync(CreateRequest("https://api.example.invalid/v1/project"));
        Assert.True(response.Redirected);
        Assert.Equal("redirect", response.ErrorCategory);
    }

    [Fact]
    public async Task Oversized_Refused()
    {
        var transport = new HttpIntegrationTransport(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 8_000))
        }));
        var request = CreateRequest("https://api.example.invalid/v1/project") with { MaxResponseBytes = 64 };
        var response = await transport.SendAsync(request);
        Assert.True(response.Oversized);
    }

    [Fact]
    public async Task Unauthorized_AuthCategory()
    {
        var transport = new HttpIntegrationTransport(new StubHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var response = await transport.SendAsync(CreateRequest("https://api.example.invalid/v1/project"));
        Assert.False(response.Succeeded);
        Assert.Equal("auth", response.ErrorCategory);
        Assert.Equal(string.Empty, response.Body);
    }

    private static TransportRequest CreateRequest(string url) =>
        new(
            "demo.generic-rest",
            "GET",
            new Uri(url),
            new Dictionary<string, string>(),
            null,
            TimeSpan.FromSeconds(2),
            4_096);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }
}

public class JsonIntegrationRegistryStoreTests
{
    [Fact]
    public void Persistence_RoundTripAndCorruptRecovery()
    {
        var path = Path.Combine(Path.GetTempPath(), "sb-int-" + Guid.NewGuid().ToString("N"), "integrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            var store = new JsonIntegrationRegistryStore(path);
            Assert.NotNull(store.Find(IntegrationIds.Calendar));
            DemoManifests.TryRegisterKnown(store, approveDemos: true);
            store.SetEnabled(IntegrationIds.UserApp, true);

            var reloaded = new JsonIntegrationRegistryStore(path);
            Assert.NotNull(reloaded.Find(IntegrationIds.UserApp));
            Assert.Null(reloaded.Find(IntegrationIds.UserApp)!.CredentialReference);

            File.WriteAllText(path, "{not json");
            var recovered = new JsonIntegrationRegistryStore(path);
            Assert.NotNull(recovered.Find(IntegrationIds.Calendar));
            Assert.Contains(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"), _ => true);
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
            }
            catch (IOException)
            {
                // temp cleanup
            }
        }
    }
}
