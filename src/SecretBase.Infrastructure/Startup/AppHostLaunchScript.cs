using System.Text;

namespace SecretBase.Infrastructure.Startup;

/// <summary>
/// Hidden launcher script so Explorer shortcuts and login auto-start can find a
/// user-local .NET install. Double-clicking SecretBase.App.exe directly does not
/// inherit DOTNET_ROOT, so the apphost flashes and exits.
/// </summary>
public static class AppHostLaunchScript
{
    public const string FileName = "launch-secretbase.vbs";
    public const string ExecutableMarkerPrefix = "' SecretBase.App.exe=";

    public static string Build(string executablePath, string? dotnetRoot)
    {
        var exe = executablePath.Trim();
        var workDir = DirectoryOf(exe);
        var root = string.IsNullOrWhiteSpace(dotnetRoot)
            ? string.Empty
            : dotnetRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var script = new StringBuilder();
        script.AppendLine("Option Explicit");
        script.AppendLine(ExecutableMarkerPrefix + exe);
        script.AppendLine("Dim shell, env, cmd, i");
        script.AppendLine("Set shell = CreateObject(\"WScript.Shell\")");
        script.AppendLine("Set env = shell.Environment(\"Process\")");
        if (!string.IsNullOrEmpty(root))
        {
            script.Append("env(\"DOTNET_ROOT\") = ").AppendLine(Vb(root));
            script.AppendLine("env(\"DOTNET_ROOT(x64)\") = env(\"DOTNET_ROOT\")");
            script.AppendLine("env(\"PATH\") = env(\"DOTNET_ROOT\") & \";\" & env(\"PATH\")");
        }

        script.Append("shell.CurrentDirectory = ").AppendLine(Vb(workDir));
        script.Append("cmd = \"\"\"\" & ").Append(Vb(exe)).AppendLine(" & \"\"\"\"");
        script.AppendLine("For i = 0 To WScript.Arguments.Count - 1");
        script.AppendLine("  cmd = cmd & \" \"\"\" & Replace(WScript.Arguments(i), \"\"\"\", \"\"\"\"\"\") & \"\"\"\"");
        script.AppendLine("Next");
        script.AppendLine("shell.Run cmd, 1, False");
        return script.ToString();
    }

    public static string BuildStartupCommand(string wscriptPath, string launcherScriptPath) =>
        $"\"{wscriptPath}\" //B //Nologo \"{launcherScriptPath}\" --autostart";

    public static bool TryReadExecutable(string? script, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(script))
        {
            return false;
        }

        foreach (var raw in script.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (!line.StartsWith(ExecutableMarkerPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            executablePath = line[ExecutableMarkerPrefix.Length..].Trim();
            return executablePath.Length > 0;
        }

        return false;
    }

    public static string? FindUserDotNetRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (IsDotNetRoot(fromEnv))
        {
            return Path.GetFullPath(fromEnv!);
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "dotnet");
        return IsDotNetRoot(local) ? Path.GetFullPath(local) : null;
    }

    private static bool IsDotNetRoot(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "dotnet.exe"));

    private static string DirectoryOf(string path)
    {
        var trimmed = path.Trim().TrimEnd('\\', '/');
        var index = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return index > 0 ? trimmed[..index] : string.Empty;
    }

    private static string Vb(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
