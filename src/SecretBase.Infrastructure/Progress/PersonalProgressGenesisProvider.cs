using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Loads Progress from the local <c>progress</c> FastAPI
/// (<c>/api/problems</c>, <c>/api/attempts</c>) and Genesis from MusicLab
/// status file / Desktop launchers / optional status URL.
/// </summary>
public sealed class PersonalProgressGenesisProvider : IProgressGenesisProvider
{
    private readonly HttpClient _http;
    private readonly string _progressApiBase;
    private readonly string? _genesisStatusUrl;
    private readonly string? _genesisRootOverride;
    private readonly IProgressGenesisProvider _fallback;
    private readonly JsonSerializerOptions _options;

    public PersonalProgressGenesisProvider(
        string? progressApiBase = null,
        string? genesisStatusUrl = null,
        string? genesisRoot = null,
        HttpClient? httpClient = null,
        IProgressGenesisProvider? fallback = null,
        JsonSerializerOptions? options = null)
    {
        var root = string.IsNullOrWhiteSpace(progressApiBase)
            ? ProgressLearningMapper.DefaultApiBase
            : progressApiBase.Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Progress API base must be an absolute http(s) URI.", nameof(progressApiBase));
        }

        // Progress is a local learning server — allow loopback HTTP.
        if (uri.Scheme == Uri.UriSchemeHttp
            && !uri.IsLoopback
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Progress API HTTP is only allowed on localhost.", nameof(progressApiBase));
        }

        _progressApiBase = uri.AbsoluteUri.TrimEnd('/');
        _genesisStatusUrl = string.IsNullOrWhiteSpace(genesisStatusUrl) ? null : genesisStatusUrl.Trim();
        _genesisRootOverride = string.IsNullOrWhiteSpace(genesisRoot) ? null : genesisRoot.Trim();
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _fallback = fallback ?? new LocalJsonProgressGenesisProvider();
        _options = options ?? SecretBaseJson.CreateOptions();
    }

    public string ProviderId => "personal-systems";

    public string DisplayName => "Progress + Genesis";

    public string SourceKind => ProgressGenesisSourceKinds.PersonalSystems;

    public async Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        ProgressTrack? progressTrack = null;
        try
        {
            progressTrack = await LoadProgressTrackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            progressTrack = null;
        }

        var genesisTrack = await LoadGenesisTrackAsync(cancellationToken).ConfigureAwait(false);

        if (progressTrack is null)
        {
            // Keep Genesis live even when Progress API is down.
            var fallback = await _fallback.GetAsync(cancellationToken).ConfigureAwait(false);
            progressTrack = fallback.Progress;
            progressTrack.Title = "Progress";
            if (string.IsNullOrWhiteSpace(progressTrack.Status)
                || progressTrack.Status.Contains("Not started", StringComparison.OrdinalIgnoreCase)
                || progressTrack.Status.Contains("On track", StringComparison.OrdinalIgnoreCase))
            {
                progressTrack.Status = "progress API offline (start uvicorn :8001)";
            }
        }

        return GenesisMusicLabMapper.Combine(progressTrack, genesisTrack, DateTimeOffset.UtcNow);
    }

    private async Task<ProgressTrack> LoadProgressTrackAsync(CancellationToken cancellationToken)
    {
        var problemsTask = _http.GetFromJsonAsync<List<ProgressProblemDto>>(
            $"{_progressApiBase}/api/problems",
            _options,
            cancellationToken);
        var attemptsTask = _http.GetFromJsonAsync<List<ProgressAttemptDto>>(
            $"{_progressApiBase}/api/attempts",
            _options,
            cancellationToken);
        await Task.WhenAll(problemsTask, attemptsTask).ConfigureAwait(false);

        var problems = (problemsTask.Result ?? [])
            .Select(p => new ProgressProblemSummary
            {
                Id = p.Id ?? string.Empty,
                Title = p.Title ?? string.Empty,
                Difficulty = p.Difficulty ?? string.Empty
            })
            .ToList();
        var attempts = (attemptsTask.Result ?? [])
            .Select(a => new ProgressAttemptSummary
            {
                Id = a.Id ?? string.Empty,
                ProblemId = a.ProblemId ?? string.Empty,
                ProblemTitle = a.ProblemTitle ?? string.Empty,
                Result = a.Result ?? string.Empty,
                Score = a.Score,
                CreatedAt = ParseTime(a.CreatedAt)
            })
            .ToList();

        return ProgressLearningMapper.FromLearningData(problems, attempts);
    }

    private async Task<GenesisTrack> LoadGenesisTrackAsync(CancellationToken cancellationToken)
    {
        GenesisStatusFileDto? fileDto = null;
        var hasStatusFile = false;

        if (!string.IsNullOrWhiteSpace(_genesisStatusUrl))
        {
            try
            {
                fileDto = await _http
                    .GetFromJsonAsync<GenesisStatusFileDto>(_genesisStatusUrl, _options, cancellationToken)
                    .ConfigureAwait(false);
                hasStatusFile = fileDto is not null;
            }
            catch
            {
                fileDto = null;
            }
        }

        var roots = GenesisPathProbe.ResolveRoots(_genesisRootOverride);
        var installed = roots.Any(Directory.Exists);
        var hasLaunchers = GenesisPathProbe.HasDesktopMusicLabLaunchers()
                           || roots.Any(GenesisPathProbe.HasMusicLabScripts);
        var statusPath = GenesisPathProbe.FindStatusFile(roots);
        if (fileDto is null && statusPath is not null)
        {
            try
            {
                var json = await File.ReadAllTextAsync(statusPath, cancellationToken).ConfigureAwait(false);
                fileDto = JsonSerializer.Deserialize<GenesisStatusFileDto>(json, _options);
                hasStatusFile = fileDto is not null;
            }
            catch
            {
                hasStatusFile = false;
            }
        }

        var running = fileDto?.Running == true
                      || await ProbeGenesisHealthAsync(fileDto?.HealthUrl, cancellationToken).ConfigureAwait(false);

        var milestones = fileDto?.Milestones?
            .Where(m => !string.IsNullOrWhiteSpace(m.Label))
            .Select(m => GenesisMilestone.Create(m.Label!, m.IsComplete))
            .ToList() ?? [];

        // Fold Progress learning milestones into Genesis when Progress is up — kept separate visually;
        // Genesis milestones stay MusicLab-focused here.
        return GenesisMusicLabMapper.FromStatus(new GenesisMusicLabStatus
        {
            Phase = string.IsNullOrWhiteSpace(fileDto?.Phase) ? "MusicLab" : fileDto!.Phase!,
            Status = fileDto?.Status,
            Percent = fileDto?.Percent,
            Stage = fileDto?.Stage,
            StageCount = fileDto?.StageCount ?? 4,
            IsInstalled = installed || hasLaunchers || running,
            IsRunning = running,
            HasDesktopLaunchers = hasLaunchers,
            HasStatusFile = hasStatusFile,
            Milestones = milestones
        });
    }

    private async Task<bool> ProbeGenesisHealthAsync(string? healthUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            return false;
        }

        try
        {
            if (!Uri.TryCreate(healthUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttp
                && !uri.IsLoopback
                && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var response = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset? ParseTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var dto) ? dto : null;
    }

    private sealed class ProgressProblemDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }
    }

    private sealed class ProgressAttemptDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("problem_id")]
        public string? ProblemId { get; set; }

        [JsonPropertyName("problem_title")]
        public string? ProblemTitle { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }

    private sealed class GenesisStatusFileDto
    {
        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("percent")]
        public double? Percent { get; set; }

        [JsonPropertyName("stage")]
        public int? Stage { get; set; }

        [JsonPropertyName("stageCount")]
        public int? StageCount { get; set; }

        [JsonPropertyName("running")]
        public bool? Running { get; set; }

        [JsonPropertyName("healthUrl")]
        public string? HealthUrl { get; set; }

        [JsonPropertyName("milestones")]
        public List<GenesisMilestoneDto>? Milestones { get; set; }
    }

    private sealed class GenesisMilestoneDto
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("isComplete")]
        public bool IsComplete { get; set; }
    }
}

/// <summary>Resolves Genesis / MusicLab paths on the user's machine (WSL + Windows).</summary>
internal static class GenesisPathProbe
{
    public static IReadOnlyList<string> ResolveRoots(string? overrideRoot)
    {
        var list = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var full = Path.GetFullPath(path.Trim());
            if (!list.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(full);
            }
        }

        Add(overrideRoot);
        Add(Environment.GetEnvironmentVariable("SECRETBASE_GENESIS_ROOT"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "genesis"));
        Add(Path.Combine("/home", Environment.UserName, "genesis"));
        Add("/home/kabuya/genesis");
        // Windows reaching WSL home via UNC (user-provided Desktop copy source).
        Add(@"\\wsl.localhost\Ubuntu\home\kabuya\genesis");
        Add(Path.Combine(AppDataPaths.SettingsDirectory, "genesis"));
        return list;
    }

    public static bool HasMusicLabScripts(string root)
    {
        var start = Path.Combine(root, "scripts", "windows", "Start-MusicLab.bat");
        var stop = Path.Combine(root, "scripts", "windows", "Stop-MusicLab.bat");
        return File.Exists(start) || File.Exists(stop);
    }

    public static bool HasDesktopMusicLabLaunchers()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            {
                return false;
            }

            return File.Exists(Path.Combine(desktop, "Start-MusicLab.bat"))
                   || File.Exists(Path.Combine(desktop, "Stop-MusicLab.bat"));
        }
        catch
        {
            return false;
        }
    }

    public static string? FindStatusFile(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var candidate in new[]
                     {
                         Path.Combine(root, GenesisMusicLabMapper.DefaultStatusFileName),
                         Path.Combine(root, ".secret-base", GenesisMusicLabMapper.DefaultStatusFileName),
                         Path.Combine(root, "status.json"),
                         Path.Combine(AppDataPaths.SettingsDirectory, GenesisMusicLabMapper.DefaultStatusFileName)
                     })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        var appData = Path.Combine(AppDataPaths.SettingsDirectory, GenesisMusicLabMapper.DefaultStatusFileName);
        return File.Exists(appData) ? appData : null;
    }
}
