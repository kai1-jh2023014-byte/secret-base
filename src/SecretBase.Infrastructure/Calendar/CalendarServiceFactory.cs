using System.Net.Http;
using SecretBase.Core.Calendar;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Builds a <see cref="CalendarService"/> from widget configuration.
/// Local events always included; Google ICS / OAuth API / Mock added when configured.
/// Tokens never live in configuration — only in <see cref="ISecureSecretStore"/>.
/// </summary>
public static class CalendarServiceFactory
{
    public static CalendarService Create(
        CalendarWidgetConfiguration configuration,
        ITimeProvider timeProvider,
        ISecureSecretStore? secretStore = null,
        Func<string, bool>? openBrowser = null,
        HttpClient? httpClient = null)
    {
        var providers = new List<ICalendarProvider>();

        var localEvents = configuration.Events.ToList();
        if (localEvents.Count == 0
            && configuration.UseSampleAgendaWhenEmpty
            && string.IsNullOrWhiteSpace(configuration.GoogleIcsUrl)
            && !configuration.IncludeMockProvider
            && !WillAddGoogleApi(configuration, secretStore, openBrowser))
        {
            var now = timeProvider.GetLocalNow();
            var day = DateOnly.FromDateTime(now.DateTime);
            localEvents.AddRange(SampleCalendarEvents.CreateCreativeDay(day, now.Offset));
        }

        providers.Add(new LocalCalendarProvider(
            localEvents,
            displayName: "Local",
            openUrl: configuration.OpenCalendarUrl));

        if (configuration.IncludeMockProvider)
        {
            providers.Add(new MockCalendarProvider(() => timeProvider.GetLocalNow()));
        }

        if (WillAddGoogleApi(configuration, secretStore, openBrowser)
            && secretStore is not null
            && openBrowser is not null
            && GoogleOAuthClientConfig.TryLoad(configuration.GoogleOAuthClientConfigPath, out var oauth, out _)
            && oauth is not null)
        {
            providers.Add(new GoogleCalendarApiProvider(oauth, secretStore, openBrowser, httpClient));
        }

        if (!string.IsNullOrWhiteSpace(configuration.GoogleIcsUrl))
        {
            providers.Add(new GoogleCalendarIcsProvider(configuration.GoogleIcsUrl, httpClient));
        }

        return new CalendarService(providers);
    }

    private static bool WillAddGoogleApi(
        CalendarWidgetConfiguration configuration,
        ISecureSecretStore? secretStore,
        Func<string, bool>? openBrowser) =>
        configuration.EnableGoogleApiProvider
        && secretStore is not null
        && openBrowser is not null
        && GoogleOAuthClientConfig.TryLoad(configuration.GoogleOAuthClientConfigPath, out var oauth, out _)
        && oauth is not null;
}
