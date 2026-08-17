using SecretBase.Core.Apps;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class CustomAppPersistenceTests
{
    [Fact]
    public void SaveAndLoad_AtomicWrite_MigratesSchema_DropsInvalid_KeepsMissingPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "apps.json");
        try
        {
            var store = new JsonCustomAppStore(path);
            var created = store.LoadOrCreate();
            Assert.Equal(CustomAppDocument.CurrentSchemaVersion, created.SchemaVersion);
            Assert.Empty(created.Apps);

            created.Apps.Add(new CustomApp
            {
                Name = "Pokemon Calculator",
                Description = "Damage calc",
                Type = CustomAppType.Application,
                LaunchTarget = @"C:\DoesNotExist\PokemonCalc.exe",
                ProjectRoot = @"D:\src\pokemon-calc"
            });
            created.Apps.Add(new CustomApp
            {
                Name = "",
                Type = CustomAppType.Application,
                LaunchTarget = @"C:\x.exe"
            });
            created.Apps.Add(new CustomApp
            {
                Name = "Bad args",
                Type = CustomAppType.Application,
                LaunchTarget = @"C:\x.exe --force"
            });
            store.Save(created);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));

            var restored = new JsonCustomAppStore(path).LoadOrCreate();
            Assert.Equal(CustomAppDocument.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Single(restored.Apps);
            Assert.Equal("Pokemon Calculator", restored.Apps[0].Name);
            Assert.Equal(@"C:\DoesNotExist\PokemonCalc.exe", restored.Apps[0].LaunchTarget);

            File.WriteAllText(path, """
                {
                  "schemaVersion": 0,
                  "apps": [
                    {
                      "id": "legacyapp",
                      "name": "UNO Party",
                      "type": "application",
                      "launchTarget": "C:\\Games\\Uno.exe"
                    }
                  ]
                }
                """);
            var migrated = new JsonCustomAppStore(path).LoadOrCreate();
            Assert.Equal(CustomAppDocument.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.Single(migrated.Apps);
            Assert.Equal("UNO Party", migrated.Apps[0].Name);

            File.WriteAllText(path, "{ not-json");
            var recovered = new JsonCustomAppStore(path).LoadOrCreate();
            Assert.Empty(recovered.Apps);
            Assert.Equal(CustomAppDocument.CurrentSchemaVersion, recovered.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
