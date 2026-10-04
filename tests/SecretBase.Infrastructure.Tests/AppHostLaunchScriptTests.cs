using SecretBase.Infrastructure.Startup;

namespace SecretBase.Infrastructure.Tests;

public class AppHostLaunchScriptTests
{
    [Fact]
    public void SelectDotNetRoot_UsesUserLocalOnlyWhenMachineLacksRuntime()
    {
        var root = AppHostLaunchScript.SelectDotNetRoot(
            machineHasRequiredRuntime: false,
            userLocalHasRequiredRuntime: true,
            userLocalRoot: @"C:\Users\me\AppData\Local\Microsoft\dotnet\");

        Assert.Equal(@"C:\Users\me\AppData\Local\Microsoft\dotnet", root);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SelectDotNetRoot_StartsExeDirectlyWhenUserLocalIsNotTheOnlyRuntime(
        bool machineHasRuntime,
        bool userLocalHasRuntime)
    {
        var root = AppHostLaunchScript.SelectDotNetRoot(
            machineHasRuntime,
            userLocalHasRuntime,
            @"C:\Users\me\AppData\Local\Microsoft\dotnet");

        Assert.Null(root);
    }

    [Fact]
    public void SharedFrameworkPresent_MatchesMajorVersionFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "secretbase-dotnet-" + Guid.NewGuid().ToString("N"));
        var match = Path.Combine(root, "shared", "Microsoft.NETCore.App", "10.0.2");
        var other = Path.Combine(root, "shared", "Microsoft.NETCore.App", "8.0.11");
        Directory.CreateDirectory(match);
        Directory.CreateDirectory(other);
        try
        {
            Assert.True(AppHostLaunchScript.SharedFrameworkPresent(root, "10."));
            Assert.False(AppHostLaunchScript.SharedFrameworkPresent(root, "9."));
            Assert.False(AppHostLaunchScript.SharedFrameworkPresent(Path.Combine(root, "missing"), "10."));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_RecordsExeAndSetsDotNetRoot()
    {
        var script = AppHostLaunchScript.Build(
            @"C:\Apps\Secret Base\SecretBase.App.exe",
            @"C:\Users\me\AppData\Local\Microsoft\dotnet");

        Assert.Contains(
            @"rem SecretBase.App.exe=C:\Apps\Secret Base\SecretBase.App.exe",
            script,
            StringComparison.Ordinal);
        Assert.Contains(@"set ""DOTNET_ROOT=C:\Users\me\AppData\Local\Microsoft\dotnet""", script, StringComparison.Ordinal);
        Assert.Contains("start \"\" /D \"%SB_DIR%\" \"%SB_EXE%\" %*", script, StringComparison.Ordinal);
        Assert.DoesNotContain("wscript", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("//B", script, StringComparison.Ordinal);
        Assert.True(AppHostLaunchScript.TryReadExecutable(script, out var exe));
        Assert.Equal(@"C:\Apps\Secret Base\SecretBase.App.exe", exe);
    }

    [Fact]
    public void TryReadExecutable_StillReadsLegacyVbsMarker()
    {
        const string script = "' SecretBase.App.exe=C:\\Apps\\SecretBase.App.exe\r\n";
        Assert.True(AppHostLaunchScript.TryReadExecutable(script, out var exe));
        Assert.Equal(@"C:\Apps\SecretBase.App.exe", exe);
    }

    [Fact]
    public void BuildDirectStartupCommand_LaunchesTheExe()
    {
        var command = AppHostLaunchScript.BuildDirectStartupCommand(@"C:\Apps\SecretBase.App.exe");
        Assert.Equal("\"C:\\Apps\\SecretBase.App.exe\" --autostart", command);
        Assert.DoesNotContain("wscript", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("//B", command, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScriptStartupCommand_UsesCmdAndForwardsAutostart()
    {
        var command = AppHostLaunchScript.BuildScriptStartupCommand(
            @"C:\Windows\System32\cmd.exe",
            @"C:\Users\me\AppData\Local\SecretBase\launch-secretbase.cmd");

        Assert.Equal(
            "\"C:\\Windows\\System32\\cmd.exe\" /d /c C:\\Users\\me\\AppData\\Local\\SecretBase\\launch-secretbase.cmd --autostart",
            command);
        Assert.True(AppHostLaunchScript.TryExtractScriptPath(command, out var path));
        Assert.Equal(@"C:\Users\me\AppData\Local\SecretBase\launch-secretbase.cmd", path);
    }

    [Fact]
    public void TryExtractScriptPath_ReadsQuotedLegacyVbs()
    {
        const string command =
            "\"C:\\Windows\\System32\\wscript.exe\" //B //Nologo \"C:\\Users\\me\\AppData\\Local\\SecretBase\\launch-secretbase.vbs\" --autostart";
        Assert.True(AppHostLaunchScript.TryExtractScriptPath(command, out var path));
        Assert.Equal(@"C:\Users\me\AppData\Local\SecretBase\launch-secretbase.vbs", path);
    }

    [Fact]
    public void WriteFile_UsesShiftJisWithoutBom()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secretbase-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, AppHostLaunchScript.FileName);
        var exe = "C:\\Users\\太郎\\SecretBase\\SecretBase.App.exe";
        try
        {
            AppHostLaunchScript.WriteFile(path, exe, "C:\\Users\\太郎\\AppData\\Local\\Microsoft\\dotnet");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 2);
            Assert.Equal((byte)'@', bytes[0]);
            Assert.Equal((byte)'e', bytes[1]);
            Assert.NotEqual(0xFF, bytes[0]);
            var text = AppHostLaunchScript.ReadText(path);
            Assert.Contains("太郎", text, StringComparison.Ordinal);
            Assert.Contains("@echo off", text, StringComparison.Ordinal);
            Assert.True(AppHostLaunchScript.TryReadExecutable(text, out var read));
            Assert.Equal(exe, read);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
