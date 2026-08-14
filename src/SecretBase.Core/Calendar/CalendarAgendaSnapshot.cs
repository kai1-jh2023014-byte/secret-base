namespace SecretBase.Core.Calendar;

/// <summary>Per-provider outcome for agenda merge (widget-local errors).</summary>
public sealed class CalendarProviderResult
{
    public required string ProviderId { get; init; }

    public required string DisplayName { get; init; }

    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public CalendarAuthStatus AuthStatus { get; init; }

    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();
}

/// <summary>Merged agenda plus per-provider status.</summary>
public sealed class CalendarAgendaSnapshot
{
    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<CalendarProviderResult> ProviderResults { get; init; } = Array.Empty<CalendarProviderResult>();
}
