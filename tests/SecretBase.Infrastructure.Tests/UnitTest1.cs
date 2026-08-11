using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Tests;

public class AppDataPathsTests
{
    [Fact]
    public void RootDirectory_LivesUnderLocalAppData_SecretBase()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecretBase");

        Assert.Equal(expected, AppDataPaths.RootDirectory);
        Assert.True(Directory.Exists(AppDataPaths.LogsDirectory));
        Assert.True(Directory.Exists(AppDataPaths.LayoutsDirectory));
        Assert.True(Directory.Exists(AppDataPaths.ThemesDirectory));
        Assert.True(Directory.Exists(AppDataPaths.SettingsDirectory));
    }
}
