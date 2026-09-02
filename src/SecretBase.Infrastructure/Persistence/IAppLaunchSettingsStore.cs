using SecretBase.Core;

namespace SecretBase.Infrastructure.Persistence;

public interface IAppLaunchSettingsStore
{
    AppLaunchSettings LoadOrCreate();
    void Save(AppLaunchSettings settings);
}
