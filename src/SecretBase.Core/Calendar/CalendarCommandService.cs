using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Calendar;

/// <summary>
/// Thin Command boundary over <see cref="CalendarService"/>. Does not replace the Calendar Widget.
/// </summary>
public sealed class CalendarCommandService
{
    private readonly CalendarService _calendar;
    private readonly ITimeProvider _time;
    private readonly string _fallbackOpenUrl;

    public CalendarCommandService(
        CalendarService calendar,
        ITimeProvider time,
        string? fallbackOpenUrl = null)
    {
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _fallbackOpenUrl = string.IsNullOrWhiteSpace(fallbackOpenUrl)
            ? CalendarWidgetConfiguration.DefaultOpenCalendarUrl
            : fallbackOpenUrl;
    }

    public async Task<CalendarCommandResult> ExecuteAsync(
        CalendarCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            CalendarCommandKind.GetTodayEvents or CalendarCommandKind.Refresh =>
                await GetTodayAsync(command.Kind, cancellationToken).ConfigureAwait(false),
            CalendarCommandKind.Open => Open(),
            _ => CalendarCommandResult.Fail(command.Kind, "Unknown calendar command.")
        };
    }

    private async Task<CalendarCommandResult> GetTodayAsync(
        CalendarCommandKind kind,
        CancellationToken cancellationToken)
    {
        var snapshot = await _calendar.GetTodaySnapshotAsync(_time.GetLocalNow(), cancellationToken)
            .ConfigureAwait(false);
        return CalendarCommandResult.Ok(kind, snapshot.Events);
    }

    private CalendarCommandResult Open()
    {
        var fromProvider = _calendar.Providers
            .Select(p => p.OpenUrl)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
        var raw = fromProvider ?? _fallbackOpenUrl;
        if (!WebUrlValidator.TryNormalize(raw, out var url, out var error) || url is null)
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.Open, error ?? WebUrlValidator.BlockedMessage);
        }

        return CalendarCommandResult.Ok(
            CalendarCommandKind.Open,
            shouldLaunch: true,
            launchTarget: url,
            launchIsExternalLink: true);
    }
}
