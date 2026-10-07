using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Loads Progress from the local <c>progress</c> app (Professional Readiness dashboard)
/// and Genesis from MusicLab status — never invents percentages.
/// </summary>
public sealed class PersonalProgressGenesisProvider : IProgressGenesisProvider
{
    private static readonly string[] ReadinessPaths =
    [
        "/api/readiness",
        "/api/dashboard",
        "/api/progress",
        "/api/skills/summary",
        "/api/me/progress"
    ];

    private readonly HttpClient _http;
    private readonly string _progressApiBase;
    private readonly string? _genesisStatusUrl;
    private readonly string? _genesisRootOverride;
    private readonly JsonSerializerOptions _options;

    public PersonalProgressGenesisProvider(
        string? progressApiBase = null,
        string? genesisStatusUrl = null,
        string? genesisRoot = null,
        HttpClient? httpClient = null,
        IProgressGenesisProvider? fallback = null,
        JsonSerializerOptions? options = null)
    {
        _ = fallback; // Personal systems must not fall back to demo seed percentages.
        var root = string.IsNullOrWhiteSpace(progressApiBase)
            ? ProgressLearningMapper.DefaultApiBase
            : progressApiBase.Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Progress API base must be an absolute http(s) URI.", nameof(progressApiBase));
        }

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
        _options = options ?? SecretBaseJson.CreateOptions();
    }

    public string ProviderId => "personal-systems";

    public string DisplayName => "Progress + Genesis";

    public string SourceKind => ProgressGenesisSourceKinds.PersonalSystems;

    public async Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        ProgressTrack progressTrack;
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
            progressTrack = ProgressLearningMapper.CreateOffline();
        }

        var genesisTrack = await LoadGenesisTrackAsync(cancellationToken).ConfigureAwait(false);
        return GenesisMusicLabMapper.Combine(progressTrack, genesisTrack, DateTimeOffset.UtcNow);
    }

    private async Task<ProgressTrack> LoadProgressTrackAsync(CancellationToken cancellationToken)
    {
        var readiness = await TryLoadReadinessAsync(cancellationToken).ConfigureAwait(false);
        if (readiness is not null)
        {
            return ProgressLearningMapper.FromReadiness(readiness);
        }

        // Legacy Phase-1 endpoints (problems / attempts) — still honest 0% when nothing solved.
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

    private async Task<ProgressReadinessSummary?> TryLoadReadinessAsync(CancellationToken cancellationToken)
    {
        foreach (var path in ReadinessPaths)
        {
            try
            {
                using var response = await _http
                    .GetAsync($"{_progressApiBase}{path}", cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (TryParseReadiness(doc.RootElement, out var summary))
                {
                    return summary;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // try next path
            }
        }

        return null;
    }

    internal static bool TryParseReadiness(JsonElement root, out ProgressReadinessSummary summary)
    {
        summary = new ProgressReadinessSummary();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Unwrap { "data": { ... } } / { "readiness": { ... } }
        var obj = root;
        foreach (var wrap in new[] { "data", "readiness", "dashboard", "progress" })
        {
            if (obj.TryGetProperty(wrap, out var inner) && inner.ValueKind == JsonValueKind.Object)
            {
                obj = inner;
                break;
            }
        }

        var percent = ReadDouble(obj,
            "professionalReadinessPercent",
            "professional_readiness_percent",
            "readinessPercent",
            "readiness_percent",
            "professionalReadiness",
            "readiness");
        var requiredDone = ReadInt(obj,
            "requiredSkillsCompleted",
            "required_skills_completed",
            "requiredCompleted",
            "必須SkillCompleted");
        var requiredTotal = ReadInt(obj,
            "requiredSkillsTotal",
            "required_skills_total",
            "requiredTotal",
            "必須SkillTotal");

        // Also accept "requiredSkills": { "completed": 0, "total": 29 }
        if (obj.TryGetProperty("requiredSkills", out var reqObj) && reqObj.ValueKind == JsonValueKind.Object)
        {
            requiredDone = ReadInt(reqObj, "completed", "done", "current") ?? requiredDone;
            requiredTotal = ReadInt(reqObj, "total", "count", "max") ?? requiredTotal;
        }

        // "必須Skill": "0/29"
        if ((!requiredDone.HasValue || !requiredTotal.HasValue)
            && TryReadString(obj, out var requiredText, "requiredSkillLabel", "requiredSkillsLabel", "必須Skill")
            && requiredText.Contains('/'))
        {
            var parts = requiredText.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && int.TryParse(parts[0], out var done)
                && int.TryParse(parts[1], out var total))
            {
                requiredDone ??= done;
                requiredTotal ??= total;
            }
        }

        var skillMap = ReadSkillMap(obj);
        var hasSignal = percent.HasValue
                        || requiredTotal.HasValue
                        || requiredDone.HasValue
                        || skillMap.Count > 0;
        if (!hasSignal)
        {
            return false;
        }

        // If only required skills are present, derive readiness % from them (matches 0/29 → 0%).
        var readinessPercent = percent
            ?? (requiredTotal is > 0
                ? 100.0 * (requiredDone ?? 0) / requiredTotal.Value
                : 0);

        summary = new ProgressReadinessSummary
        {
            ProfessionalReadinessPercent = readinessPercent,
            RequiredSkillsCompleted = requiredDone ?? 0,
            RequiredSkillsTotal = requiredTotal ?? 0,
            Detail = TryReadString(obj, out var detail, "detail", "note", "message") ? detail : null,
            SkillMap = skillMap
        };
        return true;
    }

    private static List<ProgressSkillMapEntry> ReadSkillMap(JsonElement obj)
    {
        JsonElement mapEl = default;
        var found = false;
        foreach (var name in new[] { "skillMap", "skill_map", "skills", "categories" })
        {
            if (obj.TryGetProperty(name, out mapEl)
                && mapEl.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return [];
        }

        var list = new List<ProgressSkillMapEntry>();
        if (mapEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in mapEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = TryReadString(item, out var n, "name", "title", "id", "label") ? n : null;
                var pct = ReadDouble(item, "percent", "progress", "value", "completion");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                list.Add(new ProgressSkillMapEntry
                {
                    Name = name!,
                    Percent = pct ?? 0
                });
            }
        }
        else if (mapEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in mapEl.EnumerateObject())
            {
                double pct = 0;
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var d))
                {
                    pct = d;
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    pct = ReadDouble(prop.Value, "percent", "progress", "value") ?? 0;
                }

                list.Add(new ProgressSkillMapEntry { Name = prop.Name, Percent = pct });
            }
        }

        return list;
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

        return GenesisMusicLabMapper.FromStatus(new GenesisMusicLabStatus
        {
            Phase = string.IsNullOrWhiteSpace(fileDto?.Phase) ? "MusicLab" : fileDto!.Phase!,
            Status = fileDto?.Status,
            Percent = fileDto?.Percent, // null → 0% (no invented numbers)
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

    private static double? ReadDouble(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
            {
                return d;
            }

            if (el.ValueKind == JsonValueKind.String
                && double.TryParse(el.GetString()?.TrimEnd('%'), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
            {
                return i;
            }

            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryReadString(JsonElement obj, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var el)
                && el.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(el.GetString()))
            {
                value = el.GetString()!.Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
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
