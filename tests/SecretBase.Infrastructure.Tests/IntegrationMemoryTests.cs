using SecretBase.Core.Calendar;
using SecretBase.Core.Integration;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Integration;

namespace SecretBase.Infrastructure.Tests;

public class JsonLocalCalendarStoreTests
{
    [Fact]
    public void SaveAndLoad_EventsAndUsual()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-cal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "local-calendar.json");
            var store = new JsonLocalCalendarStore(path);
            var day = new DateOnly(2026, 9, 3);
            var offset = TimeSpan.FromHours(9);
            store.SaveEvents(
            [
                new CalendarEvent
                {
                    Title = "Studio",
                    Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                    End = new DateTimeOffset(day.ToDateTime(new TimeOnly(21, 0)), offset)
                }
            ]);
            store.SaveUsual([new UsualScheduleSlot { Title = "東進", Hour = 14, Minute = 0, DurationMinutes = 120 }]);

            var reloaded = new JsonLocalCalendarStore(path);
            Assert.Single(reloaded.LoadEvents());
            Assert.Equal("Studio", reloaded.LoadEvents()[0].Title);
            Assert.Single(reloaded.LoadUsual());
            Assert.Equal("東進", reloaded.LoadUsual()[0].Title);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class JsonIntegrationMemoryStoreTests
{
    [Fact]
    public void RemembersConnected_WithoutTokens()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "integrations.json");
            var store = new JsonIntegrationMemoryStore(path);
            store.RememberConnected(IntegrationMemoryIds.Spotify, "Spotify", inAppExperience: true);
            store.RememberOpened(IntegrationMemoryIds.Classroom, "Google Classroom", inAppExperience: false);

            var json = File.ReadAllText(path);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bearer", json, StringComparison.Ordinal);

            var reloaded = new JsonIntegrationMemoryStore(path);
            var spotify = reloaded.Find(IntegrationMemoryIds.Spotify);
            Assert.NotNull(spotify);
            Assert.True(spotify!.Connected);
            Assert.True(spotify.InAppExperience);
            Assert.Contains("in-app", spotify.ToContextLabel(), StringComparison.OrdinalIgnoreCase);

            var classroom = reloaded.Find(IntegrationMemoryIds.Classroom);
            Assert.NotNull(classroom);
            Assert.False(classroom!.Connected);
            Assert.Contains("web widget", classroom.ToContextLabel(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
