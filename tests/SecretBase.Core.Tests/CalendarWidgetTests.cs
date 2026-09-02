using System.Globalization;
using SecretBase.Core.Calendar;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Calendar;

namespace SecretBase.Core.Tests;

public class CalendarEventTests
{
    [Fact]
    public void OccursOn_TimedEvent_MatchesLocalDate()
    {
        var ev = new CalendarEvent
        {
            Title = "Programming",
            Start = new DateTimeOffset(2026, 8, 13, 14, 0, 0, TimeSpan.FromHours(9)),
            End = new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.FromHours(9))
        };

        Assert.True(ev.OccursOn(new DateOnly(2026, 8, 13)));
        Assert.False(ev.OccursOn(new DateOnly(2026, 8, 14)));
    }

    [Fact]
    public void OccursOn_TimedEvent_UsesEventOffsetCalendarDate_NotMachineLocalTimezone()
    {
        var ev = new CalendarEvent
        {
            Title = "東進",
            Start = new DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.FromHours(9)),
            End = new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.FromHours(9))
        };

        Assert.True(ev.OccursOn(new DateOnly(2026, 8, 24)));
        Assert.False(ev.OccursOn(new DateOnly(2026, 8, 23)));
    }

    [Fact]
    public void OccursOn_AllDayExclusiveEnd_IncludesStartDayOnly()
    {
        var start = new DateTimeOffset(new DateOnly(2026, 8, 13).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var ev = new CalendarEvent
        {
            Title = "Holiday",
            Start = start,
            End = start.AddDays(1),
            IsAllDay = true
        };

        Assert.True(ev.OccursOn(new DateOnly(2026, 8, 13)));
        Assert.False(ev.OccursOn(new DateOnly(2026, 8, 14)));
    }
}

public class CalendarServiceTests
{
    [Fact]
    public async Task GetTodayAgendaAsync_MergesProvidersAndOrdersByStart()
    {
        var day = new DateOnly(2026, 8, 13);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "Guitar",
                Provider = CalendarProviderIds.Local,
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
            },
            new CalendarEvent
            {
                Title = "東進",
                Provider = CalendarProviderIds.Local,
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), offset)
            }
        ]);

        var service = new CalendarService([local]);
        var agenda = await service.GetTodayAgendaAsync(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset));

        Assert.Equal(2, agenda.Count);
        Assert.Equal("東進", agenda[0].Title);
        Assert.Equal("Guitar", agenda[1].Title);
    }

    [Fact]
    public async Task GetAgendaAsync_SkipsFailingProvider()
    {
        var service = new CalendarService([new ThrowingProvider(), EmptyCalendarProvider.Instance]);
        var agenda = await service.GetAgendaAsync(CalendarQuery.ForDay(new DateOnly(2026, 8, 13)));
        Assert.Empty(agenda);
    }

    [Fact]
    public async Task GetAgendaSnapshotAsync_IsolatesProviderFailure()
    {
        var day = new DateOnly(2026, 8, 13);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "Ok",
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(11, 0)), offset)
            }
        ]);

        var service = new CalendarService([new ThrowingProvider(), local]);
        var snap = await service.GetAgendaSnapshotAsync(CalendarQuery.ForDay(day));
        Assert.Single(snap.Events);
        Assert.Equal("Ok", snap.Events[0].Title);
        Assert.Contains(snap.ProviderResults, r => !r.Succeeded && r.ProviderId == "boom");
        Assert.Contains(snap.ProviderResults, r => r.Succeeded && r.ProviderId == CalendarProviderIds.Local);
    }

    [Fact]
    public async Task MockProvider_ReturnsColoredTodayAgenda()
    {
        var now = new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.FromHours(9));
        var mock = new MockCalendarProvider(() => now);
        Assert.True(mock.Capabilities.HasFlag(CalendarProviderCapabilities.ReadEvents));
        Assert.Equal(CalendarAuthStatus.NotApplicable, mock.AuthStatus);

        var service = new CalendarService([mock]);
        var agenda = await service.GetTodayAgendaAsync(now);
        Assert.Equal(4, agenda.Count);
        Assert.All(agenda, e => Assert.False(string.IsNullOrWhiteSpace(e.Color)));
    }

    private sealed class ThrowingProvider : ICalendarProvider
    {
        public string ProviderId => "boom";
        public string DisplayName => "Boom";
        public string? OpenUrl => null;
        public CalendarProviderCapabilities Capabilities => CalendarProviderCapabilities.ReadEvents;
        public CalendarAuthStatus AuthStatus => CalendarAuthStatus.Connected;
        public bool IsConfigured => true;

        public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarInfo>>(Array.Empty<CalendarInfo>());

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            CalendarQuery query,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider down");

        public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public class CalendarProviderCapabilitiesTests
{
    [Fact]
    public void Flags_CombineWithoutImplyingWrite()
    {
        var caps = CalendarProviderCapabilities.ReadEvents
            | CalendarProviderCapabilities.ListCalendars
            | CalendarProviderCapabilities.Authentication;

        Assert.True(caps.HasFlag(CalendarProviderCapabilities.ReadEvents));
        Assert.False(caps.HasFlag(CalendarProviderCapabilities.CreateEvents));
        Assert.False(caps.HasFlag(CalendarProviderCapabilities.DeleteEvents));
    }

    [Fact]
    public void ProviderIds_AreStable()
    {
        Assert.Equal("local", CalendarProviderIds.Local);
        Assert.Equal("google", CalendarProviderIds.Google);
        Assert.Equal("notion", CalendarProviderIds.Notion);
    }
}

public class IcsCalendarParserTests
{
    [Fact]
    public void Parse_ReadsVEventSummaryAndTimes()
    {
        var ics = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            UID:abc-123
            SUMMARY:東進
            DTSTART:20260813T000000Z
            DTEND:20260813T030000Z
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarParser.Parse(ics, CalendarProviderIds.Google, "Google Calendar");
        Assert.Single(events);
        Assert.Equal("東進", events[0].Title);
        Assert.Equal(CalendarProviderIds.Google, events[0].Provider);
        Assert.Equal("Google Calendar", events[0].CalendarName);
        Assert.Equal("abc-123", events[0].Id);
    }

    [Fact]
    public void Parse_AllDayValueDate()
    {
        var ics = """
            BEGIN:VEVENT
            UID:day-1
            SUMMARY:Holiday
            DTSTART;VALUE=DATE:20260813
            DTEND;VALUE=DATE:20260814
            END:VEVENT
            """;

        var events = IcsCalendarParser.Parse(ics, CalendarProviderIds.Google);
        Assert.Single(events);
        Assert.True(events[0].IsAllDay);
        Assert.True(events[0].OccursOn(new DateOnly(2026, 8, 13)));
    }
}

public class CalendarWidgetConfigurationTests
{
    [Fact]
    public void RoundTrip_PreservesTimedEventsAndGoogleIcsUrl()
    {
        var original = new CalendarWidgetConfiguration
        {
            GoogleIcsUrl = "https://calendar.google.com/calendar/ical/example/private/basic.ics",
            OpenCalendarUrl = "https://calendar.google.com/",
            UseSampleAgendaWhenEmpty = false,
            IncludeMockProvider = true,
            EnableGoogleApiProvider = false,
            Events =
            [
                new CalendarEvent
                {
                    Id = "evt-1",
                    Provider = CalendarProviderIds.Local,
                    CalendarName = "Local",
                    Title = "Programming",
                    Start = DateTimeOffset.Parse("2026-08-13T14:00:00+09:00", CultureInfo.InvariantCulture),
                    End = DateTimeOffset.Parse("2026-08-13T16:00:00+09:00", CultureInfo.InvariantCulture),
                    Location = "Desk",
                    Color = "#3498DB",
                    Source = "Local"
                }
            ]
        };

        var restored = CalendarWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal(original.GoogleIcsUrl, restored.GoogleIcsUrl);
        Assert.False(restored.UseSampleAgendaWhenEmpty);
        Assert.True(restored.IncludeMockProvider);
        Assert.False(restored.EnableGoogleApiProvider);
        Assert.Single(restored.Events);
        Assert.Equal("Programming", restored.Events[0].Title);
        Assert.Equal("Desk", restored.Events[0].Location);
        Assert.Equal("#3498DB", restored.Events[0].Color);
        Assert.Equal(14, restored.Events[0].Start.Hour);
    }

    [Fact]
    public void FromDictionary_LegacyDateOnly_BecomesAllDay()
    {
        var bag = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["Events"] = System.Text.Json.JsonSerializer.SerializeToElement(new[]
            {
                new Dictionary<string, object?>
                {
                    ["Id"] = "legacy",
                    ["Date"] = "2026-08-13",
                    ["Title"] = "Legacy note",
                    ["Notes"] = "old"
                }
            })
        };

        var restored = CalendarWidgetConfiguration.FromDictionary(bag);
        Assert.Single(restored.Events);
        Assert.True(restored.Events[0].IsAllDay);
        Assert.Equal("Legacy note", restored.Events[0].Title);
        Assert.Equal("old", restored.Events[0].Description);
    }

    [Fact]
    public void CreateCalendar_DoesNotSeedDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateCalendar();
        Assert.Equal(WidgetTypes.Calendar, widget.Type);
        var layout = SecretBase.Core.Desktop.DesktopLayout.CreateDefault();
        Assert.Equal(2, layout.Widgets.Count);
        Assert.DoesNotContain(layout.Widgets, w => w.Type == WidgetTypes.Calendar);
    }

    [Fact]
    public void SampleCreativeDay_HasThreeTimedBlocks()
    {
        var samples = SampleCalendarEvents.CreateCreativeDay(new DateOnly(2026, 8, 13), TimeSpan.FromHours(9));
        Assert.Equal(3, samples.Count);
        Assert.Equal("東進", samples[0].Title);
        Assert.Equal("Programming", samples[1].Title);
        Assert.Equal("Guitar", samples[2].Title);
    }
}
