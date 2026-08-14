using System.Globalization;
using System.Text;

namespace SecretBase.Core.Calendar;

/// <summary>
/// Minimal iCalendar (ICS) parser for VEVENT → <see cref="CalendarEvent"/>.
/// Pure string parsing — no network. Used by Infrastructure Google ICS provider.
/// </summary>
public static class IcsCalendarParser
{
    public static IReadOnlyList<CalendarEvent> Parse(
        string icsText,
        string providerId,
        string? calendarName = null)
    {
        if (string.IsNullOrWhiteSpace(icsText))
        {
            return Array.Empty<CalendarEvent>();
        }

        var unfolded = Unfold(icsText);
        var events = new List<CalendarEvent>();
        string? currentUid = null;
        string? summary = null;
        string? description = null;
        string? location = null;
        string? url = null;
        string? dtStart = null;
        string? dtEnd = null;
        var inEvent = false;

        foreach (var rawLine in unfolded.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                inEvent = true;
                currentUid = summary = description = location = url = dtStart = dtEnd = null;
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (inEvent && TryBuildEvent(
                        providerId,
                        calendarName,
                        currentUid,
                        summary,
                        description,
                        location,
                        url,
                        dtStart,
                        dtEnd,
                        out var ev))
                {
                    events.Add(ev);
                }

                inEvent = false;
                continue;
            }

            if (!inEvent)
            {
                continue;
            }

            var idx = line.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            var keyPart = line[..idx];
            var value = line[(idx + 1)..].Trim();
            var key = keyPart.Split(';')[0].ToUpperInvariant();

            switch (key)
            {
                case "UID":
                    currentUid = value;
                    break;
                case "SUMMARY":
                    summary = Unescape(value);
                    break;
                case "DESCRIPTION":
                    description = Unescape(value);
                    break;
                case "LOCATION":
                    location = Unescape(value);
                    break;
                case "URL":
                    url = value;
                    break;
                case "DTSTART":
                    dtStart = keyPart + ":" + value;
                    break;
                case "DTEND":
                    dtEnd = keyPart + ":" + value;
                    break;
            }
        }

        return events;
    }

    private static bool TryBuildEvent(
        string providerId,
        string? calendarName,
        string? uid,
        string? summary,
        string? description,
        string? location,
        string? url,
        string? dtStartRaw,
        string? dtEndRaw,
        out CalendarEvent calendarEvent)
    {
        calendarEvent = new CalendarEvent();
        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(dtStartRaw))
        {
            return false;
        }

        if (!TryParseDateProperty(dtStartRaw, out var start, out var startAllDay))
        {
            return false;
        }

        DateTimeOffset end;
        var endAllDay = startAllDay;
        if (string.IsNullOrWhiteSpace(dtEndRaw) || !TryParseDateProperty(dtEndRaw, out end, out endAllDay))
        {
            end = startAllDay ? start.AddDays(1) : start.AddHours(1);
        }

        calendarEvent = new CalendarEvent
        {
            Id = string.IsNullOrWhiteSpace(uid) ? Guid.NewGuid().ToString("N") : uid!,
            Provider = providerId,
            CalendarName = calendarName,
            Title = summary!.Trim(),
            Start = start,
            End = end,
            IsAllDay = startAllDay || endAllDay,
            Location = location,
            Description = description,
            Url = url
        };
        return true;
    }

    private static bool TryParseDateProperty(string raw, out DateTimeOffset value, out bool isAllDay)
    {
        value = default;
        isAllDay = false;
        var idx = raw.IndexOf(':');
        if (idx <= 0)
        {
            return false;
        }

        var keyPart = raw[..idx];
        var data = raw[(idx + 1)..].Trim();
        isAllDay = keyPart.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase)
                   || (data.Length == 8 && data.All(char.IsDigit));

        if (isAllDay && DateOnly.TryParseExact(data[..Math.Min(8, data.Length)], "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            value = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        // Floating / UTC forms: 20260813T090000Z or 20260813T090000
        var styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTimeOffset.TryParseExact(
                data,
                ["yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmmsszzz"],
                CultureInfo.InvariantCulture,
                styles,
                out value))
        {
            return true;
        }

        return DateTimeOffset.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
    }

    private static string Unfold(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                sb.Append(line.TrimStart());
            }
            else
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(line);
            }
        }

        return sb.ToString();
    }

    private static string Unescape(string value) =>
        value.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\,", ",", StringComparison.Ordinal)
            .Replace("\\;", ";", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
}
