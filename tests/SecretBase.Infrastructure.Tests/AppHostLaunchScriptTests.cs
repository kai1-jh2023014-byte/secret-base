using SecretBase.Infrastructure.Startup;

namespace SecretBase.Infrastructure.Tests;

public class AppHostLaunchScriptTests
{
    [Fact]
    public void Build_RecordsExeAndUserDotNetRoot()
    {
        var script = AppHostLaunchScript.Build(
            @"C:\Apps\Secret Base\SecretBase.App.exe",
            @"C:\Users\me\AppData\Local\Microsoft\dotnet");

        Assert.Contains(
            @"' SecretBase.App.exe=C:\Apps\Secret Base\SecretBase.App.exe",
            script,
            StringComparison.Ordinal);
        Assert.Contains(@"env(""DOTNET_ROOT"")", script, StringComparison.Ordinal);
        Assert.Contains(@"Microsoft\dotnet", script, StringComparison.Ordinal);
        Assert.Contains(
            @"shell.CurrentDirectory = ""C:\Apps\Secret Base""",
            script,
            StringComparison.Ordinal);
        Assert.True(AppHostLaunchScript.TryReadExecutable(script, out var exe));
        Assert.Equal(@"C:\Apps\Secret Base\SecretBase.App.exe", exe);
    }

    [Fact]
    public void Build_OmitsDotNetRootWhenMissing()
    {
        var script = AppHostLaunchScript.Build(@"C:\Apps\SecretBase.App.exe", dotnetRoot: null);
        Assert.DoesNotContain("DOTNET_ROOT", script, StringComparison.Ordinal);
        Assert.True(AppHostLaunchScript.TryReadExecutable(script, out var exe));
        Assert.Equal(@"C:\Apps\SecretBase.App.exe", exe);
    }

    [Fact]
    public void BuildStartupCommand_UsesHiddenWscriptAndForwardsAutostart()
    {
        var command = AppHostLaunchScript.BuildStartupCommand(
            @"C:\Windows\System32\wscript.exe",
            @"C:\Users\me\AppData\Local\SecretBase\launch-secretbase.vbs");

        Assert.Equal(
            "\"C:\\Windows\\System32\\wscript.exe\" //B //Nologo \"C:\\Users\\me\\AppData\\Local\\SecretBase\\launch-secretbase.vbs\" --autostart",
            command);
    }
}
