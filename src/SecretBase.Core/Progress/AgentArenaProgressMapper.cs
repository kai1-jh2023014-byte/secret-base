namespace SecretBase.Core.Progress;

/// <summary>
/// Maps Agent Arena public counters into the Progress / Genesis widget snapshot.
/// Caps match the published skill: 500 Genesis + 3000 Ascension = 3500 total.
/// </summary>
public static class AgentArenaProgressMapper
{
    public const int GenesisCap = 500;
    public const int AscensionCap = 3000;
    public const int TotalCap = GenesisCap + AscensionCap;
    public const int IdentityCap = 6; // 1 Genesis + 5 Ascensions per wallet

    public const string DefaultApiBase = "https://agent-arena-api.agentarenaonbase.workers.dev";

    public static ProgressGenesisSnapshot FromSummary(
        int roundWins,
        int roundLosses,
        int genesisMinted,
        int ascensionMinted,
        DateTimeOffset? updatedAt = null,
        AgentArenaWalletProgress? wallet = null)
    {
        genesisMinted = Math.Max(0, genesisMinted);
        ascensionMinted = Math.Max(0, ascensionMinted);
        var totalMinted = genesisMinted + ascensionMinted;
        var decided = Math.Max(0, roundWins) + Math.Max(0, roundLosses);
        var winRate = decided == 0 ? 0 : 100.0 * roundWins / decided;

        ProgressTrack progress;
        GenesisTrack genesis;

        if (wallet is not null)
        {
            var identity = Math.Clamp(wallet.GenesisWins, 0, 1) + Math.Clamp(wallet.AscensionWins, 0, 5);
            progress = new ProgressTrack
            {
                Title = "Identity Progress",
                Percent = 100.0 * identity / IdentityCap,
                Status = $"{identity}/{IdentityCap} identity slots",
                Detail = MaskWallet(wallet.Wallet) is { Length: > 0 } masked
                    ? $"{masked} · {wallet.Wins} arena wins"
                    : $"{wallet.Wins} arena wins"
            };

            genesis = new GenesisTrack
            {
                Title = "Genesis",
                Phase = wallet.GenesisWins > 0 ? "Ascension" : "Genesis",
                Stage = wallet.GenesisWins > 0
                    ? Math.Clamp(wallet.AscensionWins + 1, 1, 5)
                    : 1,
                StageCount = 5,
                Percent = wallet.GenesisWins > 0 ? 100.0 * Math.Clamp(wallet.AscensionWins, 0, 5) / 5 : 0,
                Status = wallet.GenesisWins > 0
                    ? $"Genesis claimed · Ascension {Math.Clamp(wallet.AscensionWins, 0, 5)}/5"
                    : "Genesis not yet claimed",
                Milestones =
                [
                    GenesisMilestone.Create("Claim Genesis", wallet.GenesisWins > 0),
                    GenesisMilestone.Create("First Ascension", wallet.AscensionWins >= 1),
                    GenesisMilestone.Create("Three Ascensions", wallet.AscensionWins >= 3),
                    GenesisMilestone.Create("Identity complete (5 Ascensions)", wallet.AscensionWins >= 5)
                ]
            };
        }
        else
        {
            progress = new ProgressTrack
            {
                Title = "Arena Progress",
                Percent = 100.0 * totalMinted / TotalCap,
                Status = $"{totalMinted} minted · {roundWins}W / {roundLosses}L",
                Detail = decided == 0
                    ? $"Ascension {ascensionMinted}/{AscensionCap}"
                    : $"Win rate {winRate:0.#}% · Ascension {ascensionMinted}/{AscensionCap}"
            };

            var genesisPercent = 100.0 * genesisMinted / GenesisCap;
            genesis = new GenesisTrack
            {
                Title = "Genesis",
                Phase = ResolveGenesisPhase(genesisPercent),
                Stage = ResolveGenesisStage(genesisPercent),
                StageCount = 5,
                Percent = genesisPercent,
                Status = $"{genesisMinted} / {GenesisCap} minted",
                Milestones =
                [
                    GenesisMilestone.Create("First Genesis mint", genesisMinted >= 1),
                    GenesisMilestone.Create("10% Genesis supply", genesisMinted >= GenesisCap / 10),
                    GenesisMilestone.Create("25% Genesis supply", genesisMinted >= GenesisCap / 4),
                    GenesisMilestone.Create("Ascension phase active", ascensionMinted >= 1),
                    GenesisMilestone.Create("50% Genesis supply", genesisMinted >= GenesisCap / 2)
                ]
            };
        }

        var snapshot = new ProgressGenesisSnapshot
        {
            Schema = ProgressGenesisSnapshot.SchemaVersion,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            SourceKind = ProgressGenesisSourceKinds.AgentArena,
            Progress = progress,
            Genesis = genesis
        };
        snapshot.Normalize();
        return snapshot;
    }

    public static string ResolveGenesisPhase(double genesisPercent) =>
        genesisPercent switch
        {
            < 1 => "Opening",
            < 25 => "Foundation",
            < 50 => "Expansion",
            < 80 => "Late Genesis",
            _ => "Closing"
        };

    public static int ResolveGenesisStage(double genesisPercent) =>
        genesisPercent switch
        {
            < 20 => 1,
            < 40 => 2,
            < 60 => 3,
            < 80 => 4,
            _ => 5
        };

    public static string MaskWallet(string? wallet)
    {
        if (string.IsNullOrWhiteSpace(wallet) || wallet.Length < 10)
        {
            return string.Empty;
        }

        var trimmed = wallet.Trim();
        return $"{trimmed[..6]}…{trimmed[^4..]}";
    }
}

/// <summary>Optional per-wallet stats from the Agent Arena leaderboard.</summary>
public sealed class AgentArenaWalletProgress
{
    public string Wallet { get; init; } = string.Empty;

    public int Wins { get; init; }

    public int GenesisWins { get; init; }

    public int AscensionWins { get; init; }
}
