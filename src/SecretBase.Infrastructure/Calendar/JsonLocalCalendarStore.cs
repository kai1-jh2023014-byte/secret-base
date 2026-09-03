using System.Text.Json;
using SecretBase.Core.Calendar;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Calendar;

internal sealed class LocalCalendarDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<CalendarEvent> Events { get; set; } = [];

    public List<UsualScheduleSlot> Usual { get; set; } = [];
}

/// <summary>Persists local events + usual schedule under AppData settings. No tokens.</summary>
public sealed class JsonLocalCalendarStore : ILocalCalendarStore
{
    public const int CurrentSchemaVersion = 1;

    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonLocalCalendarStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "local-calendar.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public IReadOnlyList<CalendarEvent> LoadEvents() => Load().Events;

    public void SaveEvents(IReadOnlyList<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        lock (_gate)
        {
            var doc = LoadUnlocked();
            doc.Events = events.ToList();
            SaveUnlocked(doc);
        }
    }

    public IReadOnlyList<UsualScheduleSlot> LoadUsual() => Load().Usual;

    public void SaveUsual(IReadOnlyList<UsualScheduleSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        lock (_gate)
        {
            var doc = LoadUnlocked();
            doc.Usual = slots.ToList();
            SaveUnlocked(doc);
        }
    }

    private LocalCalendarDocument Load()
    {
        lock (_gate)
        {
            return LoadUnlocked();
        }
    }

    private LocalCalendarDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new LocalCalendarDocument { SchemaVersion = CurrentSchemaVersion };
        }

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<LocalCalendarDocument>(json, _options)
                      ?? new LocalCalendarDocument();
            doc.Events ??= [];
            doc.Usual ??= [];
            doc.SchemaVersion = CurrentSchemaVersion;
            return doc;
        }
        catch (JsonException)
        {
            return new LocalCalendarDocument { SchemaVersion = CurrentSchemaVersion };
        }
        catch (IOException)
        {
            return new LocalCalendarDocument { SchemaVersion = CurrentSchemaVersion };
        }
    }

    private void SaveUnlocked(LocalCalendarDocument doc)
    {
        doc.SchemaVersion = CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(doc, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
