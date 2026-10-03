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
        Assert.Contains("workDir = \"C:\\Apps\\Secret Base\"", script, StringComparison.Ordinal);
        Assert.Contains("shell.CurrentDirectory = workDir", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"\"\"\"", script, StringComparison.Ordinal);
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

    [Fact]
    public void WriteFile_UsesShiftJisSoNotepadShowsJapanese()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secretbase-vbs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, AppHostLaunchScript.FileName);
        var exe = "C:\\Users\\太郎\\SecretBase\\SecretBase.App.exe";
        try
        {
            AppHostLaunchScript.WriteFile(path, exe, "C:\\Users\\太郎\\AppData\\Local\\Microsoft\\dotnet");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 2);
            Assert.Equal((byte)'O', bytes[0]);
            Assert.Equal((byte)'p', bytes[1]);
            var text = AppHostLaunchScript.ReadText(path);
            Assert.Contains("太郎", text, StringComparison.Ordinal);
            Assert.Contains("Option Explicit", text, StringComparison.Ordinal);
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
