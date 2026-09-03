namespace SecretBase.App.Mac;

internal static class StartupLaunchMode
{
    public const string AutoStartCommandLineSwitch = "--autostart";

    public static bool IsAutoStartLaunch =>
        Environment.GetCommandLineArgs().Any(static arg =>
            string.Equals(arg, AutoStartCommandLineSwitch, StringComparison.OrdinalIgnoreCase));
}
