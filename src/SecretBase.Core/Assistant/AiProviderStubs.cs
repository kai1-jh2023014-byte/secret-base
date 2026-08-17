namespace SecretBase.Core.Assistant;

/// <summary>Resolves an IAiProvider from settings. HTTP/SDK stay in Infrastructure.</summary>
public interface IAiProviderFactory
{
    IAiProvider Create(AssistantSettings settings);
}

/// <summary>In-memory / test provider that returns scripted replies. No network.</summary>
public sealed class ScriptedAiProvider : IAiProvider
{
    private readonly Queue<AiProviderResponse> _script;

    public ScriptedAiProvider(IEnumerable<AiProviderResponse> script)
    {
        _script = new Queue<AiProviderResponse>(script);
        ProviderId = AssistantProviderIds.OpenAi;
        DisplayName = "Scripted";
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default)
    {
        _ = messages;
        _ = tools;
        _ = model;
        cancellationToken.ThrowIfCancellationRequested();
        if (_script.Count == 0)
        {
            return Task.FromResult(AiProviderResponse.Fail("Scripted provider has no more replies."));
        }

        return Task.FromResult(_script.Dequeue());
    }
}

public sealed class UnconfiguredAiProvider : IAiProvider
{
    public string ProviderId { get; }

    public string DisplayName { get; }

    public UnconfiguredAiProvider(string providerId, string displayName)
    {
        ProviderId = providerId;
        DisplayName = displayName;
    }

    public Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiProviderResponse.NotConfigured());
}

public sealed class UnavailableAiProvider : IAiProvider
{
    private readonly string _detail;

    public UnavailableAiProvider(string providerId, string displayName, string detail)
    {
        ProviderId = providerId;
        DisplayName = displayName;
        _detail = detail;
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiProviderResponse.Unavailable(_detail));
}
