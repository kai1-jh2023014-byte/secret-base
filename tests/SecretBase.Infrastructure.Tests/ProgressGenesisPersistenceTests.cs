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
            Assert.Equal(0, seeded.Progress.Percent);
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
    public void Factory_DefaultsToPersonalSystems()
    {
        var provider = ProgressGenesisProviderFactory.Create(
            ProgressWidgetConfiguration.CreateDefault(),
            new MemoryProgressGenesisProvider());
        Assert.Equal("personal-systems", provider.ProviderId);
    }

    [Fact]
    public void Factory_UsesAgentArenaWhenRequested()
    {
        var config = new ProgressWidgetConfiguration { Source = ProgressGenesisSources.AgentArena };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("agent-arena", provider.ProviderId);
    }

    [Fact]
    public void Factory_UsesLocalWhenSourceLocal()
    {
        var config = new ProgressWidgetConfiguration { Source = ProgressGenesisSources.Local };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("local-json", provider.ProviderId);
    }

    [Fact]
    public void Factory_UsesHttpWhenSourceHttpAndRemoteUrlSet()
    {
        var config = new ProgressWidgetConfiguration
        {
            Source = ProgressGenesisSources.Http,
            RemoteUrl = "https://example.com/progress-genesis.json"
        };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("http-json", provider.ProviderId);
    }

    [Fact]
    public void Factory_FallsBackToLocalOnInvalidRemoteUrl()
    {
        var config = new ProgressWidgetConfiguration
        {
            Source = ProgressGenesisSources.Http,
            RemoteUrl = "not a url"
        };
        var provider = ProgressGenesisProviderFactory.Create(config, new MemoryProgressGenesisProvider());
        Assert.Equal("local-json", provider.ProviderId);
    }

    [Fact]
    public async Task AgentArenaProvider_MapsSummaryPayload()
    {
        var summaryJson =
            """{"rounds":{"wins":40,"losses":22},"minted":{"total":36,"genesis":12,"ascension":24}}""";
        var topJson = """{"count":0,"leaders":[]}""";
        var handler = new StubHttpHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            var body = path.EndsWith("/winners/summary", StringComparison.Ordinal)
                ? summaryJson
                : topJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler);
        var provider = new AgentArenaProgressGenesisProvider(
            apiBase: "https://agent-arena-api.agentarenaonbase.workers.dev",
            httpClient: http,
            fallback: new MemoryProgressGenesisProvider());

        var snapshot = await provider.GetAsync();
        Assert.Equal(ProgressGenesisSourceKinds.AgentArena, snapshot.SourceKind);
        Assert.Equal(100.0 * 36 / AgentArenaProgressMapper.TotalCap, snapshot.Progress.Percent, 3);
        Assert.Equal(100.0 * 12 / AgentArenaProgressMapper.GenesisCap, snapshot.Genesis.Percent, 3);
    }

    [Fact]
    public async Task AgentArenaProvider_FallsBackWhenSummaryFails()
    {
        var fallback = new MemoryProgressGenesisProvider(
            new ProgressGenesisSnapshot
            {
                Progress = new ProgressTrack { Percent = 7, Status = "Offline" },
                Genesis = GenesisTrack.CreateDefault()
            });
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var provider = new AgentArenaProgressGenesisProvider(
            httpClient: new HttpClient(handler),
            fallback: new LocalJsonProgressGenesisProvider(fallback));

        var snapshot = await provider.GetAsync();
        Assert.Equal(7, snapshot.Progress.Percent);
    }

    [Fact]
    public async Task PersonalProvider_MapsReadinessDashboardZero()
    {
        var readinessJson =
            """{"professionalReadinessPercent":0,"requiredSkillsCompleted":0,"requiredSkillsTotal":29,"skillMap":[{"name":"Python","percent":0}]}""";
        var handler = new StubHttpHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/api/readiness", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(readinessJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var dir = CreateTempDir();
        try
        {
            var genesisRoot = Path.Combine(dir, "genesis");
            Directory.CreateDirectory(Path.Combine(genesisRoot, "scripts", "windows"));
            await File.WriteAllTextAsync(
                Path.Combine(genesisRoot, "scripts", "windows", "Start-MusicLab.bat"),
                "@echo off");

            var provider = new PersonalProgressGenesisProvider(
                progressApiBase: "http://127.0.0.1:8001",
                genesisRoot: genesisRoot,
                httpClient: new HttpClient(handler));

            var snapshot = await provider.GetAsync();
            Assert.Equal(0, snapshot.Progress.Percent);
            Assert.Contains("必須Skill 0/29", snapshot.Progress.Status);
            Assert.Equal(0, snapshot.Genesis.Percent); // launchers alone must not invent %
            Assert.Equal("MusicLab", snapshot.Genesis.Phase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryParseReadiness_AcceptsRequiredSkillsObject()
    {
        using var doc = JsonDocument.Parse(
            """{"readiness":{"professionalReadiness":0,"requiredSkills":{"completed":0,"total":29}}}""");
        Assert.True(PersonalProgressGenesisProvider.TryParseReadiness(doc.RootElement, out var summary));
        Assert.Equal(0, summary.ProfessionalReadinessPercent);
        Assert.Equal(0, summary.RequiredSkillsCompleted);
        Assert.Equal(29, summary.RequiredSkillsTotal);
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
