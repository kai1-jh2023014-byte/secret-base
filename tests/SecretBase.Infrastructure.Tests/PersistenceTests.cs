using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Text;
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

            Assert.Equal(1, restored.SchemaVersion);
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

            Assert.Equal(1, restored.SchemaVersion);
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
    public void LoadMissing_CreatesDefaultClockAndTextLayout()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var layout = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(2, layout.Widgets.Count);
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
                Widgets = [DefaultWidgetFactory.CreateDefaultClock()]
            };
            store.Save(clockOnly);

            var restored = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(1, restored.SchemaVersion);
            Assert.Single(restored.Widgets);
            Assert.Equal(WidgetTypes.Clock, restored.Widgets[0].Type);
            Assert.Equal(clockOnly.Widgets[0].Id, restored.Widgets[0].Id);
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
