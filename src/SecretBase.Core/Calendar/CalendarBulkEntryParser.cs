using System.Globalization;
using System.Text.RegularExpressions;
using SecretBase.Core.Assistant;

namespace SecretBase.Core.Calendar;

/// <summary>
/// Parses multi-line / JP day schedules into local timed slots for bulk add in the Calendar widget.
/// Prefers line / 「、」 segments so English <c>HH:mm-HH:mm</c> is not mangled by the shared slot regex.
/// </summary>
public static partial class CalendarBulkEntryParser
{
    public static bool TryParse(string? text, out IReadOnlyList<LocalScheduleEvent> events)
    {
        events = Array.Empty<LocalScheduleEvent>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var segments = SplitSegments(normalized);
        var drafts = new List<LocalScheduleEvent>();
        foreach (var segment in segments)
        {
            if (TryParseSegment(segment, out var slot))
            {
                drafts.Add(slot);
            }
        }

        if (drafts.Count == 0
            && LocalScheduleParser.TryParse(normalized, out var blob)
            && blob.Count > 0)
        {
            drafts.AddRange(blob);
        }

        if (drafts.Count == 0)
        {
            return false;
        }

        events = drafts
            .OrderBy(e => e.Hour)
            .ThenBy(e => e.Minute)
            .ToList();
        return true;
    }

    public static string FormatPreview(IReadOnlyList<LocalScheduleEvent> events)
    {
        if (events.Count == 0)
        {
            return "No events parsed.";
        }

        var parts = events.Select(e =>
        {
            var endTotal = e.Hour * 60 + e.Minute + Math.Max(15, e.DurationMinutes);
            var endH = (endTotal / 60) % 24;
            var endM = endTotal % 60;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{e.Hour:00}:{e.Minute:00} 〜 {endH:00}:{endM:00}  {e.Title}");
        });
        return string.Join('\n', parts);
    }

    private static IEnumerable<string> SplitSegments(string text)
    {
        // Newlines first, then Japanese enumeration commas inside each line.
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Contains('、') || line.Contains('，'))
            {
                foreach (var part in JapaneseCommaSplit().Split(line))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length > 0)
                    {
                        yield return trimmed;
                    }
                }
            }
            else
            {
                yield return line;
            }
        }
    }

    private static bool TryParseSegment(string segment, out LocalScheduleEvent slot)
    {
        slot = default;

        var range = EnglishRangeLine().Match(segment);
        if (range.Success
            && int.TryParse(range.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sh)
            && int.TryParse(range.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sm)
            && int.TryParse(range.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var eh)
            && int.TryParse(range.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var em)
            && sh is >= 0 and <= 23 && sm is >= 0 and <= 59
            && ((eh is >= 0 and <= 23 && em is >= 0 and <= 59) || (eh == 24 && em == 0)))
        {
            var title = range.Groups[5].Value.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var startTotal = sh * 60 + sm;
            var endTotal = eh == 24 && em == 0 ? 24 * 60 : eh * 60 + em;
            var duration = endTotal - startTotal;
            if (duration <= 0)
            {
                return false;
            }

            slot = new LocalScheduleEvent(title, sh, sm, Math.Clamp(duration, 15, 480));
            return true;
        }

        var single = EnglishSlotLine().Match(segment);
        if (single.Success
            && int.TryParse(single.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            && int.TryParse(single.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute)
            && hour is >= 0 and <= 23 && minute is >= 0 and <= 59)
        {
            var title = single.Groups[3].Value.Trim();
            // Reject mangled titles from "10:00-11:30" when range regex missed.
            if (string.IsNullOrWhiteSpace(title) || title is "-" or "–" or "—")
            {
                return false;
            }

            slot = new LocalScheduleEvent(title, hour, minute, 60);
            return true;
        }

        if (LocalScheduleParser.TryParse(segment, out var one) && one.Count >= 1)
        {
            // Prefer the first when a segment somehow expands.
            slot = one[0];
            return true;
        }

        if (LocalScheduleParser.TryParse(segment + "をいれて", out one) && one.Count >= 1)
        {
            slot = one[0];
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"[、，]", RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseCommaSplit();

    /// <summary>09:00-10:30 Study / 09:00 – 10:30 Study</summary>
    [GeneratedRegex(
        @"^\s*(\d{1,2}):(\d{2})\s*[-–—〜~]\s*(\d{1,2}):(\d{2})\s+(.+?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnglishRangeLine();

    /// <summary>09:00 Study</summary>
    [GeneratedRegex(
        @"^\s*(\d{1,2}):(\d{2})\s+(.+?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSlotLine();
}
