using SecretBase.Core;

namespace SecretBase.Core.Tests;

public class AppLaunchSettingsTests
{
    [Fact]
    public void Default_EnablesTaskbarAiChat_WithoutAutostart()
    {
        var settings = new AppLaunchSettings();
        Assert.Equal(2, settings.SchemaVersion);
        Assert.False(settings.LaunchAtWindowsLogin);
        Assert.True(settings.TaskbarAiChatEnabled);
    }
}
