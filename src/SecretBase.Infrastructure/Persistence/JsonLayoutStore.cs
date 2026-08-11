using System.Text.Json;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public interface ILayoutStore
{
    DesktopLayout LoadOrCreateDefault(RoomId roomId);
    void Save(DesktopLayout layout);
}

public sealed class JsonLayoutStore : ILayoutStore
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options;

    public JsonLayoutStore(string? layoutsDirectory = null, JsonSerializerOptions? options = null)
    {
        _directory = layoutsDirectory ?? AppDataPaths.LayoutsDirectory;
        _options = options ?? SecretBaseJson.CreateOptions();
        Directory.CreateDirectory(_directory);
    }

    public DesktopLayout LoadOrCreateDefault(RoomId roomId)
    {
        var path = GetPath(roomId);
        if (!File.Exists(path))
        {
            var created = DesktopLayout.CreateDefault();
            // Ensure room id matches request (future multi-room).
            if (!Equals(created.RoomId, roomId))
            {
                created = new DesktopLayout
                {
                    RoomId = roomId,
                    SchemaVersion = created.SchemaVersion,
                    Widgets = created.Widgets
                        .Select(w => new WidgetInstance
                        {
                            Id = w.Id,
                            Type = w.Type,
                            Position = new WidgetPosition(w.Position.X, w.Position.Y),
                            Size = new WidgetSize(w.Size.Width, w.Size.Height),
                            RoomId = roomId,
                            Configuration = new Dictionary<string, JsonElement>(w.Configuration, StringComparer.Ordinal)
                        })
                        .ToList()
                };
            }

            Save(created);
            return created;
        }

        var json = File.ReadAllText(path);
        var layout = JsonSerializer.Deserialize<DesktopLayout>(json, _options)
                     ?? DesktopLayout.CreateDefault();

        if (layout.SchemaVersion < 1)
        {
            layout.SchemaVersion = 1;
        }

        if (layout.Widgets.Count == 0)
        {
            layout.Widgets.Add(DefaultWidgetFactory.CreateDefaultClock(layout.RoomId));
        }

        foreach (var widget in layout.Widgets)
        {
            widget.Size.Clamp(minWidth: 120, minHeight: 80);
        }

        return layout;
    }

    public void Save(DesktopLayout layout)
    {
        Directory.CreateDirectory(_directory);
        var path = GetPath(layout.RoomId);
        var json = JsonSerializer.Serialize(layout, _options);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    private string GetPath(RoomId roomId) =>
        Path.Combine(_directory, $"{roomId.Value}.layout.json");
}
