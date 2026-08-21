using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Aggregates read-only Secret Base state via existing Commands/Services.
/// Does not Process.Start, touch the filesystem, or read secret values.
/// </summary>
public sealed class AssistantContextService : IAssistantContextService
{
    private const int MaxNotesPreview = 160;
    private const int MaxRecentProjects = 5;

    private readonly CalendarCommandService? _calendar;
    private readonly CreativeCommandService? _creative;
    private readonly AppCommandService? _apps;
    private readonly MusicCommandService? _music;
    private readonly Func<AssistantSettings> _settings;
    private readonly Func<bool> _isOpenAiKeyConfigured;

    public AssistantContextService(
        CalendarCommandService? calendar = null,
        CreativeCommandService? creative = null,
        AppCommandService? apps = null,
        MusicCommandService? music = null,
        Func<AssistantSettings>? settings = null,
        Func<bool>? isOpenAiKeyConfigured = null)
    {
        _calendar = calendar;
        _creative = creative;
        _apps = apps;
        _music = music;
        _settings = settings ?? (() => new AssistantSettings());
        _isOpenAiKeyConfigured = isOpenAiKeyConfigured ?? (() => false);
    }

    public async Task<AssistantContextSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = Array.Empty<CalendarEvent>();
        var upcoming = Array.Empty<CalendarEvent>();
        if (_calendar is not null)
        {
            var todayResult = await _calendar.ExecuteAsync(CalendarCommand.GetTodayEvents(), cancellationToken)
                .ConfigureAwait(false);
            if (todayResult.Succeeded)
            {
                today = todayResult.Events.ToArray();
            }

            var upcomingResult = await _calendar.ExecuteAsync(CalendarCommand.GetUpcoming(7), cancellationToken)
                .ConfigureAwait(false);
            if (upcomingResult.Succeeded)
            {
                upcoming = upcomingResult.Events.ToArray();
            }
        }

        var projects = Array.Empty<AssistantProjectSummary>();
        if (_creative is not null)
        {
            var listed = _creative.Execute(CreativeCommand.SearchProjects(null));
            if (listed.Succeeded)
            {
                projects = listed.Projects.Select(ToProjectSummary).ToArray();
            }
        }

        var recent = projects
            .Where(p => p.LastOpened is not null)
            .OrderByDescending(p => p.LastOpened)
            .Take(MaxRecentProjects)
            .ToArray();

        var apps = Array.Empty<AssistantAppSummary>();
        if (_apps is not null)
        {
            var listed = _apps.Execute(AppCommand.ListApps());
            if (listed.Succeeded)
            {
                apps = listed.Apps.Select(a => new AssistantAppSummary
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    Type = a.Type.ToString()
                }).ToArray();
            }
        }

        var integrations = IntegrationCatalog.Commands
            .Select(c => c.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssistantContextSnapshot
        {
            CapturedAt = DateTimeOffset.Now,
            TodayEvents = today,
            UpcomingEvents = upcoming,
            Projects = projects,
            RecentProjects = recent,
            Apps = apps,
            Music = GetMusicState(),
            Integrations = integrations,
            Provider = GetProviderStatus()
        };
    }

    public Task<AssistantProjectSummary?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_creative is null || string.IsNullOrWhiteSpace(projectId))
        {
            return Task.FromResult<AssistantProjectSummary?>(null);
        }

        var result = _creative.Execute(CreativeCommand.GetCreativeProject(projectId.Trim()));
        if (!result.Succeeded || result.Project is null)
        {
            return Task.FromResult<AssistantProjectSummary?>(null);
        }

        return Task.FromResult<AssistantProjectSummary?>(ToProjectSummary(result.Project));
    }

    public AssistantMusicState GetMusicState()
    {
        if (_music is null)
        {
            return new AssistantMusicState
            {
                SearchAvailable = false,
                PlaybackAvailable = false,
                UsesDemoCatalog = false,
                Note = "Music is not wired."
            };
        }

        var caps = _music.MusicService.AggregateCapabilities();
        var providers = _music.MusicService.Providers.ToList();
        var demoOnlySearch = providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .All(p => string.Equals(p.DisplayName, "Demo catalog", StringComparison.Ordinal));
        var playback = _music.MusicService.GetPlaybackProvider();
        return new AssistantMusicState
        {
            SearchAvailable = caps.HasFlag(MusicProviderCapabilities.Search),
            PlaybackAvailable = caps.HasFlag(MusicProviderCapabilities.Playback),
            UsesDemoCatalog = demoOnlySearch,
            CurrentTrackTitle = playback?.CurrentTrack?.Title,
            CurrentTrackArtist = playback?.CurrentTrack?.Artist,
            IsPlaying = playback?.IsPlaying ?? false,
            Note = demoOnlySearch
                ? "Search/playback use the Secret Base demo catalog only — not Spotify/YouTube API."
                : "Music uses Secret Base providers only. Do not invent Spotify/YouTube API playback."
        };
    }

    public AssistantProviderStatusInfo GetProviderStatus()
    {
        var settings = _settings();
        var configured = string.Equals(settings.ProviderId, AssistantProviderIds.OpenAi, StringComparison.OrdinalIgnoreCase)
                         && _isOpenAiKeyConfigured();

        var label = configured
            ? "Configured"
            : string.Equals(settings.ProviderId, AssistantProviderIds.OpenAi, StringComparison.OrdinalIgnoreCase)
                ? "Not configured"
                : "Provider stub / unavailable in MVP";

        return new AssistantProviderStatusInfo
        {
            ProviderId = settings.ProviderId,
            Model = settings.Model,
            IsConfigured = configured,
            StatusLabel = label
        };
    }

    private AssistantProjectSummary ToProjectSummary(CreativeProject project)
    {
        IReadOnlyList<string> quick;
        if (_creative?.Projects is not null)
        {
            quick = _creative.Projects.GetQuickActions(project.Id)
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(8)
                .ToArray();
        }
        else
        {
            quick = project.Resources
                .Where(r => r.IsQuickAction)
                .Select(r => r.Name)
                .Take(8)
                .ToArray();
        }

        return new AssistantProjectSummary
        {
            Id = project.Id,
            Name = project.Name,
            Description = Truncate(project.Description, MaxNotesPreview),
            NotesPreview = Truncate(project.Notes, MaxNotesPreview),
            ProjectType = project.ProjectType.ToString(),
            IsFavorite = project.IsFavorite,
            LastOpened = project.LastOpened,
            QuickActionNames = quick,
            HasRootFolder = !string.IsNullOrWhiteSpace(project.RootFolder)
        };
    }

    private static string? Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    /// <summary>Formats a snapshot for the LLM. Never includes secrets or absolute paths.</summary>
    public static string FormatForModel(AssistantContextSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"Captured: {snapshot.CapturedAt:yyyy-MM-dd HH:mm}",
            $"Provider: {snapshot.Provider.ProviderId} / {snapshot.Provider.Model} ({snapshot.Provider.StatusLabel})",
            "",
            $"Today events ({snapshot.TodayEvents.Count}):"
        };
        if (snapshot.TodayEvents.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var e in snapshot.TodayEvents.Take(12))
            {
                var when = e.IsAllDay ? e.Start.ToString("yyyy-MM-dd") : e.Start.ToString("HH:mm");
                lines.Add($"- {when} {e.Title}");
            }
        }

        lines.Add("");
        lines.Add($"Upcoming events ({snapshot.UpcomingEvents.Count}):");
        if (snapshot.UpcomingEvents.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var e in snapshot.UpcomingEvents.Take(12))
            {
                lines.Add($"- {e.Start:yyyy-MM-dd HH:mm} {e.Title}");
            }
        }

        lines.Add("");
        lines.Add($"Projects ({snapshot.Projects.Count}):");
        if (snapshot.Projects.Count == 0)
        {
            lines.Add("- (none registered)");
        }
        else
        {
            foreach (var p in snapshot.Projects.Take(20))
            {
                var fav = p.IsFavorite ? " ★" : string.Empty;
                lines.Add($"- {p.Id}: {p.Name}{fav} ({p.ProjectType})");
                if (!string.IsNullOrWhiteSpace(p.Description))
                {
                    lines.Add($"  desc: {p.Description}");
                }

                if (!string.IsNullOrWhiteSpace(p.NotesPreview))
                {
                    lines.Add($"  notes: {p.NotesPreview}");
                }

                if (p.QuickActionNames.Count > 0)
                {
                    lines.Add($"  quick: {string.Join(", ", p.QuickActionNames)}");
                }
            }
        }

        lines.Add("");
        lines.Add($"Recent projects ({snapshot.RecentProjects.Count}):");
        if (snapshot.RecentProjects.Count == 0)
        {
            lines.Add("- (none opened yet)");
        }
        else
        {
            foreach (var p in snapshot.RecentProjects)
            {
                lines.Add($"- {p.Id}: {p.Name} (last {p.LastOpened:yyyy-MM-dd})");
            }
        }

        lines.Add("");
        lines.Add($"Apps ({snapshot.Apps.Count}):");
        if (snapshot.Apps.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var a in snapshot.Apps.Take(20))
            {
                lines.Add($"- {a.Id}: {a.Name} ({a.Type})");
            }
        }

        lines.Add("");
        lines.Add("Music:");
        lines.Add($"- search={snapshot.Music.SearchAvailable}, playback={snapshot.Music.PlaybackAvailable}, demoCatalog={snapshot.Music.UsesDemoCatalog}");
        if (!string.IsNullOrWhiteSpace(snapshot.Music.CurrentTrackTitle))
        {
            lines.Add($"- now: {snapshot.Music.CurrentTrackTitle} ({snapshot.Music.CurrentTrackArtist}) playing={snapshot.Music.IsPlaying}");
        }

        lines.Add($"- note: {snapshot.Music.Note}");
        lines.Add("");
        lines.Add("Integrations: " + (snapshot.Integrations.Count == 0
            ? "(none)"
            : string.Join(", ", snapshot.Integrations)));
        return string.Join('\n', lines);
    }

    public static string FormatProjectForModel(AssistantProjectSummary project)
    {
        var lines = new List<string>
        {
            $"id: {project.Id}",
            $"name: {project.Name}",
            $"type: {project.ProjectType}",
            $"favorite: {project.IsFavorite}",
            $"hasRootFolder: {project.HasRootFolder}"
        };
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            lines.Add($"description: {project.Description}");
        }

        if (!string.IsNullOrWhiteSpace(project.NotesPreview))
        {
            lines.Add($"notes: {project.NotesPreview}");
        }

        if (project.LastOpened is not null)
        {
            lines.Add($"lastOpened: {project.LastOpened:yyyy-MM-dd HH:mm}");
        }

        if (project.QuickActionNames.Count > 0)
        {
            lines.Add("quickActions: " + string.Join(", ", project.QuickActionNames));
        }

        return string.Join('\n', lines);
    }
}
