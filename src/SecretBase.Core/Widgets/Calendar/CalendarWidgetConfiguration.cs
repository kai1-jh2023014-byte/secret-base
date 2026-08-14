using System.Globalization;
using System.Text.Json;
using SecretBase.Core.Calendar;

namespace SecretBase.Core.Widgets.Calendar;

/// <summary>
/// Calendar Widget settings. Visual tokens come from <c>ThemeDefinition</c>.
/// Local timed events + optional Google ICS / OAuth API flags (no tokens here).
/// </summary>
public sealed class CalendarWidgetConfiguration
{
    public const string DefaultOpenCalendarUrl = "https://calendar.google.com/";

    /// <summary>Optional Google Calendar "secret address in iCal format" HTTPS URL.</summary>
    public string? GoogleIcsUrl { get; set; }

    public string OpenCalendarUrl { get; set; } = DefaultOpenCalendarUrl;

    /// <summary>When true and no events exist, seed Creative OS sample agenda for today.</summary>
    public bool UseSampleAgendaWhenEmpty { get; set; } = true;

    /// <summary>Include deterministic Mock provider (UI demos / tests).</summary>
    public bool IncludeMockProvider { get; set; }

    /// <summary>
    /// When true, wire Google Calendar API if OAuth client JSON exists under AppData
    /// and the host supplies <c>ISecureSecretStore</c> + browser opener.
    /// </summary>
    public bool EnableGoogleApiProvider { get; set; } = true;

    /// <summary>Optional override for OAuth client JSON path (default: AppData credentials file).</summary>
    public string? GoogleOAuthClientConfigPath { get; set; }

    public List<CalendarEvent> Events { get; set; } = [];

    public static CalendarWidgetConfiguration CreateDefault() => new();

    public static CalendarWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();

        if (configuration.TryGetValue(nameof(GoogleIcsUrl), out var ics) &&
            ics.ValueKind == JsonValueKind.String)
        {
            var url = ics.GetString();
            result.GoogleIcsUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }

        if (configuration.TryGetValue(nameof(OpenCalendarUrl), out var open) &&
            open.ValueKind == JsonValueKind.String)
        {
            var url = open.GetString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                result.OpenCalendarUrl = url.Trim();
            }
        }

        if (configuration.TryGetValue(nameof(UseSampleAgendaWhenEmpty), out var sample) &&
            (sample.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.UseSampleAgendaWhenEmpty = sample.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(IncludeMockProvider), out var mock) &&
            (mock.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.IncludeMockProvider = mock.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(EnableGoogleApiProvider), out var googleApi) &&
            (googleApi.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.EnableGoogleApiProvider = googleApi.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(GoogleOAuthClientConfigPath), out var oauthPath) &&
            oauthPath.ValueKind == JsonValueKind.String)
        {
            var path = oauthPath.GetString();
            result.GoogleOAuthClientConfigPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
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
            [nameof(GoogleIcsUrl)] = JsonSerializer.SerializeToElement(GoogleIcsUrl),
            [nameof(OpenCalendarUrl)] = JsonSerializer.SerializeToElement(OpenCalendarUrl),
            [nameof(UseSampleAgendaWhenEmpty)] = JsonSerializer.SerializeToElement(UseSampleAgendaWhenEmpty),
            [nameof(IncludeMockProvider)] = JsonSerializer.SerializeToElement(IncludeMockProvider),
            [nameof(EnableGoogleApiProvider)] = JsonSerializer.SerializeToElement(EnableGoogleApiProvider),
            [nameof(GoogleOAuthClientConfigPath)] = JsonSerializer.SerializeToElement(GoogleOAuthClientConfigPath),
            [nameof(Events)] = JsonSerializer.SerializeToElement(
                Events.Select(SerializeEvent).ToList())
        };
    }

    private static Dictionary<string, JsonElement> SerializeEvent(CalendarEvent e) =>
        new(StringComparer.Ordinal)
        {
            ["Id"] = JsonSerializer.SerializeToElement(e.Id),
            ["Provider"] = JsonSerializer.SerializeToElement(e.Provider),
            ["CalendarId"] = JsonSerializer.SerializeToElement(e.CalendarId),
            ["CalendarName"] = JsonSerializer.SerializeToElement(e.CalendarName),
            ["Title"] = JsonSerializer.SerializeToElement(e.Title ?? string.Empty),
            ["Start"] = JsonSerializer.SerializeToElement(e.Start.ToString("o", CultureInfo.InvariantCulture)),
            ["End"] = JsonSerializer.SerializeToElement(e.End.ToString("o", CultureInfo.InvariantCulture)),
            ["IsAllDay"] = JsonSerializer.SerializeToElement(e.IsAllDay),
            ["Location"] = JsonSerializer.SerializeToElement(e.Location),
            ["Description"] = JsonSerializer.SerializeToElement(e.Description),
            ["Url"] = JsonSerializer.SerializeToElement(e.Url),
            ["Color"] = JsonSerializer.SerializeToElement(e.Color),
            ["Source"] = JsonSerializer.SerializeToElement(e.Source)
        };

    private static bool TryReadEvent(JsonElement item, out CalendarEvent calendarEvent)
    {
        calendarEvent = new CalendarEvent();
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var title = ReadString(item, "Title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        // New timed shape
        if (TryReadDateTimeOffset(item, "Start", out var start))
        {
            if (!TryReadDateTimeOffset(item, "End", out var end))
            {
                end = start.AddHours(1);
            }

            calendarEvent = new CalendarEvent
            {
                Id = ReadString(item, "Id") ?? Guid.NewGuid().ToString("N"),
                Provider = ReadString(item, "Provider") ?? CalendarProviderIds.Local,
                CalendarId = ReadString(item, "CalendarId"),
                CalendarName = ReadString(item, "CalendarName"),
                Title = title.Trim(),
                Start = start,
                End = end,
                IsAllDay = ReadBool(item, "IsAllDay"),
                Location = ReadString(item, "Location"),
                Description = ReadString(item, "Description") ?? ReadString(item, "Notes"),
                Url = ReadString(item, "Url"),
                Color = ReadString(item, "Color"),
                Source = ReadString(item, "Source")
            };
            return true;
        }

        // Legacy Date-only (month-grid era) → all-day
        if (item.TryGetProperty("Date", out var dateElement) &&
            dateElement.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var startDay = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            calendarEvent = new CalendarEvent
            {
                Id = ReadString(item, "Id") ?? Guid.NewGuid().ToString("N"),
                Provider = CalendarProviderIds.Local,
                CalendarName = "Local",
                Title = title.Trim(),
                Start = startDay,
                End = startDay.AddDays(1),
                IsAllDay = true,
                Description = ReadString(item, "Notes"),
                Color = ReadString(item, "Color"),
                Source = ReadString(item, "Source") ?? "Local"
            };
            return true;
        }

        return false;
    }

    private static string? ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return el.GetString();
    }

    private static bool ReadBool(JsonElement item, string name) =>
        item.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False && el.GetBoolean();

    private static bool TryReadDateTimeOffset(JsonElement item, string name, out DateTimeOffset value)
    {
        value = default;
        if (!item.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTimeOffset.TryParse(el.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
    }
}
