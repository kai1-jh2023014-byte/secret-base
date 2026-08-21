using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Music;

namespace SecretBase.Core.Assistant;

/// <summary>Read-only Secret Base snapshot for the assistant. Never contains API keys or OAuth tokens.</summary>
public sealed class AssistantContextSnapshot
{
    public DateTimeOffset CapturedAt { get; init; }

    public IReadOnlyList<CalendarEvent> TodayEvents { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<CalendarEvent> UpcomingEvents { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<AssistantProjectSummary> Projects { get; init; } = Array.Empty<AssistantProjectSummary>();

    public IReadOnlyList<AssistantProjectSummary> RecentProjects { get; init; } = Array.Empty<AssistantProjectSummary>();

    public IReadOnlyList<AssistantAppSummary> Apps { get; init; } = Array.Empty<AssistantAppSummary>();

    public AssistantMusicState Music { get; init; } = new();

    public IReadOnlyList<string> Integrations { get; init; } = Array.Empty<string>();

    public AssistantProviderStatusInfo Provider { get; init; } = new();
}

public sealed class AssistantProjectSummary
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? NotesPreview { get; init; }

    public string ProjectType { get; init; } = "Other";

    public bool IsFavorite { get; init; }

    public DateTimeOffset? LastOpened { get; init; }

    public IReadOnlyList<string> QuickActionNames { get; init; } = Array.Empty<string>();

    public bool HasRootFolder { get; init; }
}

public sealed class AssistantAppSummary
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string Type { get; init; } = "Application";
}

public sealed class AssistantMusicState
{
    public bool SearchAvailable { get; init; }

    public bool PlaybackAvailable { get; init; }

    public bool UsesDemoCatalog { get; init; }

    public string? CurrentTrackTitle { get; init; }

    public string? CurrentTrackArtist { get; init; }

    public bool IsPlaying { get; init; }

    public string Note { get; init; } =
        "Music uses Secret Base providers only. Demo catalog is not Spotify/YouTube API playback.";
}

public sealed class AssistantProviderStatusInfo
{
    public string ProviderId { get; init; } = AssistantProviderIds.OpenAi;

    public string Model { get; init; } = AssistantSettings.DefaultOpenAiModel;

    public bool IsConfigured { get; init; }

    public string StatusLabel { get; init; } = "Not configured";
}

/// <summary>Read-only context over existing Commands/Services. Never launches OS.</summary>
public interface IAssistantContextService
{
    Task<AssistantContextSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<AssistantProjectSummary?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default);

    AssistantMusicState GetMusicState();

    AssistantProviderStatusInfo GetProviderStatus();
}
