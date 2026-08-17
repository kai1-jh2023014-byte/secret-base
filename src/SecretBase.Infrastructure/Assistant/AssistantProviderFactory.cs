using SecretBase.Core.Assistant;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Assistant;

/// <summary>Creates OpenAI, or stubs for Gemini/Local until those providers ship.</summary>
public sealed class AssistantProviderFactory : IAiProviderFactory
{
    private readonly ISecureSecretStore _secrets;
    private readonly HttpClient _http;

    public AssistantProviderFactory(ISecureSecretStore secrets, HttpClient? http = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public IAiProvider Create(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var id = string.IsNullOrWhiteSpace(settings.ProviderId)
            ? AssistantProviderIds.OpenAi
            : settings.ProviderId.Trim().ToLowerInvariant();

        if (string.Equals(id, AssistantProviderIds.Gemini, StringComparison.Ordinal))
        {
            return new UnavailableAiProvider(
                AssistantProviderIds.Gemini,
                "Gemini",
                "Gemini is not available in this MVP. Select OpenAI.");
        }

        if (string.Equals(id, AssistantProviderIds.Local, StringComparison.Ordinal))
        {
            return new UnavailableAiProvider(
                AssistantProviderIds.Local,
                "Local",
                "Local AI is not available in this MVP. Select OpenAI.");
        }

        return new OpenAiAssistantProvider(_http, () =>
        {
            _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var value);
            return value;
        });
    }
}
