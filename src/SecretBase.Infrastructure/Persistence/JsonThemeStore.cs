using System.Text.Json;
using SecretBase.Core.Themes;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public interface IThemeStore
{
    ThemeDefinition LoadOrCreateDefault(string themeId = "default");
    void Save(ThemeDefinition theme);
}

public sealed class JsonThemeStore : IThemeStore
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options;
    private readonly IAppLogger? _logger;

    public JsonThemeStore(
        string? themesDirectory = null,
        JsonSerializerOptions? options = null,
        IAppLogger? logger = null)
    {
        _directory = themesDirectory ?? AppDataPaths.ThemesDirectory;
        _options = options ?? SecretBaseJson.CreateOptions();
        _logger = logger;
        Directory.CreateDirectory(_directory);
    }

    public ThemeDefinition LoadOrCreateDefault(string themeId = "default")
    {
        var path = GetPath(themeId);
        if (!File.Exists(path))
        {
            return CreateAndSaveDefault(themeId);
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger?.Warn("theme", "Theme file was empty. Restoring default theme.");
                CorruptJsonFileRecovery.TryBackupCorruptFile(path, _logger, "theme");
                return CreateAndSaveDefault(themeId);
            }

            var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, _options);
            if (theme is null)
            {
                _logger?.Warn("theme", "Theme file could not be parsed. Restoring default theme.");
                CorruptJsonFileRecovery.TryBackupCorruptFile(path, _logger, "theme");
                return CreateAndSaveDefault(themeId);
            }

            theme.Id = themeId;
            return theme;
        }
        catch (JsonException ex)
        {
            _logger?.Warn("theme", "Theme file was invalid JSON. Restoring default theme.");
            _logger?.Error("theme", "Theme JSON parse failed.", ex);
            CorruptJsonFileRecovery.TryBackupCorruptFile(path, _logger, "theme");
            return CreateAndSaveDefault(themeId);
        }
    }

    public void Save(ThemeDefinition theme)
    {
        Directory.CreateDirectory(_directory);
        var path = GetPath(theme.Id);
        var json = JsonSerializer.Serialize(theme, _options);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    private ThemeDefinition CreateAndSaveDefault(string themeId)
    {
        var created = ThemeDefinition.CreateDefault();
        created.Id = themeId;
        _logger?.Info("theme", "Default theme restored.");
        Save(created);
        return created;
    }

    private string GetPath(string themeId) =>
        Path.Combine(_directory, $"{themeId}.theme.json");
}
