using System.Text.Json;
using SecretBase.Core.Blocks;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Music;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Text;
using SecretBase.Core.Widgets.Web;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class LayoutPersistenceTests
{
    [Fact]
    public void SaveAndLoad_RestoresClockPositionSizeAndConfiguration()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var clock = layout.Widgets.Single(w => w.Type == WidgetTypes.Clock);
            clock.Position.X = 120;
            clock.Position.Y = 80;
            clock.Size.Width = 320;
            clock.Size.Height = 180;
            clock.Configuration = new ClockWidgetConfiguration
            {
                Use24HourFormat = true,
                ShowSeconds = true,
                ShowDate = true
            }.ToDictionary();

            store.Save(layout);
            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Equal(2, restored.Widgets.Count);
            var widget = restored.Widgets.Single(w => w.Type == WidgetTypes.Clock);
            Assert.Equal(120, widget.Position.X);
            Assert.Equal(80, widget.Position.Y);
            Assert.Equal(320, widget.Size.Width);
            Assert.Equal(180, widget.Size.Height);
            Assert.Equal(clock.Id, widget.Id);

            var config = ClockWidgetConfiguration.FromDictionary(widget.Configuration);
            Assert.True(config.Use24HourFormat);
            Assert.True(config.ShowSeconds);
            Assert.True(config.ShowDate);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RestoresTextPositionSizeAndConfiguration()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var text = layout.Widgets.Single(w => w.Type == WidgetTypes.Text);
            text.Position.X = 200;
            text.Position.Y = 300;
            text.Size.Width = 360;
            text.Size.Height = 220;
            text.Configuration = new TextWidgetConfiguration
            {
                Text = "Today: finish Text Widget",
                FontSize = 20,
                TextAlignment = TextWidgetAlignment.Right
            }.ToDictionary();

            store.Save(layout);
            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
            var widget = restored.Widgets.Single(w => w.Type == WidgetTypes.Text);
            Assert.Equal(text.Id, widget.Id);
            Assert.Equal(WidgetTypes.Text, widget.Type);
            Assert.Equal(200, widget.Position.X);
            Assert.Equal(300, widget.Position.Y);
            Assert.Equal(360, widget.Size.Width);
            Assert.Equal(220, widget.Size.Height);
            Assert.Equal(RoomId.DefaultRoomId, widget.RoomId);

            var config = TextWidgetConfiguration.FromDictionary(widget.Configuration);
            Assert.Equal("Today: finish Text Widget", config.Text);
            Assert.Equal(20, config.FontSize);
            Assert.Equal(TextWidgetAlignment.Right, config.TextAlignment);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RestoresBlocksItemsAndGeometry()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var block = DefaultBlockFactory.Create("DEVELOPMENT", x: 420, y: 60, width: 380, height: 280);
            block.Items.Add(new BlockItem
            {
                Name = "Cursor",
                Type = BlockItemType.Application,
                Target = @"C:\Tools\Cursor\Cursor.exe",
                X = 12,
                Y = 20
            });
            block.Items.Add(new BlockItem
            {
                Name = "Repo",
                Type = BlockItemType.Folder,
                Target = @"C:\Repos\secret-base",
                X = 110,
                Y = 20
            });
            layout.Blocks.Add(block);

            store.Save(layout);
            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Single(restored.Blocks);
            var loaded = restored.Blocks[0];
            Assert.Equal(block.Id, loaded.Id);
            Assert.Equal("DEVELOPMENT", loaded.Name);
            Assert.Equal(420, loaded.Position.X);
            Assert.Equal(60, loaded.Position.Y);
            Assert.Equal(380, loaded.Size.Width);
            Assert.Equal(280, loaded.Size.Height);
            Assert.Equal(2, loaded.Items.Count);
            Assert.Equal(BlockItemType.Application, loaded.Items[0].Type);
            Assert.Equal(@"C:\Tools\Cursor\Cursor.exe", loaded.Items[0].Target);
            Assert.Equal(12, loaded.Items[0].X);
            Assert.Equal(20, loaded.Items[0].Y);
            Assert.Equal(BlockItemType.Folder, loaded.Items[1].Type);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_EmptyBlock_RoundTrips()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            layout.Blocks.Add(DefaultBlockFactory.Create("EMPTY"));
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Single(restored.Blocks);
            Assert.Empty(restored.Blocks[0].Items);
            Assert.Equal("EMPTY", restored.Blocks[0].Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_MultipleBlocks_PreservesOrderAndIds()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var first = DefaultBlockFactory.Create("A");
            var second = DefaultBlockFactory.Create("B", x: 500, y: 200);
            layout.Blocks.Add(first);
            layout.Blocks.Add(second);
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(2, restored.Blocks.Count);
            Assert.Equal(first.Id, restored.Blocks[0].Id);
            Assert.Equal(second.Id, restored.Blocks[1].Id);
            Assert.Equal("A", restored.Blocks[0].Name);
            Assert.Equal("B", restored.Blocks[1].Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadMissing_CreatesDefaultClockAndTextLayout()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(2, layout.Widgets.Count);
            Assert.Empty(layout.Blocks);
            Assert.Equal(DesktopLayout.CurrentSchemaVersion, layout.SchemaVersion);
            Assert.Contains(layout.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.Contains(layout.Widgets, w => w.Type == WidgetTypes.Text);
            Assert.True(File.Exists(Path.Combine(dir, "default.layout.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadClockOnlyLayout_DoesNotDropExistingClock()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var clockOnly = new DesktopLayout
            {
                RoomId = RoomId.DefaultRoomId,
                SchemaVersion = 1,
                Widgets = [DefaultWidgetFactory.CreateDefaultClock()],
                Blocks = []
            };
            store.Save(clockOnly);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Single(restored.Widgets);
            Assert.Equal(WidgetTypes.Clock, restored.Widgets[0].Type);
            Assert.Equal(clockOnly.Widgets[0].Id, restored.Widgets[0].Id);
            Assert.Empty(restored.Blocks);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadLegacySchemaV1Json_WithoutBlocks_PreservesWidgets()
    {
        var dir = CreateTempDir();
        try
        {
            var clock = DefaultWidgetFactory.CreateDefaultClock();
            var text = DefaultWidgetFactory.CreateDefaultText();
            // Hand-written v1 payload (no blocks property) — must remain loadable.
            var legacy = $$"""
                {
                  "roomId": "default",
                  "schemaVersion": 1,
                  "widgets": [
                    {
                      "id": "{{clock.Id}}",
                      "type": "clock",
                      "position": { "x": 48, "y": 48 },
                      "size": { "width": 280, "height": 160 },
                      "roomId": "default",
                      "configuration": {}
                    },
                    {
                      "id": "{{text.Id}}",
                      "type": "text",
                      "position": { "x": 48, "y": 240 },
                      "size": { "width": 320, "height": 180 },
                      "roomId": "default",
                      "configuration": {}
                    }
                  ]
                }
                """;
            File.WriteAllText(Path.Combine(dir, "default.layout.json"), legacy);

            var store = new JsonLayoutStore(dir);
            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(2, restored.Widgets.Count);
            Assert.Equal(clock.Id, restored.Widgets[0].Id);
            Assert.Equal(text.Id, restored.Widgets[1].Id);
            Assert.Empty(restored.Blocks);
            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RestoresWebWidgetUrlAndGeometry()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var web = DefaultWidgetFactory.CreateWeb(
                "https://www.youtube.com/",
                layout.RoomId,
                x: 140,
                y: 90,
                width: 640,
                height: 400);
            layout.Widgets.Add(web);
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(3, restored.Widgets.Count);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Text);

            var widget = restored.Widgets.Single(w => w.Type == WidgetTypes.Web);
            Assert.Equal(web.Id, widget.Id);
            Assert.Equal(140, widget.Position.X);
            Assert.Equal(90, widget.Position.Y);
            Assert.Equal(640, widget.Size.Width);
            Assert.Equal(400, widget.Size.Height);
            Assert.Equal(RoomId.DefaultRoomId, widget.RoomId);

            var config = WebWidgetConfiguration.FromDictionary(widget.Configuration);
            Assert.Equal("https://www.youtube.com/", config.Url);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_MixedClockTextWeb_PreservesAllTypes()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            layout.Widgets.Add(DefaultWidgetFactory.CreateWeb("https://github.com/"));
            layout.Widgets.Add(DefaultWidgetFactory.CreateWeb("https://www.notion.so/"));
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(4, restored.Widgets.Count);
            Assert.Equal(2, restored.Widgets.Count(w => w.Type == WidgetTypes.Web));

            var urls = restored.Widgets
                .Where(w => w.Type == WidgetTypes.Web)
                .Select(w => WebWidgetConfiguration.FromDictionary(w.Configuration).Url)
                .OrderBy(u => u, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(["https://github.com/", "https://www.notion.so/"], urls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RestoresCalendarEventsAndGeometry()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var config = new CalendarWidgetConfiguration
            {
                GoogleIcsUrl = "https://calendar.google.com/calendar/ical/demo/private/basic.ics",
                UseSampleAgendaWhenEmpty = false,
                Events =
                [
                    new CalendarEvent
                    {
                        Id = "ship-1",
                        Provider = CalendarProviderIds.Local,
                        Title = "Calendar ship",
                        Start = DateTimeOffset.Parse("2026-08-13T09:00:00+09:00"),
                        End = DateTimeOffset.Parse("2026-08-13T12:00:00+09:00")
                    }
                ]
            };
            var calendar = DefaultWidgetFactory.CreateCalendar(
                layout.RoomId,
                x: 400,
                y: 60,
                width: 330,
                height: 360,
                configuration: config);
            layout.Widgets.Add(calendar);
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Text);
            var widget = restored.Widgets.Single(w => w.Type == WidgetTypes.Calendar);
            Assert.Equal(calendar.Id, widget.Id);
            Assert.Equal(400, widget.Position.X);
            Assert.Equal(60, widget.Position.Y);

            var loaded = CalendarWidgetConfiguration.FromDictionary(widget.Configuration);
            Assert.Equal(config.GoogleIcsUrl, loaded.GoogleIcsUrl);
            Assert.False(loaded.UseSampleAgendaWhenEmpty);
            Assert.Single(loaded.Events);
            Assert.Equal("Calendar ship", loaded.Events[0].Title);
            Assert.Equal(9, loaded.Events[0].Start.Hour);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RestoresMusicSourcesAndGeometry()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            var config = new MusicWidgetConfiguration
            {
                ActiveSourceId = "spotify-default",
                Sources =
                [
                    new MusicSource
                    {
                        Id = "spotify-default",
                        Type = MusicSourceType.Spotify,
                        Name = "Spotify",
                        Url = "https://open.spotify.com/",
                        IsEnabled = true
                    },
                    new MusicSource
                    {
                        Id = "yt-1",
                        Type = MusicSourceType.YouTube,
                        Name = "YouTube Music",
                        Url = "https://music.youtube.com/",
                        IsEnabled = true
                    }
                ]
            };
            var music = DefaultWidgetFactory.CreateMusic(
                layout.RoomId,
                x: 160,
                y: 100,
                width: 380,
                height: 440,
                configuration: config);
            layout.Widgets.Add(music);
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Text);
            var widget = restored.Widgets.Single(w => w.Type == WidgetTypes.Music);
            Assert.Equal(music.Id, widget.Id);
            Assert.Equal(160, widget.Position.X);
            Assert.Equal(100, widget.Position.Y);
            Assert.Equal(380, widget.Size.Width);
            Assert.Equal(440, widget.Size.Height);
            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);

            var loaded = MusicWidgetConfiguration.FromDictionary(widget.Configuration);
            Assert.Equal("spotify-default", loaded.ActiveSourceId);
            Assert.Equal(2, loaded.Sources.Count);
            Assert.Equal(MusicSourceType.Spotify, loaded.Sources[0].Type);
            Assert.Equal("https://open.spotify.com/", loaded.Sources[0].Url);
            Assert.DoesNotContain("token", System.Text.Json.JsonSerializer.Serialize(widget.Configuration), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_MixedClockTextWebCalendarMusic_PreservesAllTypes()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = DesktopLayout.CreateDefault();
            layout.Widgets.Add(DefaultWidgetFactory.CreateWeb("https://github.com/"));
            layout.Widgets.Add(DefaultWidgetFactory.CreateCalendar());
            layout.Widgets.Add(DefaultWidgetFactory.CreateMusic());
            layout.Widgets.Add(DefaultWidgetFactory.CreateCreative());
            layout.Widgets.Add(DefaultWidgetFactory.CreateAi());
            store.Save(layout);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Text);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Web);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Calendar);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Music);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Creative);
            Assert.Contains(restored.Widgets, w => w.Type == WidgetTypes.Ai);
            Assert.Equal(DesktopLayout.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CreativeWorkspaceStore_AtomicRoundTrip_AndSchemaVersion()
    {
        var path = Path.Combine(CreateTempDir(), "workspace.json");
        try
        {
            var store = new JsonCreativeWorkspaceStore(path);
            var service = new CreativeWorkspaceService(store);
            Assert.True(service.TryAdd(@"D:\Art\piece.png", CreativeItemType.File, "piece", out var item, out _));
            Assert.True(service.TryMarkOpened(item!.Id, DateTimeOffset.UtcNow, out _, out _));

            var reloaded = store.LoadOrCreate();
            Assert.Equal(CreativeWorkspaceDocument.CurrentSchemaVersion, reloaded.SchemaVersion);
            Assert.Single(reloaded.Items);
            Assert.Equal(@"D:\Art\piece.png", reloaded.Items[0].Path);
            Assert.NotNull(reloaded.Items[0].LastOpened);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void CreativeProjectStore_AtomicRoundTrip_SchemaMigration_AndMissingFile()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "projects.json");
        try
        {
            Assert.False(File.Exists(path));
            var store = new JsonCreativeProjectStore(path);
            var created = store.LoadOrCreate();
            Assert.Equal(CreativeProjectDocument.CurrentSchemaVersion, created.SchemaVersion);
            Assert.True(File.Exists(path));

            var service = new CreativeProjectService(store);
            Assert.True(service.TryCreate(
                "My Song",
                "demo",
                CreativeProjectType.Music,
                @"D:\Music\MySong",
                out var project,
                out _));
            Assert.True(service.TryAddResource(
                project!.Id,
                CreativeProjectResourceKind.ExternalLink,
                "YouTube",
                "https://www.youtube.com/watch?v=1",
                out _,
                out _));
            Assert.True(service.TrySaveNotes(project.Id, "サビ", out _, out _));
            var resourceId = service.FindById(project.Id)!.Resources[0].Id;
            Assert.True(service.TryToggleResourceQuickAction(project.Id, resourceId, out _, out _));
            Assert.True(service.TryRecordResourceOpened(
                project.Id, resourceId, DateTimeOffset.UtcNow, out _, out _, out _));

            var reloaded = store.LoadOrCreate();
            Assert.Equal(2, reloaded.SchemaVersion);
            Assert.Single(reloaded.Projects);
            Assert.Equal("My Song", reloaded.Projects[0].Name);
            Assert.Equal("サビ", reloaded.Projects[0].Notes);
            Assert.True(reloaded.Projects[0].Resources[0].IsQuickAction);
            Assert.Single(reloaded.Projects[0].RecentItems);
            Assert.False(File.Exists(path + ".tmp"));

            // v1 document migrates to v2 on load.
            File.WriteAllText(path, """
                {
                  "schemaVersion": 1,
                  "projects": [
                    {
                      "id": "legacy1",
                      "name": "Legacy Project",
                      "projectType": "other",
                      "rootFolder": "D:\\Legacy",
                      "resources": []
                    }
                  ]
                }
                """);
            var migrated = new JsonCreativeProjectStore(path).LoadOrCreate();
            Assert.Equal(CreativeProjectDocument.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.Single(migrated.Projects);
            Assert.Equal("Legacy Project", migrated.Projects[0].Name);
            Assert.NotNull(migrated.Projects[0].RecentItems);
            Assert.Null(migrated.Projects[0].Notes);

            // Missing / corrupt file → empty document, no throw.
            File.WriteAllText(path, "{ not-json");
            var recovered = new JsonCreativeProjectStore(path).LoadOrCreate();
            Assert.Empty(recovered.Projects);
            Assert.Equal(CreativeProjectDocument.CurrentSchemaVersion, recovered.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class ThemePersistenceTests
{
    [Fact]
    public void SaveAndLoad_PreservesThemeTokens()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonThemeStore(dir);
            var theme = ThemeDefinition.CreateDefault();
            theme.Id = "default";
            theme.CornerRadius = 20;
            theme.Transparency = 0.7;
            theme.WidgetBackground = "#AA112233";
            store.Save(theme);

            var restored = store.LoadOrCreateDefault("default");
            Assert.Equal(20, restored.CornerRadius);
            Assert.Equal(0.7, restored.Transparency);
            Assert.Equal("#AA112233", restored.WidgetBackground);
            Assert.Equal(1, restored.SchemaVersion);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
