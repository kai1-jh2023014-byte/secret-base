using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Parses Japanese one-day schedule phrases into local calendar_add_event payloads
/// without calling a remote model (avoids timeout for 「19時勉強、20時…をいれて」).
/// </summary>
public static partial class LocalScheduleParser
{
    private static readonly Regex TimedSlot = TimedSlotPattern();

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
                         || text.Contains("add", StringComparison.OrdinalIgnoreCase);
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
        var matches = TimedSlot.Matches(cleaned);
        if (matches.Count == 0)
        {
            return false;
        }

        var drafts = new List<(int Hour, int Minute, string Title)>();
        foreach (Match match in matches)
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

            drafts.Add((hour, minute, title));
        }

        if (drafts.Count == 0)
        {
            return false;
        }

        var list = new List<LocalScheduleEvent>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var current = drafts[i];
            var duration = 60;
            if (i + 1 < drafts.Count)
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

            list.Add(new LocalScheduleEvent(current.Title, current.Hour, current.Minute, duration));
        }

        events = list;
        return true;
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

        var parts = events.Select(e =>
            e.Minute == 0
                ? $"{e.Hour:00}:00 {e.Title}"
                : $"{e.Hour:00}:{e.Minute:00} {e.Title}");
        return $"Add {events.Count} event(s) to today's local calendar: {string.Join(", ", parts)}.";
    }

    private static string Normalize(string text)
    {
        var t = text.Trim();
        t = t.Replace('　', ' ');
        // Drop trailing request phrases so they are not part of the last title.
        t = TrailingRequest().Replace(t, string.Empty);
        return t;
    }

    private static string CleanTitle(string raw)
    {
        var t = raw.Trim().Trim('、', ',', '・', ' ', '　');
        t = TrailingRequest().Replace(t, string.Empty).Trim();
        t = t.Trim('を', 'に', 'へ', 'は', 'が');
        return t.Trim();
    }

    /// <summary>
    /// Groups: 1=hour, 2=半, 3=minutes, 4=title until next timed slot.
    /// </summary>
    [GeneratedRegex(
        @"(\d{1,2})\s*時(?:(半)|(\d{1,2})\s*分)?\s*([^0-9]+?)(?=(?:\d{1,2}\s*時)|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimedSlotPattern();

    [GeneratedRegex(
        @"(を)?\s*(いれて|入れて|追加して|反映して)?\s*(ください|下さい)?[.。!！？\s　]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingRequest();
}

/// <summary>One parsed local schedule slot.</summary>
public readonly record struct LocalScheduleEvent(string Title, int Hour, int Minute, int DurationMinutes);
