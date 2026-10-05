using System.Globalization;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;

namespace SecretBase.Core.Widgets.Clock;

/// <summary>
/// Pure formatting for Clock display — no UI dependencies; easy to unit test.
/// Dates always use English (en-US), independent of the OS UI language.
/// </summary>
public static class ClockDisplayFormatter
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string FormatTime(DateTimeOffset instant, ClockWidgetConfiguration configuration)
    {
        // ITimeProvider.GetLocalNow() already returns local wall-clock time.
        if (configuration.Use24HourFormat)
        {
            return configuration.ShowSeconds
                ? instant.ToString("HH:mm:ss", English)
                : instant.ToString("HH:mm", English);
        }

        return configuration.ShowSeconds
            ? instant.ToString("hh:mm:ss tt", English)
            : instant.ToString("hh:mm tt", English);
    }

    public static string FormatDate(DateTimeOffset instant, ClockWidgetConfiguration configuration)
    {
        if (!configuration.ShowDate)
        {
            return string.Empty;
        }

        return configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleLarge => instant.ToString("dddd, MMMM dd", English),
            ClockWidgetConfiguration.StyleDigital => instant.ToString("dddd, MMMM dd", English),
            ClockWidgetConfiguration.StyleFocus => instant.ToString("dddd, MMMM d", English),
            ClockWidgetConfiguration.StyleMinimal => instant.ToString("MMM d", English),
            _ => instant.ToString("dddd", English) + "\n" + instant.ToString("MMMM d", English)
        };
    }

    public static (string Time, string Date) Format(ITimeProvider timeProvider, ClockWidgetConfiguration configuration)
    {
        var now = timeProvider.GetLocalNow();
        return (FormatTime(now, configuration), FormatDate(now, configuration));
    }

    public static (string Time, string Date, string Next, string Status) FormatBase(
        ITimeProvider timeProvider,
        ClockWidgetConfiguration configuration,
        ClockBaseStatus? status)
    {
        var (time, date) = Format(timeProvider, configuration);
        if (configuration.ShowSeconds
            && string.Equals(configuration.DisplayStyle, ClockWidgetConfiguration.StyleBase, StringComparison.Ordinal))
        {
            var noSeconds = new ClockWidgetConfiguration
            {
                DisplayStyle = configuration.DisplayStyle,
                Use24HourFormat = configuration.Use24HourFormat,
                ShowSeconds = false,
                ShowDate = configuration.ShowDate,
                SizeScale = configuration.SizeScale,
                DateScale = configuration.DateScale
            };
            time = FormatTime(timeProvider.GetLocalNow(), noSeconds);
        }

        return (time, date, status?.NextLine ?? string.Empty, status?.StatusLine ?? string.Empty);
    }
}
