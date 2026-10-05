using System.Text.Json;

namespace SecretBase.Core.Widgets.Pomodoro;

public sealed class PomodoroWidgetConfiguration
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public int FocusMinutes { get; set; } = 25;

    public int ShortBreakMinutes { get; set; } = 5;

    public int LongBreakMinutes { get; set; } = 15;

    public bool SoundOnComplete { get; set; } = true;

    public static PomodoroWidgetConfiguration CreateDefault() => new();

    public static PomodoroWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(SchemaVersion), out var ver)
            && ver.ValueKind == JsonValueKind.Number
            && ver.TryGetInt32(out var schema))
        {
            result.SchemaVersion = schema < 1 ? CurrentSchemaVersion : schema;
        }

        if (configuration.TryGetValue(nameof(FocusMinutes), out var focus)
            && focus.ValueKind == JsonValueKind.Number
            && focus.TryGetInt32(out var focusMinutes))
        {
            result.FocusMinutes = Math.Clamp(focusMinutes, 1, 120);
        }

        if (configuration.TryGetValue(nameof(ShortBreakMinutes), out var shortBreak)
            && shortBreak.ValueKind == JsonValueKind.Number
            && shortBreak.TryGetInt32(out var shortMinutes))
        {
            result.ShortBreakMinutes = Math.Clamp(shortMinutes, 1, 60);
        }

        if (configuration.TryGetValue(nameof(LongBreakMinutes), out var longBreak)
            && longBreak.ValueKind == JsonValueKind.Number
            && longBreak.TryGetInt32(out var longMinutes))
        {
            result.LongBreakMinutes = Math.Clamp(longMinutes, 1, 60);
        }

        if (configuration.TryGetValue(nameof(SoundOnComplete), out var sound)
            && (sound.ValueKind == JsonValueKind.True || sound.ValueKind == JsonValueKind.False))
        {
            result.SoundOnComplete = sound.GetBoolean();
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion),
            [nameof(FocusMinutes)] = JsonSerializer.SerializeToElement(Math.Clamp(FocusMinutes, 1, 120)),
            [nameof(ShortBreakMinutes)] = JsonSerializer.SerializeToElement(Math.Clamp(ShortBreakMinutes, 1, 60)),
            [nameof(LongBreakMinutes)] = JsonSerializer.SerializeToElement(Math.Clamp(LongBreakMinutes, 1, 60)),
            [nameof(SoundOnComplete)] = JsonSerializer.SerializeToElement(SoundOnComplete)
        };
}
