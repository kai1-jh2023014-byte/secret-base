namespace SecretBase.Core.Calendar;

/// <summary>Authentication / readiness for a calendar provider.</summary>
public enum CalendarAuthStatus
{
    /// <summary>Provider does not use auth (e.g. Local / Mock).</summary>
    NotApplicable = 0,

    /// <summary>Missing client configuration (e.g. no OAuth client file).</summary>
    NotConfigured = 1,

    /// <summary>Configured but user has not connected.</summary>
    Disconnected = 2,

    /// <summary>Connected; tokens available.</summary>
    Connected = 3,

    /// <summary>Token refresh failed or API denied access.</summary>
    Error = 4
}
