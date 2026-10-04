namespace SecretBase.Core.Jev;

/// <summary>
/// Jev is the decision layer. These ids are not conversation-provider ids.
/// </summary>
public static class JevSecretKeys
{
    /// <summary>
    /// Credential Manager / Keychain entry. Distinct from
    /// <c>Assistant/OpenAI/ApiKey</c> and <c>Assistant/Gemini/ApiKey</c>.
    /// </summary>
    public const string ApiKey = "Jev/ApiKey";
}

public static class JevUserMessages
{
    public const string NotConfigured = "Jev API key is not set.";

    public const string Connected = "Jev connected. Decision AI answered a connection check.";

    public const string AuthenticationFailed = "Jev rejected the API key.";

    public const string InsufficientCredits = "Jev account has insufficient credits.";

    public const string Inactive = "Jev account is inactive.";

    public const string Unavailable = "Jev decision AI is unavailable.";

    public const string Timeout = "Jev decision timed out.";

    public const string NetworkError = "Could not connect to Jev.";

    public const string InvalidResponse = "Jev returned an unexpected response.";

    public const string WrongKey =
        "That key belongs to a conversation provider. Jev keeps its own API key.";

    public const string ConversationKeyIgnored =
        "A Jev key was not saved as the conversation provider key.";
}
