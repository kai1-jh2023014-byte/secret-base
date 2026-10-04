using SecretBase.Core.Assistant;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Assistant;

/// <summary>Creates OpenAI, Gemini, Ollama, or fallback-wrapped providers.</summary>
public sealed class AssistantProviderFactory : IAiProviderFactory
{
    private readonly ISecureSecretStore _secrets;
    private readonly HttpClient _http;
    private readonly AiProviderResolver _resolver;

    public AssistantProviderFactory(ISecureSecretStore secrets, HttpClient? http = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _resolver = new AiProviderResolver(this);
    }

    public IAiProvider Create(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(settings);
        var runtime = AssistantProviderSelection.ForRuntime(
            migrated,
            HasSecret(AssistantSecretKeys.OpenAiApiKey),
            HasSecret(AssistantSecretKeys.GeminiApiKey));
        return _resolver.Resolve(runtime).Provider;
    }

    private bool HasSecret(string key) =>
        _secrets.TryGetSecret(key, out var value) && !string.IsNullOrWhiteSpace(value);

    public IAiProvider CreateForProviderId(string providerId, AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(settings);
        var id = string.IsNullOrWhiteSpace(providerId)
            ? AssistantProviderIds.OpenAi
            : providerId.Trim().ToLowerInvariant();

        return id switch
        {
            AssistantProviderIds.Gemini => new GeminiAssistantProvider(_http, () =>
            {
                _secrets.TryGetSecret(AssistantSecretKeys.GeminiApiKey, out var value);
                return value;
            }),
            AssistantProviderIds.Local => new OllamaAssistantProvider(
                _http,
                () => migrated.LocalBaseUrl,
                () => migrated.LocalModel),
            _ => new OpenAiAssistantProvider(_http, () =>
            {
                _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var value);
                return value;
            })
        };
    }

    public async Task<bool> ProbeAvailabilityAsync(
        string providerId,
        AssistantSettings settings,
        CancellationToken cancellationToken = default)
    {
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(settings);
        var id = string.IsNullOrWhiteSpace(providerId)
            ? AssistantProviderIds.OpenAi
            : providerId.Trim().ToLowerInvariant();

        return id switch
        {
            AssistantProviderIds.OpenAi =>
                _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var openAi)
                && !string.IsNullOrWhiteSpace(openAi),
            AssistantProviderIds.Gemini =>
                _secrets.TryGetSecret(AssistantSecretKeys.GeminiApiKey, out var gemini)
                && !string.IsNullOrWhiteSpace(gemini),
            AssistantProviderIds.Local =>
                await LocalAiAvailabilityProbe.IsOllamaReachableAsync(
                    _http,
                    migrated.LocalBaseUrl,
                    cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }
}
