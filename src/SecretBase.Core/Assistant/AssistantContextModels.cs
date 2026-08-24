using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Music;

namespace SecretBase.Core.Assistant;

/// <summary>Read-only Secret Base snapshot for the assistant. Never contains API keys or OAuth tokens.</summary>
public sealed class AssistantContextSnapshot
{
    public DateTimeOffset CapturedAt { get; init; }

    public AssistantContextScope Scope { get; init; } = AssistantContextScope.All;

    public IReadOnlyList<AssistantContextSectionStatus> Sections { get; init; } = Array.Empty<AssistantContextSectionStatus>();

    public IReadOnlyList<CalendarEvent> TodayEvents { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<CalendarEvent> UpcomingEvents { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<AssistantFreeTimeSlot> FreeTimeSlots { get; init; } = Array.Empty<AssistantFreeTimeSlot>();

    public IReadOnlyList<AssistantProjectSummary> Projects { get; init; } = Array.Empty<AssistantProjectSummary>();

    public IReadOnlyList<AssistantProjectSummary> RecentProjects { get; init; } = Array.Empty<AssistantProjectSummary>();

    public IReadOnlyList<AssistantAppSummary> Apps { get; init; } = Array.Empty<AssistantAppSummary>();

    public AssistantMusicState Music { get; init; } = new();

    public IReadOnlyList<string> Integrations { get; init; } = Array.Empty<string>();

    public AssistantProviderStatusInfo Provider { get; init; } = new();
}

public sealed class AssistantFreeTimeSlot
{
    public DateTimeOffset Start { get; init; }

    public DateTimeOffset End { get; init; }

    public override string ToString() =>
        $"{Start:HH:mm}–{End:HH:mm}";
}

public sealed class AssistantContextSectionStatus
{
    public required string Name { get; init; }

    public bool IsAvailable { get; init; }

    public string? Message { get; init; }
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

    /// <summary>Whether a project folder is registered. Absolute path is never exposed to the LLM.</summary>
    public bool HasProjectRoot { get; init; }
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

    public string DisplayName { get; init; } = "OpenAI";

    public string Model { get; init; } = AssistantSettings.DefaultOpenAiModel;

    public bool IsConfigured { get; init; }

    public bool IsAvailable { get; init; } = true;

    public string StatusLabel { get; init; } = "Not configured";

    public int MaxSteps { get; init; } = AssistantSettings.DefaultMaxSteps;

    public bool RequireConfirmationForActions { get; init; } = true;

    public bool HasApiKey { get; init; }

    /// <summary>Masked key status for Settings UI. Never the raw secret.</summary>
    public string ApiKeyDisplay => HasApiKey ? "••••••••" : "(not set)";
}

/// <summary>Read-only context over existing Commands/Services. Never launches OS.</summary>
public interface IAssistantContextService
{
    Task<AssistantContextSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<AssistantContextSnapshot> GetSnapshotAsync(
        AssistantContextScope scope,
        CancellationToken cancellationToken = default);

    Task<AssistantProjectSummary?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default);

    AssistantMusicState GetMusicState();

    AssistantProviderStatusInfo GetProviderStatus();
}
