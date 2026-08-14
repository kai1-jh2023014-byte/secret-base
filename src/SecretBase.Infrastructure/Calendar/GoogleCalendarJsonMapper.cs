using System.Globalization;
using System.Text.Json;
using SecretBase.Core.Calendar;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>Maps Google Calendar API JSON items → <see cref="CalendarEvent"/> (no tokens).</summary>
public static class GoogleCalendarJsonMapper
{
    public static bool TryMapEvent(JsonElement item, CalendarInfo calendar, out CalendarEvent ev)
    {
        ev = new CalendarEvent();
        var title = item.TryGetProperty("summary", out var sum) ? sum.GetString() : "(No title)";
        if (!item.TryGetProperty("start", out var startEl) || !TryReadGoogleDate(startEl, out var start, out var allDay))
        {
            return false;
        }

        var end = start.AddHours(1);
        var endAllDay = allDay;
        if (item.TryGetProperty("end", out var endEl))
        {
            _ = TryReadGoogleDate(endEl, out end, out endAllDay);
        }

        if (end < start)
        {
            return false;
        }

        ev = new CalendarEvent
        {
            Id = item.TryGetProperty("id", out var id)
                ? id.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N"),
            Provider = CalendarProviderIds.Google,
            CalendarId = calendar.Id,
            CalendarName = calendar.Name,
            Title = string.IsNullOrWhiteSpace(title) ? "(No title)" : title!,
            Start = start,
            End = end,
            IsAllDay = allDay || endAllDay,
            Location = item.TryGetProperty("location", out var loc) ? loc.GetString() : null,
            Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            Url = item.TryGetProperty("htmlLink", out var link) ? link.GetString() : null,
            Color = calendar.Color ?? "#4285F4",
            Source = $"Google Calendar · {calendar.Name}",
            LastUpdated = DateTimeOffset.UtcNow
        };
        return true;
    }

    public static bool TryReadGoogleDate(JsonElement el, out DateTimeOffset value, out bool allDay)
    {
        value = default;
        allDay = false;
        if (el.TryGetProperty("dateTime", out var dt)
            && dt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                dt.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value))
        {
            return true;
        }

        if (el.TryGetProperty("date", out var d)
            && d.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(d.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            allDay = true;
            value = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        return false;
    }
}
