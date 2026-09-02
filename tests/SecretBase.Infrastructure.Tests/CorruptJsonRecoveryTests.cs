using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

[Collection(nameof(CorruptJsonRecoverySerialTests))]
public class CorruptLayoutRecoveryTests
{
    [Fact]
    public void LoadOrCreateDefault_ValidJson_LoadsNormally()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonLayoutStore(dir);
            var expected = DesktopLayout.CreateDefault();
            store.Save(expected);

            var loaded = store.LoadOrCreateDefault(RoomId.DefaultRoomId);
            Assert.Equal(expected.Widgets.Count, loaded.Widgets.Count);
            Assert.DoesNotContain(
                Directory.GetFiles(dir, "*.corrupt-*"),
                _ => true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_MissingFile_CreatesDefault()
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
    public void LoadOrCreateDefault_MalformedJson_RestoresDefaultAndBacksUpCorruptFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "default.layout.json");
            File.WriteAllText(path, "{ not-valid-json");

            var logger = new TestAppLogger();
            var store = new JsonLayoutStore(dir, logger: logger);
            var layout = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(2, layout.Widgets.Count);
            Assert.Contains(layout.Widgets, w => w.Type == WidgetTypes.Clock);
            var backups = Directory.GetFiles(dir, "default.layout.corrupt-*.json");
            Assert.Single(backups);
            Assert.Contains("not-valid-json", File.ReadAllText(backups[0]));
            Assert.True(File.Exists(path));
            Assert.Contains(logger.Warnings, m => m.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logger.Infos, m => m.Contains("Default layout restored", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_EmptyFile_RestoresDefaultAndBacksUpCorruptFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "default.layout.json");
            File.WriteAllText(path, "   ");

            var store = new JsonLayoutStore(dir);
            var layout = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(2, layout.Widgets.Count);
            var backups = Directory.GetFiles(dir, "default.layout.corrupt-*.json");
            Assert.Single(backups);
            Assert.True(string.IsNullOrWhiteSpace(File.ReadAllText(backups[0])));
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_MalformedJsonWhenBackupFails_StillRestoresDefault()
    {
        var dir = CreateTempDir();
        var fixedUtc = new DateTime(2026, 9, 1, 12, 30, 45, DateTimeKind.Utc);
        CorruptJsonFileRecovery.SetTestUtcNowForTests(fixedUtc);
        try
        {
            var path = Path.Combine(dir, "default.layout.json");
            var blockedBackup = Path.Combine(dir, "default.layout.corrupt-2026-09-01T123045Z.json");
            File.WriteAllText(blockedBackup, "existing-backup");
            File.WriteAllText(path, "{ not-valid-json");

            var logger = new TestAppLogger();
            var store = new JsonLayoutStore(dir, logger: logger);
            var layout = store.LoadOrCreateDefault(RoomId.DefaultRoomId);

            Assert.Equal(2, layout.Widgets.Count);
            Assert.Contains(layout.Widgets, w => w.Type == WidgetTypes.Clock);
            Assert.True(File.Exists(path));
            Assert.DoesNotContain("not-valid-json", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal("existing-backup", File.ReadAllText(blockedBackup));
            Assert.Contains(logger.Warnings, m => m.Contains("Could not back up corrupt file", StringComparison.Ordinal));
            Assert.Contains(logger.Infos, m => m.Contains("Default layout restored", StringComparison.Ordinal));
        }
        finally
        {
            CorruptJsonFileRecovery.ClearTestUtcNowForTests();
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

[Collection(nameof(CorruptJsonRecoverySerialTests))]
public class CorruptThemeRecoveryTests
{
    [Fact]
    public void LoadOrCreateDefault_ValidJson_LoadsNormally()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonThemeStore(dir);
            var theme = ThemeDefinition.CreateDefault();
            theme.CornerRadius = 18;
            store.Save(theme);

            var loaded = store.LoadOrCreateDefault("default");
            Assert.Equal(18, loaded.CornerRadius);
            Assert.DoesNotContain(
                Directory.GetFiles(dir, "*.corrupt-*"),
                _ => true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_MissingFile_CreatesDefault()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new JsonThemeStore(dir);
            var theme = store.LoadOrCreateDefault("default");

            Assert.Equal(ThemeDefinition.CreateDefault().Background, theme.Background);
            Assert.True(File.Exists(Path.Combine(dir, "default.theme.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_MalformedJson_RestoresDefaultAndBacksUpCorruptFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "default.theme.json");
            File.WriteAllText(path, "{ broken");

            var logger = new TestAppLogger();
            var store = new JsonThemeStore(dir, logger: logger);
            var theme = store.LoadOrCreateDefault("default");

            Assert.Equal(ThemeDefinition.CreateDefault().Accent, theme.Accent);
            var backups = Directory.GetFiles(dir, "default.theme.corrupt-*.json");
            Assert.Single(backups);
            Assert.Contains("broken", File.ReadAllText(backups[0]));
            Assert.True(File.Exists(path));
            Assert.Contains(logger.Warnings, m => m.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logger.Infos, m => m.Contains("Default theme restored", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_EmptyFile_RestoresDefaultAndBacksUpCorruptFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "default.theme.json");
            File.WriteAllText(path, string.Empty);

            var store = new JsonThemeStore(dir);
            var theme = store.LoadOrCreateDefault("default");

            Assert.Equal(ThemeDefinition.CreateDefault().Foreground, theme.Foreground);
            var backups = Directory.GetFiles(dir, "default.theme.corrupt-*.json");
            Assert.Single(backups);
            Assert.True(string.IsNullOrWhiteSpace(File.ReadAllText(backups[0])));
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_MalformedJsonWhenBackupFails_StillRestoresDefault()
    {
        var dir = CreateTempDir();
        var fixedUtc = new DateTime(2026, 9, 1, 12, 30, 45, DateTimeKind.Utc);
        CorruptJsonFileRecovery.SetTestUtcNowForTests(fixedUtc);
        try
        {
            var path = Path.Combine(dir, "default.theme.json");
            var blockedBackup = Path.Combine(dir, "default.theme.corrupt-2026-09-01T123045Z.json");
            File.WriteAllText(blockedBackup, "existing-backup");
            File.WriteAllText(path, "{ broken");

            var logger = new TestAppLogger();
            var store = new JsonThemeStore(dir, logger: logger);
            var theme = store.LoadOrCreateDefault("default");

            Assert.Equal(ThemeDefinition.CreateDefault().Accent, theme.Accent);
            Assert.True(File.Exists(path));
            Assert.DoesNotContain("broken", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal("existing-backup", File.ReadAllText(blockedBackup));
            Assert.Contains(logger.Warnings, m => m.Contains("Could not back up corrupt file", StringComparison.Ordinal));
            Assert.Contains(logger.Infos, m => m.Contains("Default theme restored", StringComparison.Ordinal));
        }
        finally
        {
            CorruptJsonFileRecovery.ClearTestUtcNowForTests();
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

public sealed class CorruptJsonRecoverySerialTests
{
}

internal sealed class TestAppLogger : IAppLogger
{
    public List<string> Infos { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public void Info(string category, string message) => Infos.Add(message);

    public void Warn(string category, string message) => Warnings.Add(message);

    public void Error(string category, string message, Exception? exception = null) =>
        Errors.Add(message);
}
