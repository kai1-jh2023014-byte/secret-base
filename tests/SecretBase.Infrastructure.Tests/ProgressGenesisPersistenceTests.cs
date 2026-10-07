using System.Net;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Progress;
using SecretBase.Core.Widgets.Progress;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Progress;

namespace SecretBase.Infrastructure.Tests;

public class ProgressGenesisPersistenceTests
{
    [Fact]
    public void SaveAndLoad_AtomicRoundTrip_SeedsWhenMissing()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "progress-genesis.json");
            var store = new JsonProgressGenesisStore(path);
            var seeded = store.LoadOrCreate();
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.True(seeded.Progress.Percent >= 0);
            Assert.Equal(ProgressGenesisSourceKinds.LocalJson, seeded.SourceKind);

            seeded.Progress.Percent = 61;
            seeded.Progress.Status = "Shipping";
            seeded.Genesis.Phase = "Expansion";
            seeded.Genesis.Stage = 3;
            seeded.UpdatedAt = new DateTimeOffset(2026, 10, 7, 8, 30, 0, TimeSpan.Zero);
            store.Save(seeded);

            Assert.False(File.Exists(path + ".tmp"));
            var restored = new JsonProgressGenesisStore(path).LoadOrCreate();
            Assert.Equal(61, restored.Progress.Percent);
            Assert.Equal("Shipping", restored.Progress.Status);
            Assert.Equal("Expansion", restored.Genesis.Phase);
            Assert.Equal(3, restored.Genesis.Stage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CorruptJson_ReturnsEmptySnapshot()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "progress-genesis.json");
            File.WriteAllText(path, "{ not-json");
            var store = new JsonProgressGenesisStore(path);
            var snapshot = store.LoadOrCreate();
            Assert.Equal(0, snapshot.Progress.Percent);
            Assert.Equal("Not started", snapshot.Progress.Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalProvider_ReadsStore()
    {
        var memory = new MemoryProgressGenesisProvider(
            new ProgressGenesisSnapshot
            {
                Progress = new ProgressTrack { Percent = 12, Status = "Warmup" },
                Genesis = GenesisTrack.CreateDefault()
            });
        // Re-save through JSON store to exercise LocalJson provider + file.
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "progress-genesis.json");
            var store = new JsonProgressGenesisStore(path);
            store.Save(memory.LoadOrCreate());
            var provider = new LocalJsonProgressGenesisProvider(store);
            var snapshot = await provider.GetAsync();
            Assert.Equal(12, snapshot.Progress.Percent);
            Assert.Equal(ProgressGenesisSourceKinds.LocalJson, snapshot.SourceKind);
            Assert.Equal("local-json", provider.ProviderId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Factory_UsesLocalWhenRemoteUrlMissing()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "progress-genesis.json");
            var store = new JsonProgressGenesisStore(path);
            var provider = ProgressGenesisProviderFactory.Create(
                ProgressWidgetConfiguration.CreateDefault(),
                store);
            Assert.Equal("local-json", provider.ProviderId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Factory_UsesHttpWhenRemoteUrlSet()
    {
        var config = new ProgressWidgetConfiguration
        {
            RemoteUrl = "https://example.com/progress-genesis.json"
        };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("http-json", provider.ProviderId);
    }

    [Fact]
    public void Factory_FallsBackToLocalOnInvalidRemoteUrl()
    {
        var config = new ProgressWidgetConfiguration { RemoteUrl = "not a url" };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("local-json", provider.ProviderId);
    }

    [Fact]
    public async Task HttpProvider_FallsBackWhenRequestFails()
    {
        var fallbackStore = new MemoryProgressGenesisProvider(
            new ProgressGenesisSnapshot
            {
                Progress = new ProgressTrack { Percent = 44, Status = "Fallback" },
                Genesis = GenesisTrack.CreateDefault()
            });
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var provider = new HttpProgressGenesisProvider(
            "https://example.com/progress-genesis.json",
            http,
            new LocalJsonProgressGenesisProvider(fallbackStore));

        var snapshot = await provider.GetAsync();
        Assert.Equal(44, snapshot.Progress.Percent);
    }

    [Fact]
    public async Task HttpProvider_ParsesSuccessfulPayload()
    {
        var payload = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Title = "Progress", Percent = 88, Status = "Live" },
            Genesis = new GenesisTrack
            {
                Title = "Genesis",
                Phase = "Orbit",
                Stage = 4,
                StageCount = 5,
                Percent = 70,
                Status = "Advancing"
            }
        };
        var json = JsonSerializer.Serialize(payload, SecretBaseJson.CreateOptions());
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var provider = new HttpProgressGenesisProvider(
            "https://example.com/progress-genesis.json",
            http,
            new MemoryProgressGenesisProvider());

        var snapshot = await provider.GetAsync();
        Assert.Equal(88, snapshot.Progress.Percent);
        Assert.Equal("Orbit", snapshot.Genesis.Phase);
        Assert.Equal(ProgressGenesisSourceKinds.Http, snapshot.SourceKind);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
