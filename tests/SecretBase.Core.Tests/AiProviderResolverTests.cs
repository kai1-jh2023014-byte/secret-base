using SecretBase.Core.Assistant;

namespace SecretBase.Core.Tests;

public class AiProviderResolverTests
{
    [Fact]
    public async Task FactoryWrappedProvider_FallsBackToLocal_WhenOpenAiNotConfigured()
    {
        var factory = new StubProviderFactory();
        var resolver = new AiProviderResolver(factory);
        var provider = resolver.Resolve(new AssistantSettings { ProviderId = AssistantProviderIds.OpenAi }).Provider;
        var response = await provider.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            AssistantSettings.DefaultOpenAiModel);
        Assert.Equal(AiProviderStatus.Ok, response.Status);
        Assert.Equal("local-ok", response.Content);
    }

    [Fact]
    public async Task FactoryWrappedProvider_ReturnsNotConfigured_WhenLocalAlsoUnavailable()
    {
        var factory = new StubProviderFactory(localAvailable: false);
        var resolver = new AiProviderResolver(factory);
        var provider = resolver.Resolve(new AssistantSettings { ProviderId = AssistantProviderIds.Gemini }).Provider;
        var response = await provider.ChatAsync(
            [new AiMessage { Role = AiMessageRole.User, Content = "hi" }],
            [],
            "gemini");
        Assert.Equal(AiProviderStatus.NotConfigured, response.Status);
    }

    [Fact]
    public void Resolve_LocalPreferred_DoesNotWrapFallback()
    {
        var factory = new StubProviderFactory();
        var resolution = new AiProviderResolver(factory).Resolve(new AssistantSettings
        {
            ProviderId = AssistantProviderIds.Local
        });
        Assert.Equal(AssistantProviderIds.Local, resolution.ActiveProviderId);
        Assert.Equal("Local AI", resolution.ActiveDisplayName);
    }

    private sealed class StubProviderFactory : IAiProviderFactory
    {
        private readonly bool _localAvailable;

        public StubProviderFactory(bool localAvailable = true) => _localAvailable = localAvailable;

        public IAiProvider Create(AssistantSettings settings) =>
            new AiProviderResolver(this).Resolve(settings).Provider;

        public IAiProvider CreateForProviderId(string providerId, AssistantSettings settings) =>
            providerId switch
            {
                AssistantProviderIds.Local => _localAvailable
                    ? new LocalOkProvider()
                    : new UnavailableAiProvider(AssistantProviderIds.Local, "Local AI", "down"),
                AssistantProviderIds.Gemini => new UnconfiguredAiProvider(AssistantProviderIds.Gemini, "Gemini"),
                _ => new UnconfiguredAiProvider(AssistantProviderIds.OpenAi, "OpenAI")
            };

        public Task<bool> ProbeAvailabilityAsync(string providerId, AssistantSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(providerId == AssistantProviderIds.Local && _localAvailable);
    }

    private sealed class LocalOkProvider : IAiProvider
    {
        public string ProviderId => AssistantProviderIds.Local;

        public string DisplayName => "Local AI";

        public Task<AiProviderResponse> ChatAsync(
            IReadOnlyList<AiMessage> messages,
            IReadOnlyList<AssistantToolDefinition> tools,
            string model,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AiProviderResponse.Text("local-ok"));
    }
}
