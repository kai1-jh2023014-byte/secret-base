using System.Text.Json;
using SecretBase.Core.Progress;

namespace SecretBase.Core.Widgets.Progress;

/// <summary>
/// Progress / Genesis widget settings.
/// Default source is live Agent Arena counters; local JSON and custom HTTPS remain available.
/// </summary>
public sealed class ProgressWidgetConfiguration
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// <see cref="ProgressGenesisSources"/> — agent-arena (default), local, or http.
    /// </summary>
    public string Source { get; set; } = ProgressGenesisSources.AgentArena;

    /// <summary>
    /// When <see cref="Source"/> is http, fetch this URL (same JSON schema as the local store).
    /// </summary>
    public string? RemoteUrl { get; set; }

    /// <summary>
    /// Optional Agent Arena wallet (0x…). When set with agent-arena source, Progress shows
    /// personal identity completion from the public leaderboard.
    /// </summary>
    public string? WalletAddress { get; set; }

    /// <summary>
    /// Optional override for the Agent Arena API root.
    /// Empty = <see cref="AgentArenaProgressMapper.DefaultApiBase"/>.
    /// </summary>
    public string? ArenaApiBase { get; set; }

    /// <summary>Auto-refresh interval in seconds (15–600). Default 60.</summary>
    public int RefreshSeconds { get; set; } = 60;

    /// <summary>Show Genesis milestones list (first incomplete + completed count).</summary>
    public bool ShowMilestones { get; set; } = true;

    public static ProgressWidgetConfiguration CreateDefault() => new();

    public static int ClampRefreshSeconds(int value) => Math.Clamp(value, 15, 600);

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
            // Schema v1: RemoteUrl alone implied HTTP.
            result.Source = ProgressGenesisSources.Http;
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
        return result;
    }

    public Dictionary<string, JsonElement> ToDictionary() =>
        new(StringComparer.Ordinal)
        {
            [nameof(SchemaVersion)] = JsonSerializer.SerializeToElement(CurrentSchemaVersion),
            [nameof(Source)] = JsonSerializer.SerializeToElement(ProgressGenesisSources.Normalize(Source)),
            [nameof(RemoteUrl)] = JsonSerializer.SerializeToElement(RemoteUrl),
            [nameof(WalletAddress)] = JsonSerializer.SerializeToElement(NormalizeWallet(WalletAddress)),
            [nameof(ArenaApiBase)] = JsonSerializer.SerializeToElement(ArenaApiBase),
            [nameof(RefreshSeconds)] = JsonSerializer.SerializeToElement(ClampRefreshSeconds(RefreshSeconds)),
            [nameof(ShowMilestones)] = JsonSerializer.SerializeToElement(ShowMilestones)
        };
}
