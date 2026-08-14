using System.Text.Json;
using System.Text.Json.Serialization;
using SecretBase.Core.Calendar;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Persists the last successful agenda under AppData (events only — never tokens).
/// Implements <see cref="ICalendarAgendaCache"/>.
/// </summary>
public sealed class JsonCalendarAgendaCache : ICalendarAgendaCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public JsonCalendarAgendaCache(string? path = null)
    {
        _filePath = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecretBase",
            "calendar-agenda-cache.json");
    }

    public string FilePath => _filePath;

    public void Save(IReadOnlyList<CalendarEvent> events, DateTimeOffset savedAt)
    {
        ArgumentNullException.ThrowIfNull(events);

        var dto = new CacheFile
        {
            Version = 1,
            SavedAtUtc = savedAt.ToUniversalTime().ToString("O"),
            Events = events.Select(ToDto).ToList(),
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (_gate)
        {
            File.WriteAllText(_filePath, json);
        }
    }

    public IReadOnlyList<CalendarEvent>? TryLoad(out DateTimeOffset? savedAt)
    {
        savedAt = null;
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            string json;
            lock (_gate)
            {
                json = File.ReadAllText(_filePath);
            }

            var dto = JsonSerializer.Deserialize<CacheFile>(json, JsonOptions);
            if (dto?.Events is null)
            {
                return null;
            }

            if (DateTimeOffset.TryParse(
                    dto.SavedAtUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var at))
            {
                savedAt = at;
            }

            return dto.Events.Select(FromDto).Where(static e => e is not null).Cast<CalendarEvent>().ToList();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static EventDto ToDto(CalendarEvent e) => new()
    {
        Id = e.Id,
        Provider = e.Provider,
        CalendarId = e.CalendarId,
        CalendarName = e.CalendarName,
        Title = e.Title,
        Description = e.Description,
        StartUtc = e.Start.ToUniversalTime().ToString("O"),
        EndUtc = e.End.ToUniversalTime().ToString("O"),
        IsAllDay = e.IsAllDay,
        Location = e.Location,
        Url = e.Url,
        Color = e.Color,
        Source = e.Source,
        LastUpdatedUtc = e.LastUpdated?.ToUniversalTime().ToString("O"),
    };

    private static CalendarEvent? FromDto(EventDto? dto)
    {
        if (dto is null
            || string.IsNullOrWhiteSpace(dto.Title)
            || !DateTimeOffset.TryParse(dto.StartUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start)
            || !DateTimeOffset.TryParse(dto.EndUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end)
            || end < start)
        {
            return null;
        }

        DateTimeOffset? lastUpdated = null;
        if (!string.IsNullOrWhiteSpace(dto.LastUpdatedUtc)
            && DateTimeOffset.TryParse(dto.LastUpdatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lu))
        {
            lastUpdated = lu;
        }

        return new CalendarEvent
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id!,
            Provider = string.IsNullOrWhiteSpace(dto.Provider) ? CalendarProviderIds.Local : dto.Provider!,
            CalendarId = dto.CalendarId,
            CalendarName = dto.CalendarName,
            Title = dto.Title!.Trim(),
            Description = dto.Description,
            Start = start,
            End = end,
            IsAllDay = dto.IsAllDay,
            Location = dto.Location,
            Url = dto.Url,
            Color = dto.Color,
            Source = dto.Source,
            LastUpdated = lastUpdated
        };
    }

    private sealed class CacheFile
    {
        public int Version { get; set; }
        public string? SavedAtUtc { get; set; }
        public List<EventDto>? Events { get; set; }
    }

    private sealed class EventDto
    {
        public string? Id { get; set; }
        public string? Provider { get; set; }
        public string? CalendarId { get; set; }
        public string? CalendarName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? StartUtc { get; set; }
        public string? EndUtc { get; set; }
        public bool IsAllDay { get; set; }
        public string? Location { get; set; }
        public string? Url { get; set; }
        public string? Color { get; set; }
        public string? Source { get; set; }
        public string? LastUpdatedUtc { get; set; }
    }
}
