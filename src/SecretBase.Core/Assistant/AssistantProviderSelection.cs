namespace SecretBase.Core.Assistant;

/// <summary>
/// Picks the provider the shelf and chat should actually use.
/// An explicit Local choice stays Local. A saved Gemini key is used when the
/// selected remote provider has no key of its own.
/// </summary>
public static class AssistantProviderSelection
{
    public static string ResolveActiveProviderId(string? providerId, bool hasOpenAiKey, bool hasGeminiKey)
    {
        var id = Normalize(providerId);
        if (id == AssistantProviderIds.Local)
        {
            return id;
        }

        var selectedReady = id switch
        {
            AssistantProviderIds.Gemini => hasGeminiKey,
            AssistantProviderIds.OpenAi => hasOpenAiKey,
            _ => false
        };
        if (selectedReady)
        {
            return id;
        }

        if (hasGeminiKey)
        {
            return AssistantProviderIds.Gemini;
        }

        if (hasOpenAiKey)
        {
            return AssistantProviderIds.OpenAi;
        }

        return id;
    }

    /// <summary>
    /// Returns settings whose provider and model match <see cref="ResolveActiveProviderId"/>.
    /// Does not mutate <paramref name="settings"/> when a switch is required.
    /// </summary>
    public static AssistantSettings ForRuntime(AssistantSettings settings, bool hasOpenAiKey, bool hasGeminiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var resolved = ResolveActiveProviderId(settings.ProviderId, hasOpenAiKey, hasGeminiKey);
        var model = ModelFor(resolved, settings);
        if (string.Equals(resolved, Normalize(settings.ProviderId), StringComparison.Ordinal)
            && string.Equals(model, settings.Model, StringComparison.Ordinal))
        {
            return settings;
        }

        return new AssistantSettings
        {
            SchemaVersion = settings.SchemaVersion,
            ProviderId = resolved,
            Model = model,
            MaxSteps = settings.MaxSteps,
            RequireConfirmationForActions = true,
            LocalBaseUrl = settings.LocalBaseUrl,
            LocalModel = settings.LocalModel
        };
    }

    public static string Normalize(string? providerId) =>
        string.IsNullOrWhiteSpace(providerId)
            ? AssistantProviderIds.OpenAi
            : providerId.Trim().ToLowerInvariant();

    private static string ModelFor(string providerId, AssistantSettings settings)
    {
        if (providerId == AssistantProviderIds.Local)
        {
            return string.IsNullOrWhiteSpace(settings.LocalModel)
                ? AssistantSettings.DefaultLocalModel
                : settings.LocalModel.Trim();
        }

        if (providerId == AssistantProviderIds.Gemini)
        {
            return NeedsGeminiDefault(settings.Model)
                ? AssistantSettings.DefaultGeminiModel
                : settings.Model.Trim();
        }

        if (providerId == AssistantProviderIds.OpenAi && IsGeminiModelName(settings.Model))
        {
            return AssistantSettings.DefaultOpenAiModel;
        }

        return string.IsNullOrWhiteSpace(settings.Model)
            ? AssistantSettings.DefaultOpenAiModel
            : settings.Model.Trim();
    }

    private static bool NeedsGeminiDefault(string? model) =>
        string.IsNullOrWhiteSpace(model)
        || model.Contains("gpt-", StringComparison.OrdinalIgnoreCase)
        || model.Contains("o1", StringComparison.OrdinalIgnoreCase)
        || model.Contains("o3", StringComparison.OrdinalIgnoreCase)
        || model.Contains("claude", StringComparison.OrdinalIgnoreCase)
        || model.Contains("llama", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeminiModelName(string? model) =>
        !string.IsNullOrWhiteSpace(model)
        && model.Contains("gemini", StringComparison.OrdinalIgnoreCase);
}
