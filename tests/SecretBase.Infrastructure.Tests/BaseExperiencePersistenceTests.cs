using SecretBase.Core.Base;
using SecretBase.Core.Desktop;
using SecretBase.Core.Todo;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class BaseSettingsPersistenceTests
{
    [Fact]
    public void MissingFile_WithExistingLayout_CompletesOnboarding()
    {
            var dir = TempDirs.CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "base.json");
            var store = new JsonBaseSettingsStore(path);
            var settings = store.LoadOrCreate(layoutAlreadyExisted: true);
            Assert.True(settings.OnboardingCompleted);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CorruptJson_DoesNotThrow()
    {
            var dir = TempDirs.CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "base.json");
            File.WriteAllText(path, "{ not-json");
            var store = new JsonBaseSettingsStore(path);
            var settings = store.LoadOrCreate(layoutAlreadyExisted: true);
            Assert.True(settings.OnboardingCompleted);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class TodoPersistenceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsItems()
    {
            var dir = TempDirs.CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "todos.json");
            var store = new JsonTodoStore(path);
            var list = store.LoadOrCreate();
            list.Items.Add(TodoItem.Create("Review cleanup candidates"));
            store.Save(list);

            var restored = new JsonTodoStore(path).LoadOrCreate();
            Assert.Single(restored.Items);
            Assert.Equal("Review cleanup candidates", restored.Items[0].Title);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CorruptJson_ReturnsEmptyList()
    {
            var dir = TempDirs.CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "todos.json");
            File.WriteAllText(path, "{ broken");
            var store = new JsonTodoStore(path);
            Assert.Empty(store.LoadOrCreate().Items);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class LayoutExistsTests
{
    [Fact]
    public void Exists_IsFalseUntilSaved()
    {
            var dir = TempDirs.CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            Assert.False(store.Exists(RoomId.DefaultRoomId));
            store.Save(DesktopLayout.CreateDefault());
            Assert.True(store.Exists(RoomId.DefaultRoomId));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

file static class TempDirs
{
    public static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
