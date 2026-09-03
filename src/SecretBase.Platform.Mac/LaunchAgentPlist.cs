using System.Security;
using System.Text;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Builds a per-user LaunchAgent plist. Does not talk to launchctl (the OS loads
/// ~/Library/LaunchAgents at Aqua login). No Dock, Finder, or system-wide launchd jobs.
/// </summary>
public static class LaunchAgentPlist
{
    public const string DefaultLabel = "com.secretbase.app";
    public const string AutoStartCommandLineSwitch = "--autostart";

    public static string FileNameForLabel(string label) => $"{label}.plist";

    public static string Build(string label, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var escapedLabel = SecurityElement.Escape(label.Trim()) ?? label;
        var escapedExe = SecurityElement.Escape(executablePath.Trim()) ?? executablePath;
        var escapedSwitch = SecurityElement.Escape(AutoStartCommandLineSwitch) ?? AutoStartCommandLineSwitch;

        var builder = new StringBuilder();
        builder.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        builder.AppendLine(@"<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">");
        builder.AppendLine("<plist version=\"1.0\">");
        builder.AppendLine("<dict>");
        builder.AppendLine("  <key>Label</key>");
        builder.AppendLine($"  <string>{escapedLabel}</string>");
        builder.AppendLine("  <key>ProgramArguments</key>");
        builder.AppendLine("  <array>");
        builder.AppendLine($"    <string>{escapedExe}</string>");
        builder.AppendLine($"    <string>{escapedSwitch}</string>");
        builder.AppendLine("  </array>");
        builder.AppendLine("  <key>RunAtLoad</key>");
        builder.AppendLine("  <true/>");
        builder.AppendLine("  <key>LimitLoadToSessionType</key>");
        builder.AppendLine("  <string>Aqua</string>");
        builder.AppendLine("</dict>");
        builder.AppendLine("</plist>");
        return builder.ToString();
    }

    public static bool TryReadExecutablePath(string plistXml, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(plistXml))
        {
            return false;
        }

        const string start = "<key>ProgramArguments</key>";
        var argsIndex = plistXml.IndexOf(start, StringComparison.Ordinal);
        if (argsIndex < 0)
        {
            return false;
        }

        var firstString = plistXml.IndexOf("<string>", argsIndex, StringComparison.Ordinal);
        if (firstString < 0)
        {
            return false;
        }

        var valueStart = firstString + "<string>".Length;
        var valueEnd = plistXml.IndexOf("</string>", valueStart, StringComparison.Ordinal);
        if (valueEnd < 0)
        {
            return false;
        }

        executablePath = System.Net.WebUtility.HtmlDecode(plistXml[valueStart..valueEnd].Trim());
        return !string.IsNullOrWhiteSpace(executablePath);
    }

    public static bool IsSupportedHostName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return fileName.Equals("SecretBase.App.Mac", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDotnetHost(string fileName) =>
        fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
}
