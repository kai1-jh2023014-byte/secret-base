namespace SecretBase.Core.Assistant;

/// <summary>Result of resolving the active LLM provider with optional local fallback.</summary>
public sealed class AiProviderResolution
{
    public required IAiProvider Provider { get; init; }

    public required string ActiveProviderId { get; init; }

    public required string ActiveDisplayName { get; init; }

    public bool UsedFallback { get; init; }

    public string? FallbackNote { get; init; }
}

/// <summary>
/// Picks the preferred provider from settings, falling back to Local when remote is
/// not configured or unavailable. Does not start processes or touch the filesystem.
/// </summary>
public sealed class AiProviderResolver
{
    private readonly IAiProviderFactory _factory;

    public AiProviderResolver(IAiProviderFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public AiProviderResolution Resolve(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var preferredId = NormalizeId(settings.ProviderId);
        var preferred = _factory.CreateForProviderId(preferredId, settings);

        if (preferredId == AssistantProviderIds.Local)
        {
            return Wrap(preferred, preferredId, usedFallback: false);
        }

        return new AiProviderResolution
        {
            Provider = new FallbackAiProvider(preferred, preferredId, _factory, settings),
            ActiveProviderId = preferredId,
            ActiveDisplayName = preferred.DisplayName,
            UsedFallback = false
        };
    }

    private static AiProviderResolution Wrap(IAiProvider provider, string id, bool usedFallback, string? note = null) =>
        new()
        {
            Provider = provider,
            ActiveProviderId = id,
            ActiveDisplayName = provider.DisplayName,
            UsedFallback = usedFallback,
            FallbackNote = note
        };

    private static string NormalizeId(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? AssistantProviderIds.OpenAi
            : id.Trim().ToLowerInvariant();
}

/// <summary>Delegates to a primary provider, then Local on NotConfigured / Unavailable.</summary>
internal sealed class FallbackAiProvider : IAiProvider
{
    private readonly IAiProvider _primary;
    private readonly string _preferredId;
    private readonly IAiProviderFactory _factory;
    private readonly AssistantSettings _settings;

    public FallbackAiProvider(
        IAiProvider primary,
        string preferredId,
        IAiProviderFactory factory,
        AssistantSettings settings)
    {
        _primary = primary;
        _preferredId = preferredId;
        _factory = factory;
        _settings = settings;
        ProviderId = preferredId;
        DisplayName = primary.DisplayName;
    }

    public string ProviderId { get; private set; }

    public string DisplayName { get; private set; }

    public string? LastFallbackNote { get; private set; }

    public async Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default)
    {
        var primary = await _primary.ChatAsync(messages, tools, model, cancellationToken).ConfigureAwait(false);
        if (primary.Status == AiProviderStatus.Ok)
        {
            ProviderId = _preferredId;
            DisplayName = _primary.DisplayName;
            LastFallbackNote = null;
            return primary;
        }

        if (string.Equals(_preferredId, AssistantProviderIds.Local, StringComparison.Ordinal))
        {
            return primary;
        }

        var local = _factory.CreateForProviderId(AssistantProviderIds.Local, _settings);
        var fallback = await local.ChatAsync(messages, tools, ResolveLocalModel(model), cancellationToken)
            .ConfigureAwait(false);
        if (fallback.Status == AiProviderStatus.Ok)
        {
            ProviderId = AssistantProviderIds.Local;
            DisplayName = local.DisplayName;
            LastFallbackNote = DescribeFallback(_preferredId, primary);
            return fallback;
        }

        ProviderId = _preferredId;
        DisplayName = _primary.DisplayName;
        return primary;
    }

    private static string ResolveLocalModel(string model) =>
        string.IsNullOrWhiteSpace(model) ? AssistantSettings.DefaultLocalModel : model;

    private static string DescribeFallback(string preferredId, AiProviderResponse primary)
    {
        var name = preferredId switch
        {
            AssistantProviderIds.Gemini => "Gemini",
            AssistantProviderIds.OpenAi => "OpenAI",
            _ => preferredId
        };

        return primary.Status == AiProviderStatus.NotConfigured
            ? $"{name} is not configured. Using Local AI."
            : $"{name} is unavailable. Using Local AI.";
    }
}
