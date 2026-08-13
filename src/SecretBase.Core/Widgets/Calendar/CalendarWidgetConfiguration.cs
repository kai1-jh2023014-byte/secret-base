using System.Globalization;
using System.Text.Json;
using SecretBase.Core.Calendar;

namespace SecretBase.Core.Widgets.Calendar;

/// <summary>
/// Calendar Widget settings. Visual tokens come from <c>ThemeDefinition</c>.
/// Optional local events persist in the layout JSON (no cloud sync in v0.1).
/// </summary>
public sealed class CalendarWidgetConfiguration
{
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>When true, the widget opens on today's month; otherwise uses <see cref="PinnedYear"/>/<see cref="PinnedMonth"/>.</summary>
    public bool FollowToday { get; set; } = true;

    public int PinnedYear { get; set; }

    public int PinnedMonth { get; set; } = 1;

    public List<CalendarEvent> Events { get; set; } = [];

    public static CalendarWidgetConfiguration CreateDefault() => new();

    public static CalendarWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();

        if (configuration.TryGetValue(nameof(FirstDayOfWeek), out var firstDay))
        {
            if (firstDay.ValueKind == JsonValueKind.String &&
                Enum.TryParse<DayOfWeek>(firstDay.GetString(), ignoreCase: true, out var parsed))
            {
                result.FirstDayOfWeek = parsed;
            }
            else if (firstDay.ValueKind == JsonValueKind.Number &&
                     Enum.IsDefined(typeof(DayOfWeek), firstDay.GetInt32()))
            {
                result.FirstDayOfWeek = (DayOfWeek)firstDay.GetInt32();
            }
        }

        if (configuration.TryGetValue(nameof(FollowToday), out var follow) &&
            (follow.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.FollowToday = follow.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(PinnedYear), out var year) &&
            year.ValueKind == JsonValueKind.Number)
        {
            result.PinnedYear = year.GetInt32();
        }

        if (configuration.TryGetValue(nameof(PinnedMonth), out var month) &&
            month.ValueKind == JsonValueKind.Number)
        {
            var m = month.GetInt32();
            if (m is >= 1 and <= 12)
            {
                result.PinnedMonth = m;
            }
        }

        if (configuration.TryGetValue(nameof(Events), out var events) &&
            events.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in events.EnumerateArray())
            {
                if (TryReadEvent(item, out var calendarEvent))
                {
                    result.Events.Add(calendarEvent);
                }
            }
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(FirstDayOfWeek)] = JsonSerializer.SerializeToElement(FirstDayOfWeek.ToString()),
            [nameof(FollowToday)] = JsonSerializer.SerializeToElement(FollowToday),
            [nameof(PinnedYear)] = JsonSerializer.SerializeToElement(PinnedYear),
            [nameof(PinnedMonth)] = JsonSerializer.SerializeToElement(PinnedMonth),
            [nameof(Events)] = JsonSerializer.SerializeToElement(
                Events.Select(e => new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["Id"] = JsonSerializer.SerializeToElement(e.Id),
                    ["Date"] = JsonSerializer.SerializeToElement(e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    ["Title"] = JsonSerializer.SerializeToElement(e.Title ?? string.Empty),
                    ["Notes"] = JsonSerializer.SerializeToElement(e.Notes)
                }).ToList())
        };
    }

    public ICalendarEventSource CreateEventSource() =>
        Events.Count == 0
            ? EmptyCalendarEventSource.Instance
            : new LocalCalendarEventSource(Events);

    private static bool TryReadEvent(JsonElement item, out CalendarEvent calendarEvent)
    {
        calendarEvent = new CalendarEvent();
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!item.TryGetProperty("Date", out var dateElement) ||
            dateElement.ValueKind != JsonValueKind.String ||
            !DateOnly.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        var title = string.Empty;
        if (item.TryGetProperty("Title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
        {
            title = titleElement.GetString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var id = Guid.NewGuid();
        if (item.TryGetProperty("Id", out var idElement) &&
            idElement.ValueKind == JsonValueKind.String &&
            Guid.TryParse(idElement.GetString(), out var parsedId))
        {
            id = parsedId;
        }

        string? notes = null;
        if (item.TryGetProperty("Notes", out var notesElement) && notesElement.ValueKind == JsonValueKind.String)
        {
            notes = notesElement.GetString();
        }

        calendarEvent = new CalendarEvent
        {
            Id = id,
            Date = date,
            Title = title.Trim(),
            Notes = notes
        };
        return true;
    }
}
