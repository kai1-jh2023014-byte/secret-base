using System.Text.Json;
using SecretBase.Core.Music;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Widgets.Music;

/// <summary>
/// Music Widget settings: source list + optional active source.
/// Never stores tokens, cookies, API keys, or passwords.
/// </summary>
public sealed class MusicWidgetConfiguration
{
    public const string DefaultSpotifyUrl = "https://open.spotify.com/";
    public const string DefaultYouTubeUrl = "https://music.youtube.com/";

    public List<MusicSource> Sources { get; set; } = [];

    /// <summary>Last opened / selected source id (optional).</summary>
    public string? ActiveSourceId { get; set; }

    /// <summary>Last selected track metadata (never tokens). Used to restore UI.</summary>
    public MusicTrack? CurrentTrack { get; set; }

    /// <summary>Compact floating face (art + title + transport) vs full search surface.</summary>
    public bool IsCompact { get; set; }

    /// <summary>Last expanded frame width so compact toggles do not wipe a custom size.</summary>
    public double? ExpandedWidth { get; set; }

    /// <summary>Last expanded frame height so compact toggles do not wipe a custom size.</summary>
    public double? ExpandedHeight { get; set; }

    public static MusicWidgetConfiguration CreateDefault()
    {
        var config = new MusicWidgetConfiguration
        {
            Sources =
            [
                new MusicSource
                {
                    Id = "spotify-default",
                    Type = MusicSourceType.Spotify,
                    Name = "Spotify",
                    Url = DefaultSpotifyUrl,
                    IsEnabled = true
                },
                new MusicSource
                {
                    Id = "youtube-default",
                    Type = MusicSourceType.YouTube,
                    Name = "YouTube Music",
                    Url = DefaultYouTubeUrl,
                    IsEnabled = true
                },
                new MusicSource
                {
                    Id = "local-placeholder",
                    Type = MusicSourceType.Local,
                    Name = "Local",
                    Url = null,
                    IsEnabled = true
                }
            ]
        };
        return config;
    }

    public static MusicWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = new MusicWidgetConfiguration();

        if (configuration.TryGetValue(nameof(ActiveSourceId), out var active)
            && active.ValueKind == JsonValueKind.String)
        {
            var id = active.GetString();
            result.ActiveSourceId = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        if (configuration.TryGetValue(nameof(CurrentTrack), out var trackEl)
            && trackEl.ValueKind == JsonValueKind.Object
            && TryReadTrack(trackEl, out var track))
        {
            result.CurrentTrack = track;
        }

        if (configuration.TryGetValue(nameof(IsCompact), out var compact)
            && (compact.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.IsCompact = compact.GetBoolean();
        }

        if (configuration.TryGetValue(nameof(ExpandedWidth), out var ew)
            && ew.ValueKind == JsonValueKind.Number
            && ew.TryGetDouble(out var expandedW)
            && expandedW >= 120)
        {
            result.ExpandedWidth = expandedW;
        }

        if (configuration.TryGetValue(nameof(ExpandedHeight), out var eh)
            && eh.ValueKind == JsonValueKind.Number
            && eh.TryGetDouble(out var expandedH)
            && expandedH >= 80)
        {
            result.ExpandedHeight = expandedH;
        }

        if (configuration.TryGetValue(nameof(Sources), out var sources)
            && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sources.EnumerateArray())
            {
                if (TryReadSource(item, out var source))
                {
                    result.Sources.Add(source);
                }
            }
        }

        if (result.Sources.Count == 0)
        {
            // Keep compact / size prefs when sources fall back to defaults.
            var defaults = CreateDefault();
            defaults.IsCompact = result.IsCompact;
            defaults.CurrentTrack = result.CurrentTrack;
            defaults.ActiveSourceId = result.ActiveSourceId;
            defaults.ExpandedWidth = result.ExpandedWidth;
            defaults.ExpandedHeight = result.ExpandedHeight;
            return defaults;
        }

        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary()
    {
        // Drop invalid web URLs before persist; keep Local without URL.
        var cleaned = Sources
            .Select(SanitizeForPersist)
            .Where(s => s is not null)
            .Cast<MusicSource>()
            .ToList();

        var bag = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(ActiveSourceId)] = JsonSerializer.SerializeToElement(ActiveSourceId),
            [nameof(IsCompact)] = JsonSerializer.SerializeToElement(IsCompact),
            [nameof(Sources)] = JsonSerializer.SerializeToElement(
                cleaned.Select(SerializeSource).ToList())
        };

        if (ExpandedWidth is > 0)
        {
            bag[nameof(ExpandedWidth)] = JsonSerializer.SerializeToElement(ExpandedWidth.Value);
        }

        if (ExpandedHeight is > 0)
        {
            bag[nameof(ExpandedHeight)] = JsonSerializer.SerializeToElement(ExpandedHeight.Value);
        }

        if (CurrentTrack is not null && !string.IsNullOrWhiteSpace(CurrentTrack.Title))
        {
            bag[nameof(CurrentTrack)] = JsonSerializer.SerializeToElement(SerializeTrack(CurrentTrack));
        }

        return bag;
    }

    public MusicSource? FindSource(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Sources.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    private static MusicSource? SanitizeForPersist(MusicSource source)
    {
        if (string.IsNullOrWhiteSpace(source.Name))
        {
            return null;
        }

        if (source.Type == MusicSourceType.Local)
        {
            return new MusicSource
            {
                Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
                Type = MusicSourceType.Local,
                Name = source.Name.Trim(),
                Url = null,
                IsEnabled = source.IsEnabled
            };
        }

        if (!WebUrlValidator.TryNormalize(source.Url, out var normalized, out _))
        {
            return null;
        }

        return new MusicSource
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
            Type = source.Type,
            Name = source.Name.Trim(),
            Url = normalized,
            IsEnabled = source.IsEnabled
        };
    }

    private static Dictionary<string, JsonElement> SerializeSource(MusicSource s) =>
        new(StringComparer.Ordinal)
        {
            ["Id"] = JsonSerializer.SerializeToElement(s.Id),
            ["Type"] = JsonSerializer.SerializeToElement(s.Type.ToString()),
            ["Name"] = JsonSerializer.SerializeToElement(s.Name),
            ["Url"] = JsonSerializer.SerializeToElement(s.Url),
            ["IsEnabled"] = JsonSerializer.SerializeToElement(s.IsEnabled)
        };

    private static Dictionary<string, JsonElement> SerializeTrack(MusicTrack t) =>
        new(StringComparer.Ordinal)
        {
            ["Id"] = JsonSerializer.SerializeToElement(t.Id),
            ["Title"] = JsonSerializer.SerializeToElement(t.Title),
            ["Artist"] = JsonSerializer.SerializeToElement(t.Artist),
            ["Album"] = JsonSerializer.SerializeToElement(t.Album),
            ["ProviderId"] = JsonSerializer.SerializeToElement(t.ProviderId),
            ["Source"] = JsonSerializer.SerializeToElement(t.Source)
            // ArtworkUrl intentionally omitted from persistence — avoid storing remote image fetches.
        };

    private static bool TryReadTrack(JsonElement item, out MusicTrack track)
    {
        track = new MusicTrack();
        var title = ReadString(item, "Title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        track = new MusicTrack
        {
            Id = ReadString(item, "Id") ?? Guid.NewGuid().ToString("N"),
            Title = title.Trim(),
            Artist = ReadString(item, "Artist") ?? string.Empty,
            Album = ReadString(item, "Album"),
            ProviderId = ReadString(item, "ProviderId") ?? string.Empty,
            Source = ReadString(item, "Source") ?? string.Empty,
            ArtworkUrl = null
        };
        return true;
    }

    private static bool TryReadSource(JsonElement item, out MusicSource source)
    {
        source = new MusicSource();
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var name = ReadString(item, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var type = MusicSourceType.Web;
        if (item.TryGetProperty("Type", out var typeEl))
        {
            if (typeEl.ValueKind == JsonValueKind.String
                && Enum.TryParse<MusicSourceType>(typeEl.GetString(), ignoreCase: true, out var parsed))
            {
                type = parsed;
            }
            else if (typeEl.ValueKind == JsonValueKind.Number
                     && typeEl.TryGetInt32(out var n)
                     && Enum.IsDefined(typeof(MusicSourceType), n))
            {
                type = (MusicSourceType)n;
            }
        }

        var urlRaw = ReadString(item, "Url");
        string? url = null;
        if (type == MusicSourceType.Local)
        {
            url = null;
        }
        else if (!WebUrlValidator.TryNormalize(urlRaw, out var normalized, out _))
        {
            // Reject invalid web sources entirely (do not persist dangerous schemes).
            return false;
        }
        else
        {
            url = normalized;
        }

        var enabled = true;
        if (item.TryGetProperty("IsEnabled", out var en)
            && en.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            enabled = en.GetBoolean();
        }

        source = new MusicSource
        {
            Id = ReadString(item, "Id") ?? Guid.NewGuid().ToString("N"),
            Type = type,
            Name = name.Trim(),
            Url = url,
            IsEnabled = enabled
        };
        return true;
    }

    private static string? ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return el.GetString();
    }
}
