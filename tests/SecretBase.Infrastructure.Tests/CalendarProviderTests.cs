using System.Text.Json;
using SecretBase.Core.Calendar;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Platform.Abstractions;

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

    [Fact]
    public void Create_WithMock_AddsMockProvider()
    {
        var config = new CalendarWidgetConfiguration
        {
            IncludeMockProvider = true,
            UseSampleAgendaWhenEmpty = false
        };
        var service = CalendarServiceFactory.Create(config, new FixedNow(DateTimeOffset.Now));
        Assert.Contains(service.Providers, p => p.ProviderId == "mock");
    }

    [Fact]
    public void Create_WithOAuthClientFile_AddsGoogleApiProvider()
    {
        var path = Path.Combine(Path.GetTempPath(), "secret-base-oauth-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{"installed":{"client_id":"test-client.apps.googleusercontent.com"}}""");
        try
        {
            var config = new CalendarWidgetConfiguration
            {
                EnableGoogleApiProvider = true,
                GoogleOAuthClientConfigPath = path,
                UseSampleAgendaWhenEmpty = false
            };
            var secrets = new MemorySecureSecretStore();
            var service = CalendarServiceFactory.Create(
                config,
                new FixedNow(DateTimeOffset.Now),
                secretStore: secrets,
                openBrowser: _ => true);

            Assert.Contains(service.Providers, p => p.DisplayName == "Google Calendar"
                && p.Capabilities.HasFlag(CalendarProviderCapabilities.Authentication));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FixedNow(DateTimeOffset instant) : ITimeProvider
    {
        public DateTimeOffset GetLocalNow() => instant;
    }
}

public class JsonCalendarAgendaCacheTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsEventsWithoutTokens()
    {
        var path = Path.Combine(Path.GetTempPath(), "sb-cal-cache-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var cache = new JsonCalendarAgendaCache(path);
            var start = new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.FromHours(9));
            cache.Save(
            [
                new CalendarEvent
                {
                    Id = "1",
                    Provider = CalendarProviderIds.Google,
                    CalendarName = "School",
                    Title = "Study",
                    Start = start,
                    End = start.AddHours(1),
                    Color = "#4285F4",
                    Source = "Google Calendar · School"
                }
            ],
            DateTimeOffset.UtcNow);

            var loaded = cache.TryLoad(out var savedAt);
            Assert.NotNull(loaded);
            Assert.NotNull(savedAt);
            Assert.Single(loaded!);
            Assert.Equal("Study", loaded![0].Title);
            Assert.Equal("#4285F4", loaded[0].Color);
            Assert.DoesNotContain("token", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("refresh", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public class GoogleOAuthClientConfigTests
{
    [Fact]
    public void TryLoad_ReadsInstalledClientId()
    {
        var path = Path.Combine(Path.GetTempPath(), "sb-oauth-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{"installed":{"client_id":"abc.apps.googleusercontent.com","client_secret":"nope"}}""");
        try
        {
            Assert.True(GoogleOAuthClientConfig.TryLoad(path, out var config, out var error));
            Assert.Null(error);
            Assert.Equal("abc.apps.googleusercontent.com", config!.ClientId);
            Assert.Equal("nope", config.ClientSecret);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        Assert.False(GoogleOAuthClientConfig.TryLoad(
            Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".json"),
            out _,
            out var error));
        Assert.NotNull(error);
    }
}

public class GoogleCalendarJsonMapperTests
{
    [Fact]
    public void TryMapEvent_TimedEvent()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "id": "evt1",
              "summary": "Programming",
              "start": { "dateTime": "2026-08-13T19:00:00+09:00" },
              "end": { "dateTime": "2026-08-13T21:00:00+09:00" },
              "location": "Desk",
              "htmlLink": "https://www.google.com/calendar/event?eid=x"
            }
            """);

        var cal = new CalendarInfo
        {
            Id = "primary",
            Name = "Primary",
            ProviderId = CalendarProviderIds.Google,
            Color = "#4285F4"
        };

        Assert.True(GoogleCalendarJsonMapper.TryMapEvent(doc.RootElement, cal, out var ev));
        Assert.Equal("Programming", ev.Title);
        Assert.Equal(19, ev.Start.Hour);
        Assert.Equal("Desk", ev.Location);
        Assert.Equal("#4285F4", ev.Color);
        Assert.Contains("Primary", ev.Source);
    }

    [Fact]
    public void TryMapEvent_AllDay()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "id": "day1",
              "summary": "Holiday",
              "start": { "date": "2026-08-13" },
              "end": { "date": "2026-08-14" }
            }
            """);

        var cal = new CalendarInfo { Id = "primary", Name = "Primary", ProviderId = "google", Color = "#4285F4" };
        Assert.True(GoogleCalendarJsonMapper.TryMapEvent(doc.RootElement, cal, out var ev));
        Assert.True(ev.IsAllDay);
        Assert.True(ev.OccursOn(new DateOnly(2026, 8, 13)));
    }

    [Fact]
    public void TryMapEvent_MissingStart_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("""{ "id": "x", "summary": "Nope" }""");
        var cal = new CalendarInfo { Id = "primary", Name = "Primary", ProviderId = "google" };
        Assert.False(GoogleCalendarJsonMapper.TryMapEvent(doc.RootElement, cal, out _));
    }
}

public class GoogleCalendarApiProviderAuthTests
{
    [Fact]
    public async Task GetEventsAsync_WithoutRefreshToken_ReturnsEmpty_NotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "sb-oauth2-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{"client_id":"test.apps.googleusercontent.com"}""");
        try
        {
            Assert.True(GoogleOAuthClientConfig.TryLoad(path, out var oauth, out _));
            var provider = new GoogleCalendarApiProvider(
                oauth,
                new MemorySecureSecretStore(),
                _ => true);

            Assert.Equal(CalendarAuthStatus.Disconnected, provider.AuthStatus);
            var events = await provider.GetEventsAsync(CalendarQuery.ForDay(new DateOnly(2026, 8, 13)));
            Assert.Empty(events);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
