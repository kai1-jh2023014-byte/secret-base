using SecretBase.Core.Workspace;

namespace SecretBase.Core.Base;

/// <summary>
/// Personal Base state that is not layout/theme: onboarding, recents, last workspace.
/// </summary>
public sealed class BaseSettings
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public bool OnboardingCompleted { get; set; }

    public string Atmosphere { get; set; } = BaseAtmosphere.Calm;

    public List<string> SelectedModules { get; set; } = [];

    public List<string> RecentAppNames { get; set; } = [];

    public WorkspaceSession? LastWorkspace { get; set; }

    public DateTimeOffset? OnboardingCompletedAt { get; set; }
}

public static class BaseAtmosphere
{
    public const string Calm = "Calm";
    public const string Focus = "Focus";
    public const string Dark = "Dark";
    public const string Minimal = "Minimal";

    public static IReadOnlyList<string> All { get; } = [Calm, Focus, Dark, Minimal];

    public static string ToThemePreset(string? atmosphere) =>
        atmosphere?.Trim() switch
        {
            Focus => "Focus",
            Dark => "Dark",
            Minimal => "Minimal",
            _ => "Atelier"
        };
}

public static class BaseModules
{
    public const string Ai = "ai";
    public const string Clock = "clock";
    public const string Calendar = "calendar";
    public const string Music = "music";
    public const string Projects = "projects";
    public const string Todo = "todo";

    public static IReadOnlyList<string> All { get; } =
        [Ai, Clock, Calendar, Music, Projects, Todo];
}

public interface IBaseSettingsStore
{
    BaseSettings LoadOrCreate(bool layoutAlreadyExisted);

    void Save(BaseSettings settings);
}

public sealed class MemoryBaseSettingsStore : IBaseSettingsStore
{
    private BaseSettings _settings = new();

    public BaseSettings LoadOrCreate(bool layoutAlreadyExisted)
    {
        if (!_settings.OnboardingCompleted && layoutAlreadyExisted)
        {
            _settings.OnboardingCompleted = true;
        }

        return _settings;
    }

    public void Save(BaseSettings settings)
    {
        _settings = settings ?? new BaseSettings();
    }
}

public static class BaseSettingsMigrator
{
    public static BaseSettings MigrateToCurrent(BaseSettings? loaded, bool layoutAlreadyExisted)
    {
        var settings = loaded ?? new BaseSettings();
        settings.SelectedModules ??= [];
        settings.RecentAppNames ??= [];
        if (settings.Schema < 1)
        {
            settings.Schema = BaseSettings.SchemaVersion;
        }

        if (loaded is null && layoutAlreadyExisted)
        {
            settings.OnboardingCompleted = true;
        }

        if (string.IsNullOrWhiteSpace(settings.Atmosphere))
        {
            settings.Atmosphere = BaseAtmosphere.Calm;
        }

        return settings;
    }

    public static void RecordAppLaunch(BaseSettings settings, string name)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        settings.RecentAppNames ??= [];
        settings.RecentAppNames.RemoveAll(item =>
            string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        settings.RecentAppNames.Insert(0, name.Trim());
        if (settings.RecentAppNames.Count > 8)
        {
            settings.RecentAppNames.RemoveRange(8, settings.RecentAppNames.Count - 8);
        }
    }
}
