using System.Text.Json;
using SecretBase.Core.Progress;

namespace SecretBase.Core.Widgets.Progress;

/// <summary>
/// Progress / Genesis widget settings.
/// Default source is the personal Progress learning API + Genesis MusicLab.
/// </summary>
public sealed class ProgressWidgetConfiguration
{
    public const int CurrentSchemaVersion = 4;

    public const string DisplayFull = "full";
    public const string DisplayMinimal = "minimal";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// <see cref="ProgressGenesisSources"/> — personal-systems (default), agent-arena, local, or http.
    /// </summary>
    public string Source { get; set; } = ProgressGenesisSources.PersonalSystems;

    /// <summary>
    /// <see cref="DisplayFull"/> (card) or <see cref="DisplayMinimal"/> (transparent, two-line).
    /// </summary>
    public string DisplayMode { get; set; } = DisplayFull;

    /// <summary>
    /// Progress FastAPI root. Empty = <see cref="ProgressLearningMapper.DefaultApiBase"/>
    /// (<c>http://127.0.0.1:8001</c>).
    /// </summary>
    public string? ProgressApiBase { get; set; }

    /// <summary>
    /// Optional Genesis / MusicLab status HTTPS or localhost URL (JSON).
    /// When unset, the provider probes the Genesis folder + Desktop .bat launchers.
    /// </summary>
    public string? GenesisStatusUrl { get; set; }

    /// <summary>
    /// Optional path to the Genesis project root (contains <c>scripts/windows/Start-MusicLab.bat</c>).
    /// Empty = auto-detect common WSL / home paths.
    /// </summary>
    public string? GenesisRoot { get; set; }

    /// <summary>
    /// When <see cref="Source"/> is http, fetch this URL (same JSON schema as the local store).
    /// </summary>
    public string? RemoteUrl { get; set; }

    /// <summary>
    /// Optional Agent Arena wallet (0x…). Only used when Source = agent-arena.
    /// </summary>
    public string? WalletAddress { get; set; }

    /// <summary>Optional Agent Arena API root override.</summary>
    public string? ArenaApiBase { get; set; }

    /// <summary>Auto-refresh interval in seconds (15–600). Default 60.</summary>
    public int RefreshSeconds { get; set; } = 60;

    /// <summary>Show Genesis milestones list (first incomplete + completed count). Ignored in minimal mode.</summary>
    public bool ShowMilestones { get; set; } = true;

    public bool IsMinimal =>
        string.Equals(NormalizeDisplayMode(DisplayMode), DisplayMinimal, StringComparison.Ordinal);

    public static ProgressWidgetConfiguration CreateDefault() => new();

    public static int ClampRefreshSeconds(int value) => Math.Clamp(value, 15, 600);

    public static string NormalizeDisplayMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return DisplayFull;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "minimal" or "min" or "clear" or "transparent" or "overlay" => DisplayMinimal,
            _ => DisplayFull
        };
    }

    public static string? NormalizeWallet(string? wallet)
    {
        if (string.IsNullOrWhiteSpace(wallet))
        {
            return null;
        }

        var trimmed = wallet.Trim();
        if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || trimmed.Length < 10)
        {
            return null;
        }

        return trimmed;
    }

    public static ProgressWidgetConfiguration FromDictionary(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var result = CreateDefault();
        if (configuration.TryGetValue(nameof(SchemaVersion), out var ver)
            && ver.ValueKind == JsonValueKind.Number
            && ver.TryGetInt32(out var schema))
        {
            result.SchemaVersion = schema < 1 ? CurrentSchemaVersion : schema;
        }

        if (configuration.TryGetValue(nameof(Source), out var source)
            && source.ValueKind == JsonValueKind.String)
        {
            result.Source = ProgressGenesisSources.Normalize(source.GetString());
        }
        else if (configuration.TryGetValue(nameof(RemoteUrl), out var legacyUrl)
                 && legacyUrl.ValueKind == JsonValueKind.String
                 && !string.IsNullOrWhiteSpace(legacyUrl.GetString()))
        {
            result.Source = ProgressGenesisSources.Http;
        }

        if (configuration.TryGetValue(nameof(DisplayMode), out var display)
            && display.ValueKind == JsonValueKind.String)
        {
            result.DisplayMode = NormalizeDisplayMode(display.GetString());
        }

        if (configuration.TryGetValue(nameof(ProgressApiBase), out var progressBase)
            && progressBase.ValueKind == JsonValueKind.String)
        {
            var raw = progressBase.GetString()?.Trim();
            result.ProgressApiBase = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(GenesisStatusUrl), out var genesisUrl)
            && genesisUrl.ValueKind == JsonValueKind.String)
        {
            var raw = genesisUrl.GetString()?.Trim();
            result.GenesisStatusUrl = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(GenesisRoot), out var genesisRoot)
            && genesisRoot.ValueKind == JsonValueKind.String)
        {
            var raw = genesisRoot.GetString()?.Trim();
            result.GenesisRoot = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(RemoteUrl), out var url)
            && url.ValueKind == JsonValueKind.String)
        {
            var raw = url.GetString()?.Trim();
            result.RemoteUrl = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(WalletAddress), out var wallet)
            && wallet.ValueKind == JsonValueKind.String)
        {
            result.WalletAddress = NormalizeWallet(wallet.GetString());
        }

        if (configuration.TryGetValue(nameof(ArenaApiBase), out var arenaBase)
            && arenaBase.ValueKind == JsonValueKind.String)
        {
            var raw = arenaBase.GetString()?.Trim();
            result.ArenaApiBase = string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        if (configuration.TryGetValue(nameof(RefreshSeconds), out var refresh)
            && refresh.ValueKind == JsonValueKind.Number
            && refresh.TryGetInt32(out var seconds))
        {
            result.RefreshSeconds = ClampRefreshSeconds(seconds);
        }

        if (configuration.TryGetValue(nameof(ShowMilestones), out var milestones)
            && (milestones.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            result.ShowMilestones = milestones.GetBoolean();
        }

        result.Source = ProgressGenesisSources.Normalize(result.Source);
        result.DisplayMode = NormalizeDisplayMode(result.DisplayMode);
        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion),
            [nameof(Source)] = JsonSerializer.SerializeToElement(ProgressGenesisSources.Normalize(Source)),
            [nameof(DisplayMode)] = JsonSerializer.SerializeToElement(NormalizeDisplayMode(DisplayMode)),
            [nameof(ProgressApiBase)] = JsonSerializer.SerializeToElement(ProgressApiBase),
            [nameof(GenesisStatusUrl)] = JsonSerializer.SerializeToElement(GenesisStatusUrl),
            [nameof(GenesisRoot)] = JsonSerializer.SerializeToElement(GenesisRoot),
            [nameof(RemoteUrl)] = JsonSerializer.SerializeToElement(RemoteUrl),
            [nameof(WalletAddress)] = JsonSerializer.SerializeToElement(NormalizeWallet(WalletAddress)),
            [nameof(ArenaApiBase)] = JsonSerializer.SerializeToElement(ArenaApiBase),
            [nameof(RefreshSeconds)] = JsonSerializer.SerializeToElement(ClampRefreshSeconds(RefreshSeconds)),
            [nameof(ShowMilestones)] = JsonSerializer.SerializeToElement(ShowMilestones)
        };
}
