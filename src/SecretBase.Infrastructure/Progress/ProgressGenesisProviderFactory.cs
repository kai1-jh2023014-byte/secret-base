using SecretBase.Core.Progress;
using SecretBase.Core.Widgets.Progress;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Picks local JSON vs HTTP provider from widget configuration.
/// Assumed data source when no remote URL: AppData <c>settings/progress-genesis.json</c>.
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
}
