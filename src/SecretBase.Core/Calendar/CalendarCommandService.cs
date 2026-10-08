using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Calendar;

/// <summary>
/// Thin Command boundary over <see cref="CalendarService"/>. Does not replace the Calendar Widget.
/// AI add-event defaults to the local Today agenda (widget). Google writes require
/// <see cref="CalendarEventDestinations.Google"/> and a connected writer.
/// Never OS files, never invented credentials.
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

    public CalendarService Calendar => _calendar;

    public async Task<CalendarCommandResult> ExecuteAsync(
        CalendarCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            CalendarCommandKind.GetTodayEvents or CalendarCommandKind.Refresh =>
                await GetTodayAsync(command.Kind, cancellationToken).ConfigureAwait(false),
            CalendarCommandKind.GetUpcoming =>
                await GetUpcomingAsync(command, cancellationToken).ConfigureAwait(false),
            CalendarCommandKind.Open => Open(),
            CalendarCommandKind.AddEvent => await AddEventAsync(command, cancellationToken).ConfigureAwait(false),
            CalendarCommandKind.RememberUsual => RememberUsual(command),
            CalendarCommandKind.ApplyUsual => ApplyUsual(),
            CalendarCommandKind.RemoveEvent => RemoveEvent(command),
            _ => CalendarCommandResult.Fail(command.Kind, "Unknown calendar command.")
        };
    }

    internal LocalCalendarProvider? Local =>
        _calendar.Providers.OfType<LocalCalendarProvider>().FirstOrDefault();

    internal ICalendarProvider? GoogleWriteProvider =>
        _calendar.Providers.FirstOrDefault(p =>
            p.Capabilities.HasFlag(CalendarProviderCapabilities.CreateEvents)
            && p is ICalendarEventWriter);

    internal ICalendarEventWriter? GoogleWriter => GoogleWriteProvider as ICalendarEventWriter;

    public IReadOnlyList<CalendarEvent> ListLocalEvents() =>
        Local?.ListAll() ?? [];

    private async Task<CalendarCommandResult> GetTodayAsync(
        CalendarCommandKind kind,
        CancellationToken cancellationToken)
    {
        var snapshot = await _calendar.GetTodaySnapshotAsync(_time.GetLocalNow(), cancellationToken)
            .ConfigureAwait(false);
        return CalendarCommandResult.Ok(kind, snapshot.Events);
    }

    private async Task<CalendarCommandResult> GetUpcomingAsync(
        CalendarCommand command,
        CancellationToken cancellationToken)
    {
        var query = CalendarQuery.ForUpcoming(_time.GetLocalNow(), command.Days);
        var snapshot = await _calendar.GetAgendaSnapshotAsync(query, cancellationToken)
            .ConfigureAwait(false);
        return CalendarCommandResult.Ok(CalendarCommandKind.GetUpcoming, snapshot.Events);
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

    private async Task<CalendarCommandResult> AddEventAsync(
        CalendarCommand command,
        CancellationToken cancellationToken)
    {
        var title = command.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.AddEvent, "Event title is required.");
        }

        var now = _time.GetLocalNow();
        var day = DateOnly.FromDateTime(now.DateTime);
        var hour = command.Hour is >= 0 and <= 23 ? command.Hour : now.Hour;
        var minute = Math.Clamp(command.Minute, 0, 59);
        var duration = command.DurationMinutes <= 0 ? 60 : Math.Clamp(command.DurationMinutes, 15, 480);
        DateTimeOffset start;
        DateTimeOffset end;
        if (command.IsAllDay)
        {
            start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), now.Offset);
            end = start.AddDays(1);
        }
        else
        {
            start = new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), now.Offset);
            end = start.AddMinutes(duration);
        }

        var destination = CalendarEventDestinations.Normalize(command.Destination);
        if (destination == CalendarEventDestinations.Local)
        {
            var local = Local;
            if (local is null)
            {
                return CalendarCommandResult.Fail(
                    CalendarCommandKind.AddEvent,
                    "Local calendar is unavailable.");
            }

            var createdLocal = local.AddEvent(title, start, end, command.IsAllDay);
            return CalendarCommandResult.Ok(CalendarCommandKind.AddEvent, [createdLocal]);
        }

        var google = GoogleWriteProvider;
        var writer = GoogleWriter;
        if (google is null || writer is null)
        {
            return CalendarCommandResult.Fail(
                CalendarCommandKind.AddEvent,
                "Google Calendar is not configured. Add google-oauth-client.json and connect Google in the Calendar widget.");
        }

        if (google.AuthStatus != CalendarAuthStatus.Connected)
        {
            return CalendarCommandResult.Fail(
                CalendarCommandKind.AddEvent,
                "Google Calendar is not connected. Open the Calendar widget and connect Google, then try again.");
        }

        try
        {
            var created = await writer
                .CreateEventAsync(title, start, end, command.IsAllDay, cancellationToken)
                .ConfigureAwait(false);
            return CalendarCommandResult.Ok(CalendarCommandKind.AddEvent, [created]);
        }
        catch (Exception ex)
        {
            return CalendarCommandResult.Fail(
                CalendarCommandKind.AddEvent,
                "Could not write to Google Calendar: " + ex.Message);
        }
    }

    private CalendarCommandResult RememberUsual(CalendarCommand command)
    {
        var local = Local;
        if (local is null)
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.RememberUsual, "Local calendar is unavailable.");
        }

        var title = command.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.RememberUsual, "Usual schedule title is required.");
        }

        var hour = command.Hour is >= 0 and <= 23 ? command.Hour : 9;
        var slot = local.RememberUsual(
            title,
            hour,
            Math.Clamp(command.Minute, 0, 59),
            command.DurationMinutes,
            command.IsAllDay);
        return CalendarCommandResult.Ok(CalendarCommandKind.RememberUsual, usual: slot);
    }

    private CalendarCommandResult ApplyUsual()
    {
        var local = Local;
        if (local is null)
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.ApplyUsual, "Local calendar is unavailable.");
        }

        var slots = local.ListUsual();
        if (slots.Count == 0)
        {
            return CalendarCommandResult.Fail(
                CalendarCommandKind.ApplyUsual,
                "No usual schedule is saved yet. Tell me a title and time to remember first.");
        }

        var now = _time.GetLocalNow();
        var day = DateOnly.FromDateTime(now.DateTime);
        var created = local.ApplyUsual(day, now.Offset);
        return CalendarCommandResult.Ok(CalendarCommandKind.ApplyUsual, created);
    }

    private CalendarCommandResult RemoveEvent(CalendarCommand command)
    {
        var local = Local;
        if (local is null)
        {
            return CalendarCommandResult.Fail(CalendarCommandKind.RemoveEvent, "Local calendar is unavailable.");
        }

        if (!local.TryRemoveEvent(command.EventId ?? string.Empty, out var removed) || removed is null)
        {
            return CalendarCommandResult.Fail(
                CalendarCommandKind.RemoveEvent,
                "Local event was not found. Secret Base does not delete files or Google events.");
        }

        return CalendarCommandResult.Ok(CalendarCommandKind.RemoveEvent, [removed]);
    }
}
