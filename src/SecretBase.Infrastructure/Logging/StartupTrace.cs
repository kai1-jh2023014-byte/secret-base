using System.Text;

namespace SecretBase.Infrastructure.Logging;

/// <summary>
/// One-file startup record written as early as managed code runs.
/// UTF-8 with BOM so Notepad on Japanese Windows shows it as text.
/// If this file is missing after a launch, the process died in the apphost
/// before <c>App</c> ran (runtime not found), not inside Secret Base.
/// </summary>
public static class StartupTrace
{
    public const string FileName = "startup-last.txt";
    public const string AttemptFileName = "launch-attempt.txt";
    public const string VisibleStatus = "window-visible";

    public static void Write(string logsDirectory, string status, string? detail = null) =>
        WriteFile(logsDirectory, FileName, status, detail);

    public static void WriteAttempt(string logsDirectory) =>
        WriteFile(logsDirectory, AttemptFileName, "process-start", null);

    private static void WriteFile(string logsDirectory, string fileName, string status, string? detail)
    {
        Directory.CreateDirectory(logsDirectory);
        var path = Path.Combine(logsDirectory, fileName);
        var builder = new StringBuilder();
        builder.AppendLine("Secret Base startup");
        builder.Append("utc=").AppendLine(DateTime.UtcNow.ToString("O"));
        builder.Append("status=").AppendLine(status);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            builder.AppendLine(detail.Trim());
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var temp = path + ".tmp";
        File.WriteAllText(temp, builder.ToString(), encoding);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    public static string? ReadStatus(string logsDirectory)
    {
        var path = Path.Combine(logsDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            const string prefix = "status=";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }
}
