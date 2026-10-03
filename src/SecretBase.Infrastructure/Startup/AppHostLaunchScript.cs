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

        // Chr(34) keeps the file readable. A pile of quotes looks like mojibake in Notepad.
        // Windows Script Host reads .vbs as ANSI unless the file is UTF-16, so Japanese
        // user/folder names must be written as Unicode or the path is corrupted.
        var script = new StringBuilder();
        script.AppendLine("Option Explicit");
        script.AppendLine(ExecutableMarkerPrefix + exe);
        script.AppendLine("Dim shell, env, exe, workDir, args, i, quote");
        script.AppendLine("Set shell = CreateObject(\"WScript.Shell\")");
        script.AppendLine("Set env = shell.Environment(\"Process\")");
        script.AppendLine("quote = Chr(34)");
        if (!string.IsNullOrEmpty(root))
        {
            script.Append("env(\"DOTNET_ROOT\") = ").AppendLine(Vb(root));
            script.AppendLine("env(\"DOTNET_ROOT(x64)\") = env(\"DOTNET_ROOT\")");
            script.AppendLine("env(\"PATH\") = env(\"DOTNET_ROOT\") & \";\" & env(\"PATH\")");
        }

        script.Append("exe = ").AppendLine(Vb(exe));
        script.Append("workDir = ").AppendLine(Vb(workDir));
        script.AppendLine("shell.CurrentDirectory = workDir");
        script.AppendLine("args = \"\"");
        script.AppendLine("For i = 0 To WScript.Arguments.Count - 1");
        script.AppendLine("  args = args & \" \" & quote & WScript.Arguments(i) & quote");
        script.AppendLine("Next");
        script.AppendLine("shell.Run quote & exe & quote & args, 1, False");
        return script.ToString();
    }

    /// <summary>UTF-16 LE with BOM. This is the encoding wscript accepts for non-ANSI paths.</summary>
    public static readonly Encoding FileEncoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

    public static void WriteFile(string launcherPath, string executablePath, string? dotnetRoot)
    {
        var directory = Path.GetDirectoryName(launcherPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(launcherPath, Build(executablePath, dotnetRoot), FileEncoding);
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
