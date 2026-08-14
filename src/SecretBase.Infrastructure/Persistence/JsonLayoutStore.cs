using System.Text.Json;
using SecretBase.Core.Blocks;
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
                created = CloneForRoom(created, roomId);
            }

            Save(created);
            return created;
        }

        var json = File.ReadAllText(path);
        var layout = JsonSerializer.Deserialize<DesktopLayout>(json, _options)
                     ?? DesktopLayout.CreateDefault();

        NormalizeLayout(layout);
        return layout;
    }

    public void Save(DesktopLayout layout)
    {
        NormalizeLayout(layout);
        Directory.CreateDirectory(_directory);
        var path = GetPath(layout.RoomId);
        var json = JsonSerializer.Serialize(layout, _options);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    private static void NormalizeLayout(DesktopLayout layout)
    {
        if (layout.SchemaVersion < 1)
        {
            layout.SchemaVersion = 1;
        }

        layout.Blocks ??= [];

        // V1 → V2: add Blocks collection without touching existing widgets.
        if (layout.SchemaVersion < DesktopLayout.CurrentSchemaVersion)
        {
            layout.SchemaVersion = DesktopLayout.CurrentSchemaVersion;
        }

        // Safety net: never leave a blank overlay with zero widgets after load.
        // Users can still remove widgets during a session; next launch restores a Clock.
        if (layout.Widgets.Count == 0)
        {
            layout.Widgets.Add(DefaultWidgetFactory.CreateDefaultClock(layout.RoomId));
        }

        foreach (var widget in layout.Widgets)
        {
            widget.Size.Clamp(minWidth: 120, minHeight: 80);
        }

        foreach (var block in layout.Blocks)
        {
            block.RoomId = layout.RoomId;
            block.Items ??= [];
            if (string.IsNullOrWhiteSpace(block.Name))
            {
                block.Name = "New Block";
            }

            block.ClampSize();
            for (var i = 0; i < block.Items.Count; i++)
            {
                var item = block.Items[i];
                if (item.Id == Guid.Empty)
                {
                    item.Id = Guid.NewGuid();
                }

                item.Name ??= string.Empty;
                item.Target ??= string.Empty;
                item.Icon ??= string.Empty;
                item.EnsurePlacement(i);
            }
        }
    }

    private static DesktopLayout CloneForRoom(DesktopLayout source, RoomId roomId)
    {
        return new DesktopLayout
        {
            RoomId = roomId,
            SchemaVersion = source.SchemaVersion,
            Widgets = source.Widgets
                .Select(w => new WidgetInstance
                {
                    Id = w.Id,
                    Type = w.Type,
                    Position = new WidgetPosition(w.Position.X, w.Position.Y),
                    Size = new WidgetSize(w.Size.Width, w.Size.Height),
                    RoomId = roomId,
                    Configuration = new Dictionary<string, JsonElement>(w.Configuration, StringComparer.Ordinal)
                })
                .ToList(),
            Blocks = source.Blocks
                .Select(b => new Block
                {
                    Id = b.Id,
                    Name = b.Name,
                    Position = new WidgetPosition(b.Position.X, b.Position.Y),
                    Size = new WidgetSize(b.Size.Width, b.Size.Height),
                    Theme = b.Theme,
                    RoomId = roomId,
                    Items = b.Items
                        .Select(i => new BlockItem
                        {
                            Id = i.Id,
                            Name = i.Name,
                            Type = i.Type,
                            Target = i.Target,
                            Icon = i.Icon
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private string GetPath(RoomId roomId) =>
        Path.Combine(_directory, $"{roomId.Value}.layout.json");
}
