using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;

namespace SecretBase.Core.Widgets.Clock;

/// <summary>
/// Pure formatting for Clock display — no UI dependencies; easy to unit test.
/// </summary>
public static class ClockDisplayFormatter
{
    public static string FormatTime(DateTimeOffset instant, ClockWidgetConfiguration configuration)
    {
        // ITimeProvider.GetLocalNow() already returns local wall-clock time.
        if (configuration.Use24HourFormat)
        {
            return configuration.ShowSeconds
                ? instant.ToString("HH:mm:ss")
                : instant.ToString("HH:mm");
        }

        return configuration.ShowSeconds
            ? instant.ToString("hh:mm:ss tt")
            : instant.ToString("hh:mm tt");
    }

    public static string FormatDate(DateTimeOffset instant, ClockWidgetConfiguration configuration)
    {
        if (!configuration.ShowDate)
        {
            return string.Empty;
        }

        return configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleFocus => instant.ToString("dddd, MMMM d"),
            ClockWidgetConfiguration.StyleMinimal => instant.ToString("MMM d"),
            _ => $"{instant:dddd}\n{instant:MMMM d}"
        };
    }

    public static (string Time, string Date) Format(ITimeProvider timeProvider, ClockWidgetConfiguration configuration)
    {
        var now = timeProvider.GetLocalNow();
        return (FormatTime(now, configuration), FormatDate(now, configuration));
    }
}
