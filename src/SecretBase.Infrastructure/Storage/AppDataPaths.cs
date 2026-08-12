namespace SecretBase.Infrastructure.Storage;

/// <summary>
/// Resolves Secret Base local data roots for split JSON persistence.
/// </summary>
public static class AppDataPaths
{
    public static string RootDirectory
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(RootDirectory, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string ThemesDirectory => Ensure("themes");
    public static string LayoutsDirectory => Ensure("layouts");
    public static string SettingsDirectory => Ensure("settings");
    public static string IconsDirectory => Ensure("icons");
    public static string BlockItemsDirectory => Ensure("block-items");

    private static string Ensure(string relative)
    {
        var path = Path.Combine(RootDirectory, relative);
        Directory.CreateDirectory(path);
        return path;
    }
}
