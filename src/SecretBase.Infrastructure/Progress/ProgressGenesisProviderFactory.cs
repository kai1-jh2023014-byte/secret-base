using SecretBase.Core.Progress;
using SecretBase.Core.Widgets.Progress;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Picks personal Progress+Genesis (default), Agent Arena, local JSON, or custom HTTPS.
/// </summary>
public static class ProgressGenesisProviderFactory
{
    public static IProgressGenesisProvider Create(
        ProgressWidgetConfiguration? configuration = null,
        IProgressGenesisStore? store = null,
        HttpClient? httpClient = null)
    {
        var config = configuration ?? ProgressWidgetConfiguration.CreateDefault();
        var localStore = store ?? new JsonProgressGenesisStore();
        var local = new LocalJsonProgressGenesisProvider(localStore);
        var source = ProgressGenesisSources.Normalize(config.Source);

        if (ProgressGenesisSources.IsLocal(source))
        {
            return local;
        }

        if (ProgressGenesisSources.IsHttp(source))
        {
            if (string.IsNullOrWhiteSpace(config.RemoteUrl))
            {
                return local;
            }

            try
            {
                return new HttpProgressGenesisProvider(config.RemoteUrl, httpClient, local);
            }
            catch (ArgumentException)
            {
                return local;
            }
        }

        if (ProgressGenesisSources.IsAgentArena(source))
        {
            try
            {
                return new AgentArenaProgressGenesisProvider(
                    apiBase: config.ArenaApiBase,
                    walletAddress: config.WalletAddress,
                    httpClient: httpClient,
                    fallback: local);
            }
            catch (ArgumentException)
            {
                return local;
            }
        }

        try
        {
            return new PersonalProgressGenesisProvider(
                progressApiBase: config.ProgressApiBase,
                genesisStatusUrl: config.GenesisStatusUrl,
                genesisRoot: config.GenesisRoot,
                httpClient: httpClient,
                fallback: local);
        }
        catch (ArgumentException)
        {
            return local;
        }
    }
}
