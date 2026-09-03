using SecretBase.Core.Calendar;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Integration;

/// <summary>Mirrors existing OAuth secrets into non-secret integration memory. Never copies token values.</summary>
public static class IntegrationMemorySynchronizer
{
    public static void SyncFromSecrets(IIntegrationMemory memory, ISecureSecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(secrets);

        if (HasSecret(secrets, MusicSecretKeys.SpotifyRefreshToken))
        {
            memory.RememberConnected(IntegrationMemoryIds.Spotify, "Spotify", inAppExperience: true);
        }

        if (HasSecret(secrets, CalendarSecretKeys.GoogleRefreshToken)
            || HasSecret(secrets, GoogleCalendarApiProvider.SecretKeyRefresh))
        {
            memory.RememberConnected(IntegrationMemoryIds.GoogleCalendar, "Google Calendar", inAppExperience: true);
        }
    }

    private static bool HasSecret(ISecureSecretStore secrets, string key) =>
        secrets.TryGetSecret(key, out var value) && !string.IsNullOrWhiteSpace(value);
}
