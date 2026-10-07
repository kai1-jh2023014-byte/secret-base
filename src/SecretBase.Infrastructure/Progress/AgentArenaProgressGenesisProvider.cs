using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Fetches live Agent Arena mint / duel counters and maps them into Progress + Genesis.
/// Confirmed endpoints: <c>/winners/summary</c>, <c>/winners/top</c> (optional wallet).
/// </summary>
public sealed class AgentArenaProgressGenesisProvider : IProgressGenesisProvider
{
    private readonly HttpClient _http;
    private readonly string _apiBase;
    private readonly string? _wallet;
    private readonly IProgressGenesisProvider _fallback;
    private readonly JsonSerializerOptions _options;

    public AgentArenaProgressGenesisProvider(
        string? apiBase = null,
        string? walletAddress = null,
        HttpClient? httpClient = null,
        IProgressGenesisProvider? fallback = null,
        JsonSerializerOptions? options = null)
    {
        var root = string.IsNullOrWhiteSpace(apiBase)
            ? AgentArenaProgressMapper.DefaultApiBase
            : apiBase.Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Agent Arena API base must be an absolute HTTPS URI.", nameof(apiBase));
        }

        _apiBase = uri.AbsoluteUri.TrimEnd('/');
        _wallet = ProgressWidgetWallet.Normalize(walletAddress);
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _fallback = fallback ?? new LocalJsonProgressGenesisProvider();
        _options = options ?? SecretBaseJson.CreateOptions();
    }

    public string ProviderId => "agent-arena";

    public string DisplayName => "Agent Arena";

    public string SourceKind => ProgressGenesisSourceKinds.AgentArena;

    public string ApiBase => _apiBase;

    public async Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _http
                .GetFromJsonAsync<ArenaSummaryDto>($"{_apiBase}/winners/summary", _options, cancellationToken)
                .ConfigureAwait(false);
            if (summary?.Minted is null || summary.Rounds is null)
            {
                return await _fallback.GetAsync(cancellationToken).ConfigureAwait(false);
            }

            AgentArenaWalletProgress? walletProgress = null;
            if (!string.IsNullOrWhiteSpace(_wallet))
            {
                walletProgress = await TryLoadWalletAsync(_wallet, cancellationToken).ConfigureAwait(false);
            }

            return AgentArenaProgressMapper.FromSummary(
                summary.Rounds.Wins,
                summary.Rounds.Losses,
                summary.Minted.Genesis,
                summary.Minted.Ascension,
                DateTimeOffset.UtcNow,
                walletProgress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fallback = await _fallback.GetAsync(cancellationToken).ConfigureAwait(false);
            fallback.Normalize();
            return fallback;
        }
    }

    private async Task<AgentArenaWalletProgress?> TryLoadWalletAsync(
        string wallet,
        CancellationToken cancellationToken)
    {
        try
        {
            var board = await _http
                .GetFromJsonAsync<ArenaLeaderboardDto>($"{_apiBase}/winners/top", _options, cancellationToken)
                .ConfigureAwait(false);
            var match = board?.Leaders?
                .FirstOrDefault(l =>
                    !string.IsNullOrWhiteSpace(l.Wallet)
                    && string.Equals(l.Wallet.Trim(), wallet, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return new AgentArenaWalletProgress
                {
                    Wallet = wallet,
                    Wins = 0,
                    GenesisWins = 0,
                    AscensionWins = 0
                };
            }

            return new AgentArenaWalletProgress
            {
                Wallet = match.Wallet ?? wallet,
                Wins = match.Wins,
                GenesisWins = match.GenesisWins,
                AscensionWins = match.AscensionWins
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class ArenaSummaryDto
    {
        [JsonPropertyName("rounds")]
        public ArenaRoundsDto? Rounds { get; set; }

        [JsonPropertyName("minted")]
        public ArenaMintedDto? Minted { get; set; }
    }

    private sealed class ArenaRoundsDto
    {
        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("losses")]
        public int Losses { get; set; }
    }

    private sealed class ArenaMintedDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("genesis")]
        public int Genesis { get; set; }

        [JsonPropertyName("ascension")]
        public int Ascension { get; set; }
    }

    private sealed class ArenaLeaderboardDto
    {
        [JsonPropertyName("leaders")]
        public List<ArenaLeaderDto>? Leaders { get; set; }
    }

    private sealed class ArenaLeaderDto
    {
        [JsonPropertyName("wallet")]
        public string? Wallet { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("genesisWins")]
        public int GenesisWins { get; set; }

        [JsonPropertyName("ascensionWins")]
        public int AscensionWins { get; set; }
    }
}

/// <summary>Shared wallet normalization for Infrastructure (avoids Core→Widgets coupling).</summary>
internal static class ProgressWidgetWallet
{
    public static string? Normalize(string? wallet)
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
}
