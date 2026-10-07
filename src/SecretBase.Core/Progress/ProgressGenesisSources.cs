namespace SecretBase.Core.Progress;

/// <summary>Widget configuration values for ProgressWidgetConfiguration.Source.</summary>
public static class ProgressGenesisSources
{
    /// <summary>Default: progress API + Genesis MusicLab.</summary>
    public const string PersonalSystems = "personal-systems";

    public const string AgentArena = "agent-arena";
    public const string Local = "local";
    public const string Http = "http";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PersonalSystems;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "local" or "local-json" or "json" => Local,
            "http" or "https" or "remote" or "http-json" => Http,
            "agent-arena" or "arena" or "agentarena" => AgentArena,
            "personal-systems" or "personal" or "progress" or "genesis" or "progress-genesis"
                => PersonalSystems,
            _ => PersonalSystems
        };
    }

    public static bool IsPersonalSystems(string? value) =>
        string.Equals(Normalize(value), PersonalSystems, StringComparison.Ordinal);

    public static bool IsAgentArena(string? value) =>
        string.Equals(Normalize(value), AgentArena, StringComparison.Ordinal);

    public static bool IsLocal(string? value) =>
        string.Equals(Normalize(value), Local, StringComparison.Ordinal);

    public static bool IsHttp(string? value) =>
        string.Equals(Normalize(value), Http, StringComparison.Ordinal);
}
