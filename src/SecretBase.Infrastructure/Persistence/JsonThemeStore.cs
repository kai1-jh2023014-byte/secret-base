using System.Text.Json;
using SecretBase.Core.Themes;
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

    public JsonThemeStore(string? themesDirectory = null, JsonSerializerOptions? options = null)
    {
        _directory = themesDirectory ?? AppDataPaths.ThemesDirectory;
        _options = options ?? SecretBaseJson.CreateOptions();
        Directory.CreateDirectory(_directory);
    }

    public ThemeDefinition LoadOrCreateDefault(string themeId = "default")
    {
        var path = GetPath(themeId);
        if (!File.Exists(path))
        {
            var created = ThemeDefinition.CreateDefault();
            created.Id = themeId;
            Save(created);
            return created;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ThemeDefinition>(json, _options)
               ?? ThemeDefinition.CreateDefault();
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

    private string GetPath(string themeId) =>
        Path.Combine(_directory, $"{themeId}.theme.json");
}
