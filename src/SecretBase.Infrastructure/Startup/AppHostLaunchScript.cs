using System.Text;

namespace SecretBase.Infrastructure.Startup;

/// <summary>
/// How Explorer and the logon Run key start SecretBase.App.exe.
/// The apphost exits before any window exists when it cannot find the .NET runtime.
/// Explorer does not inherit a terminal's DOTNET_ROOT. Forcing DOTNET_ROOT at a
/// user-local folder that lacks the shared framework also hides a working machine install,
/// and the exe flashes closed. A hidden wscript host swallows that error.
/// </summary>
public static class AppHostLaunchScript
{
    public const string FileName = "launch-secretbase.cmd";
    public const string LegacyFileName = "launch-secretbase.vbs";
    public const string ExecutableMarkerPrefix = "rem SecretBase.App.exe=";
    public const string LegacyExecutableMarkerPrefix = "' SecretBase.App.exe=";
    public const string RequiredRuntimePrefix = "10.";

    static AppHostLaunchScript()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Shift-JIS (code page 932), no BOM. Japanese <c>cmd.exe</c> reads this encoding.
    /// A BOM or UTF-16 file looks corrupt in Notepad and can make <c>cmd</c> exit immediately.
    /// </summary>
    public static Encoding FileEncoding => Encoding.GetEncoding(932);

    /// <summary>
    /// User-local root to publish as DOTNET_ROOT, or null when the exe should start directly.
    /// </summary>
    public static string? SelectDotNetRoot(
        bool machineHasRequiredRuntime,
        bool userLocalHasRequiredRuntime,
        string? userLocalRoot)
    {
        if (machineHasRequiredRuntime || !userLocalHasRequiredRuntime || string.IsNullOrWhiteSpace(userLocalRoot))
        {
            return null;
        }

        return userLocalRoot.Trim().TrimEnd('\\', '/');
    }

    public static bool SharedFrameworkPresent(string? dotnetRoot, string versionPrefix)
    {
        if (string.IsNullOrWhiteSpace(dotnetRoot) || string.IsNullOrWhiteSpace(versionPrefix))
        {
            return false;
        }

        var shared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(shared))
        {
            return false;
        }

        foreach (var directory in Directory.EnumerateDirectories(shared))
        {
            var name = Path.GetFileName(directory);
            if (name.StartsWith(versionPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string? ResolveDotNetRootForExplorerLaunch()
    {
        var machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        var user = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "dotnet");
        return SelectDotNetRoot(
            SharedFrameworkPresent(machine, RequiredRuntimePrefix),
            SharedFrameworkPresent(user, RequiredRuntimePrefix),
            user);
    }

    public static string Build(string executablePath, string dotnetRoot)
    {
        var exe = executablePath.Trim();
        var workDir = DirectoryOf(exe);
        var root = dotnetRoot.Trim().TrimEnd('\\', '/');
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecretBase",
            "logs");

        // `start` returns immediately so the console does not stay open while the app runs.
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("setlocal EnableExtensions");
        script.Append(ExecutableMarkerPrefix).AppendLine(exe);
        script.Append("set \"SB_EXE=").Append(exe).AppendLine("\"");
        script.Append("set \"SB_DIR=").Append(workDir).AppendLine("\"");
        script.Append("set \"DOTNET_ROOT=").Append(root).AppendLine("\"");
        script.AppendLine("set \"DOTNET_ROOT(x64)=%DOTNET_ROOT%\"");
        script.AppendLine("set \"PATH=%DOTNET_ROOT%;%PATH%\"");
        script.Append("set \"SB_LOG=").Append(logDir).AppendLine("\"");
        script.AppendLine("if not exist \"%SB_LOG%\" mkdir \"%SB_LOG%\"");
        script.AppendLine("> \"%SB_LOG%\\launch-last.txt\" echo Secret Base launcher");
        script.AppendLine(">> \"%SB_LOG%\\launch-last.txt\" echo exe=%SB_EXE%");
        script.AppendLine(">> \"%SB_LOG%\\launch-last.txt\" echo DOTNET_ROOT=%DOTNET_ROOT%");
        script.AppendLine("if not exist \"%SB_EXE%\" (");
        script.AppendLine("  echo Secret Base executable was not found:");
        script.AppendLine("  echo %SB_EXE%");
        script.AppendLine("  pause");
        script.AppendLine("  exit /b 1");
        script.AppendLine(")");
        script.AppendLine("start \"\" /D \"%SB_DIR%\" \"%SB_EXE%\" %*");
        script.AppendLine("exit /b 0");
        return script.ToString();
    }

    public static void WriteFile(string launcherPath, string executablePath, string dotnetRoot)
    {
        if (string.IsNullOrWhiteSpace(dotnetRoot))
        {
            throw new ArgumentException("A command launcher is only written when DOTNET_ROOT must be set.", nameof(dotnetRoot));
        }

        var directory = Path.GetDirectoryName(launcherPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(launcherPath, Build(executablePath, dotnetRoot), FileEncoding);
    }

    public static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return FileEncoding.GetString(bytes);
    }

    public static string BuildDirectStartupCommand(string executablePath) =>
        $"\"{executablePath.Trim()}\" --autostart";

    public static string BuildScriptStartupCommand(string commandProcessorPath, string scriptPath)
    {
        var script = QuoteIfNeeded(scriptPath.Trim());
        return $"\"{commandProcessorPath.Trim()}\" /d /c {script} --autostart";
    }

    public static string BuildShortcutArguments(string scriptPath) =>
        $"/d /c {QuoteIfNeeded(scriptPath.Trim())}";

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
            string? value = null;
            if (line.StartsWith(ExecutableMarkerPrefix, StringComparison.Ordinal))
            {
                value = line[ExecutableMarkerPrefix.Length..];
            }
            else if (line.StartsWith(LegacyExecutableMarkerPrefix, StringComparison.Ordinal))
            {
                value = line[LegacyExecutableMarkerPrefix.Length..];
            }

            if (value is null)
            {
                continue;
            }

            executablePath = value.Trim();
            return executablePath.Length > 0;
        }

        return false;
    }

    public static bool TryExtractScriptPath(string? command, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (TryExtractPathEndingWith(command, FileName, out path))
        {
            return true;
        }

        return TryExtractPathEndingWith(command, LegacyFileName, out path);
    }

    private static bool TryExtractPathEndingWith(string command, string fileName, out string path)
    {
        path = string.Empty;
        var index = command.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var start = index;
        while (start > 0 && command[start - 1] != '"' && !char.IsWhiteSpace(command[start - 1]))
        {
            start--;
        }

        path = command[start..(index + fileName.Length)];
        return path.Length > 0;
    }

    private static string DirectoryOf(string path)
    {
        var trimmed = path.Trim().TrimEnd('\\', '/');
        var index = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return index > 0 ? trimmed[..index] : string.Empty;
    }

    private static string QuoteIfNeeded(string value) =>
        value.IndexOfAny([' ', '&', '(', ')', '^']) >= 0 ? "\"" + value + "\"" : value;
}
