namespace SecretBase.Core.Assistant;

/// <summary>
/// What Base AI may see. Catalog, not a dump. Secrets, paths, and file contents stay out.
/// </summary>
public static class BaseAiCatalog
{
    public const string AllowedSlices =
        "Allowed slices: local time, calendar titles, open todos, registered project/app names, "
        + "workspace title, focus state, ranked memory summaries, recent activity titles, "
        + "current situation with evidence, intent (never an action), session summaries, "
        + "deterministic search hits. AI outage does not take Secret Base down.";

    public const string Forbidden =
        "Never send: API keys, credentials, tokens, clipboard, keystrokes, absolute paths, "
        + "private file contents, unrestricted browser history.";
}
