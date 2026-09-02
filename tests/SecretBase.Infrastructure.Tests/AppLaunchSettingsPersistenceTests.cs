using SecretBase.Core;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class AppLaunchSettingsPersistenceTests
{
    [Fact]
    public void SaveAndLoad_PreservesLaunchAtWindowsLogin()
    {
        var path = Path.Combine(CreateTempDir(), "launch.json");
        try
        {
            var store = new JsonAppLaunchSettingsStore(path);
            var settings = store.LoadOrCreate();
            settings.LaunchAtWindowsLogin = true;
            store.Save(settings);

            var restored = store.LoadOrCreate();
            Assert.True(restored.LaunchAtWindowsLogin);
            Assert.Equal(AppLaunchSettings.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
