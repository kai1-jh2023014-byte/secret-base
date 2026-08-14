using SecretBase.Core.Calendar;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Infrastructure.Calendar;

namespace SecretBase.Infrastructure.Tests;

public class CalendarServiceFactoryTests
{
    [Fact]
    public async Task Create_WithoutIcs_UsesSampleAgenda()
    {
        var config = CalendarWidgetConfiguration.CreateDefault();
        var now = new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.FromHours(9));
        var service = CalendarServiceFactory.Create(config, new FixedNow(now));
        var agenda = await service.GetTodayAgendaAsync(now);
        Assert.Equal(3, agenda.Count);
        Assert.Contains(agenda, e => e.Title == "東進");
    }

    [Fact]
    public void Create_WithIcs_AddsGoogleProvider()
    {
        var config = new CalendarWidgetConfiguration
        {
            GoogleIcsUrl = "https://calendar.google.com/calendar/ical/x/private/basic.ics",
            UseSampleAgendaWhenEmpty = false
        };
        var service = CalendarServiceFactory.Create(config, new FixedNow(DateTimeOffset.Now));
        Assert.Contains(service.Providers, p => p.ProviderId == CalendarProviderIds.Google);
        Assert.Contains(service.Providers, p => p.ProviderId == CalendarProviderIds.Local);
    }

    private sealed class FixedNow(DateTimeOffset instant) : ITimeProvider
    {
        public DateTimeOffset GetLocalNow() => instant;
    }
}
