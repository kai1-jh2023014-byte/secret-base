using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Parses one-day schedule phrases into local calendar_add_event payloads
/// without calling a remote model (avoids timeout for 「19時…」「22:00から…」).
/// </summary>
public static partial class LocalScheduleParser
{
    /// <summary>Write intent without requiring the word 予定.</summary>
    public static bool LooksLikeScheduleWrite(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Contains("いつも", StringComparison.Ordinal)
            || text.Contains("usual", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var wantsWrite = text.Contains("入れて", StringComparison.Ordinal)
                         || text.Contains("いれて", StringComparison.Ordinal)
                         || text.Contains("追加", StringComparison.Ordinal)
                         || text.Contains("反映", StringComparison.Ordinal)
                         || text.Contains("記録", StringComparison.Ordinal)
                         || text.Contains("add", StringComparison.OrdinalIgnoreCase)
                         || text.Contains("record", StringComparison.OrdinalIgnoreCase);
        if (!wantsWrite)
        {
            return false;
        }

        return TryParse(text, out var events) && events.Count > 0;
    }

    public static bool TryParse(string? text, out IReadOnlyList<LocalScheduleEvent> events)
    {
        events = Array.Empty<LocalScheduleEvent>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = Normalize(text);
        var dayOffset = DetectDayOffset(cleaned);
        var drafts = new List<(int Hour, int Minute, int? Duration, string Title)>();

        // Prefer explicit ranges: 22:00から23:00まで英語 / 22時から23時まで英語
        foreach (Match match in ColonRangePattern().Matches(cleaned))
        {
            if (!TryReadClock(match.Groups[1].Value, match.Groups[2].Value, out var startH, out var startM))
            {
                continue;
            }

            if (!TryReadEndClock(match.Groups[3].Value, match.Groups[4].Value, startH, startM, out var duration))
            {
                continue;
            }

            var title = CleanTitle(match.Groups[5].Value);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            drafts.Add((startH, startM, duration, title));
        }

        if (drafts.Count == 0)
        {
            foreach (Match match in JapaneseRangePattern().Matches(cleaned))
            {
                if (!TryReadJapaneseStart(match, out var startH, out var startM))
                {
                    continue;
                }

                if (!TryReadJapaneseEnd(match, startH, startM, out var duration))
                {
                    continue;
                }

                var title = CleanTitle(match.Groups[7].Value);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                drafts.Add((startH, startM, duration, title));
            }
        }

        if (drafts.Count == 0)
        {
            foreach (Match match in JapaneseSlotPattern().Matches(cleaned))
            {
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
                    || hour is < 0 or > 23)
                {
                    continue;
                }

                var minute = 0;
                if (match.Groups[2].Success && match.Groups[2].Value == "半")
                {
                    minute = 30;
                }
                else if (match.Groups[3].Success
                         && int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
                {
                    minute = Math.Clamp(m, 0, 59);
                }

                var title = CleanTitle(match.Groups[4].Value);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                drafts.Add((hour, minute, null, title));
            }
        }

        if (drafts.Count == 0)
        {
            foreach (Match match in ColonSlotPattern().Matches(cleaned))
            {
                if (!TryReadClock(match.Groups[1].Value, match.Groups[2].Value, out var hour, out var minute))
                {
                    continue;
                }

                var title = CleanTitle(match.Groups[3].Value);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                drafts.Add((hour, minute, null, title));
            }
        }

        if (drafts.Count == 0)
        {
            return false;
        }

        // Sort by start so duration-from-next is stable when mixed.
        drafts = drafts
            .OrderBy(d => d.Hour)
            .ThenBy(d => d.Minute)
            .ToList();

        var list = new List<LocalScheduleEvent>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var current = drafts[i];
            var duration = current.Duration ?? 60;
            if (current.Duration is null && i + 1 < drafts.Count)
            {
                var next = drafts[i + 1];
                var currentMinutes = current.Hour * 60 + current.Minute;
                var nextMinutes = next.Hour * 60 + next.Minute;
                var delta = nextMinutes - currentMinutes;
                if (delta > 0)
                {
                    duration = Math.Clamp(delta, 15, 480);
                }
            }

            list.Add(new LocalScheduleEvent(current.Title, current.Hour, current.Minute, duration, dayOffset));
        }

        events = list;
        return true;
    }

    /// <summary>0 = today, 1 = tomorrow. Unknown relative days stay 0.</summary>
    public static int DetectDayOffset(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Contains("明日", StringComparison.Ordinal)
            || text.Contains("あした", StringComparison.Ordinal)
            || text.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    /// <summary>Builds calendar_add_event arguments JSON (local destination + events[]).</summary>
    public static string ToAddEventArgumentsJson(IReadOnlyList<LocalScheduleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("destination", "local");
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var ev in events)
            {
                writer.WriteStartObject();
                writer.WriteString("title", ev.Title);
                writer.WriteNumber("hour", ev.Hour);
                writer.WriteNumber("minute", ev.Minute);
                writer.WriteNumber("duration_minutes", ev.DurationMinutes);
                if (ev.DayOffset > 0)
                {
                    writer.WriteNumber("day_offset", Math.Clamp(ev.DayOffset, 0, 14));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string FormatConfirmationLabel(IReadOnlyList<LocalScheduleEvent> events)
    {
        if (events.Count == 0)
        {
            return "Add events to today's local calendar.";
        }

        var dayOffset = events[0].DayOffset;
        var dayLabel = dayOffset <= 0 ? "today's" : dayOffset == 1 ? "tomorrow's" : $"day+{dayOffset}";
        var parts = events.Select(e =>
            e.Minute == 0
                ? $"{e.Hour:00}:00 {e.Title}"
                : $"{e.Hour:00}:{e.Minute:00} {e.Title}");
        return $"Add {events.Count} event(s) to {dayLabel} local calendar: {string.Join(", ", parts)}.";
    }

    private static bool TryReadClock(string hourText, string minuteText, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (!int.TryParse(hourText, NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)
            || !int.TryParse(minuteText, NumberStyles.Integer, CultureInfo.InvariantCulture, out minute))
        {
            return false;
        }

        if (hour == 24 && minute == 0)
        {
            // Midnight as end-of-day only; as a start time treat as 0:00 next day → reject as start.
            return false;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        return true;
    }

    private static bool TryReadEndClock(
        string hourText,
        string minuteText,
        int startHour,
        int startMinute,
        out int durationMinutes)
    {
        durationMinutes = 60;
        if (!int.TryParse(hourText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endHour)
            || !int.TryParse(minuteText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endMinute))
        {
            return false;
        }

        // Allow 24:00 as end-of-day.
        var endTotal = endHour == 24 && endMinute == 0
            ? 24 * 60
            : endHour * 60 + endMinute;
        if (endHour != 24 && (endHour is < 0 or > 23 || endMinute is < 0 or > 59))
        {
            return false;
        }

        var startTotal = startHour * 60 + startMinute;
        var delta = endTotal - startTotal;
        if (delta <= 0)
        {
            return false;
        }

        durationMinutes = Math.Clamp(delta, 15, 480);
        return true;
    }

    private static bool TryReadJapaneseStart(Match match, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)
            || hour is < 0 or > 23)
        {
            return false;
        }

        if (match.Groups[2].Success && match.Groups[2].Value == "半")
        {
            minute = 30;
        }
        else if (match.Groups[3].Success
                 && int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
        {
            minute = Math.Clamp(m, 0, 59);
        }

        return true;
    }

    private static bool TryReadJapaneseEnd(Match match, int startHour, int startMinute, out int durationMinutes)
    {
        durationMinutes = 60;
        // Groups: 4=endHour, 5=半, 6=endMinute digits (see JapaneseRangePattern).
        if (!int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endHour))
        {
            return false;
        }

        var endMinute = 0;
        if (match.Groups[5].Success && match.Groups[5].Value == "半")
        {
            endMinute = 30;
        }
        else if (match.Groups[6].Success
                 && int.TryParse(match.Groups[6].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
        {
            endMinute = Math.Clamp(m, 0, 59);
        }

        var endTotal = endHour == 24 ? 24 * 60 : endHour * 60 + endMinute;
        if (endHour is < 0 or > 24 || (endHour == 24 && endMinute != 0))
        {
            return false;
        }

        var startTotal = startHour * 60 + startMinute;
        var delta = endTotal - startTotal;
        if (delta <= 0)
        {
            return false;
        }

        durationMinutes = Math.Clamp(delta, 15, 480);
        return true;
    }

    private static string Normalize(string text)
    {
        var t = text.Trim();
        t = t.Replace('　', ' ');
        t = TrailingRequest().Replace(t, string.Empty);
        return t;
    }

    private static string CleanTitle(string raw)
    {
        var t = raw.Trim().Trim('、', ',', '・', ' ', '　');
        t = TrailingRequest().Replace(t, string.Empty).Trim();
        // Strip leading connectors from「まで、英語」 / 「から英語」 style captures.
        t = t.TrimStart('、', ',', '・', ' ', '　');
        if (t.StartsWith("から", StringComparison.Ordinal))
        {
            t = t[2..].Trim();
        }

        t = t.Trim('を', 'に', 'へ', 'は', 'が');
        // Drop residual range words stuck to titles.
        t = t.Replace("まで", string.Empty, StringComparison.Ordinal).Trim();
        t = TrailingCalendarNoise().Replace(t, string.Empty).Trim();
        return t.Trim();
    }

    /// <summary>22:00から23:00まで英語</summary>
    [GeneratedRegex(
        @"(\d{1,2}):(\d{2})\s*から\s*(\d{1,2}):(\d{2})\s*まで\s*[,、]?\s*([^0-9]+?)(?=(?:\d{1,2}:\d{2})|(?:\d{1,2}\s*時)|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ColonRangePattern();

    /// <summary>22時から23時半まで英語 — groups: startH, 半|分, startM, endH, 半|endM, title</summary>
    [GeneratedRegex(
        @"(\d{1,2})\s*時(?:(半)|(\d{1,2})\s*分)?\s*から\s*(\d{1,2})\s*時(?:(半)|(\d{1,2})\s*分)?\s*まで\s*[,、]?\s*([^0-9]+?)(?=(?:\d{1,2}\s*時)|(?:\d{1,2}:\d{2})|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseRangePattern();

    /// <summary>19時勉強 — groups: hour, 半, minutes, title</summary>
    [GeneratedRegex(
        @"(\d{1,2})\s*時(?:(半)|(\d{1,2})\s*分)?\s*([^0-9]+?)(?=(?:\d{1,2}\s*時)|(?:\d{1,2}:\d{2})|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseSlotPattern();

    /// <summary>22:30ギター</summary>
    [GeneratedRegex(
        @"(\d{1,2}):(\d{2})\s*([^0-9]+?)(?=(?:\d{1,2}:\d{2})|(?:\d{1,2}\s*時)|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ColonSlotPattern();

    [GeneratedRegex(
        @"(を)?\s*(いれて|入れて|追加して|反映して|記録して)?\s*(ください|下さい)?[.。!！？\s　]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingRequest();

    [GeneratedRegex(
        @"(の)?\s*(予定を)?\s*(ローカル)?\s*(カレンダー)?\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingCalendarNoise();
}

/// <summary>One parsed local schedule slot. <see cref="DayOffset"/> is days ahead of local today.</summary>
public readonly record struct LocalScheduleEvent(
    string Title,
    int Hour,
    int Minute,
    int DurationMinutes,
    int DayOffset = 0);
