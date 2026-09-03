namespace SecretBase.Core.Calendar;

/// <summary>A remembered recurring local slot (title + time). Applied onto a calendar day on request.</summary>
public sealed class UsualScheduleSlot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public int Hour { get; set; }

    public int Minute { get; set; }

    public int DurationMinutes { get; set; } = 60;

    public bool IsAllDay { get; set; }
}
