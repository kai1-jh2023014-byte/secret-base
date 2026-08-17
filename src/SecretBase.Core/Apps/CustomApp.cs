namespace SecretBase.Core.Apps;

/// <summary>
/// User-registered app. Distinct from <c>CreativeProject</c> (a making-of record).
/// Registration never grants Shell, elevation, or arbitrary process args.
/// </summary>
public sealed class CustomApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string LaunchTarget { get; set; } = string.Empty;

    public CustomAppType Type { get; set; } = CustomAppType.Application;

    /// <summary>Optional absolute project folder for Cursor open. Not required to launch the app.</summary>
    public string? ProjectRoot { get; set; }

    /// <summary>Optional link to a Creative Project id (metadata only until resolved by AppCommand).</summary>
    public string? CreativeProjectId { get; set; }

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
}
