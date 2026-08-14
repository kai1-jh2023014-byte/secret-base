namespace SecretBase.Core.Calendar;

/// <summary>What a provider can do. Not every provider supports CRUD.</summary>
[Flags]
public enum CalendarProviderCapabilities
{
    None = 0,
    ReadEvents = 1 << 0,
    ListCalendars = 1 << 1,
    Authentication = 1 << 2,
    CreateEvents = 1 << 3,
    UpdateEvents = 1 << 4,
    DeleteEvents = 1 << 5,
    MultipleCalendars = 1 << 6
}
