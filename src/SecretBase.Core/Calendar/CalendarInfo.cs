namespace SecretBase.Core.Calendar;

/// <summary>Provider-agnostic calendar list entry.</summary>
public sealed class CalendarInfo
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string? Color { get; set; }

    public bool IsPrimary { get; set; }
}
