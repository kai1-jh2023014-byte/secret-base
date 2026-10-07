using SecretBase.Core.Progress;

namespace SecretBase.Core.Tests;

public class AgentArenaProgressMapperTests
{
    [Fact]
    public void FromSummary_MapsGlobalMintCounters()
    {
        var snapshot = AgentArenaProgressMapper.FromSummary(
            roundWins: 40,
            roundLosses: 22,
            genesisMinted: 12,
            ascensionMinted: 24);

        Assert.Equal(ProgressGenesisSourceKinds.AgentArena, snapshot.SourceKind);
        Assert.Equal("Arena Progress", snapshot.Progress.Title);
        Assert.Equal(100.0 * 36 / AgentArenaProgressMapper.TotalCap, snapshot.Progress.Percent, 3);
        Assert.Contains("40W", snapshot.Progress.Status);
        Assert.Equal(100.0 * 12 / AgentArenaProgressMapper.GenesisCap, snapshot.Genesis.Percent, 3);
        Assert.Equal("Foundation", snapshot.Genesis.Phase);
        Assert.Contains(snapshot.Genesis.Milestones, m => m.Label.Contains("First Genesis") && m.IsComplete);
        Assert.Contains(snapshot.Genesis.Milestones, m => m.Label.Contains("Ascension") && m.IsComplete);
    }

    [Fact]
    public void FromSummary_Wallet_UsesIdentitySlots()
    {
        var snapshot = AgentArenaProgressMapper.FromSummary(
            roundWins: 10,
            roundLosses: 2,
            genesisMinted: 100,
            ascensionMinted: 200,
            wallet: new AgentArenaWalletProgress
            {
                Wallet = "0xAeafcbae57d0cD306bfF176DA74343063C57dcd2",
                Wins = 6,
                GenesisWins = 1,
                AscensionWins = 5
            });

        Assert.Equal("Identity Progress", snapshot.Progress.Title);
        Assert.Equal(100.0, snapshot.Progress.Percent, 3);
        Assert.Equal("Ascension", snapshot.Genesis.Phase);
        Assert.Equal(100.0, snapshot.Genesis.Percent, 3);
        Assert.All(snapshot.Genesis.Milestones, m => Assert.True(m.IsComplete));
    }

    [Fact]
    public void ResolveGenesisPhase_AndStage_ScaleWithSupply()
    {
        Assert.Equal("Opening", AgentArenaProgressMapper.ResolveGenesisPhase(0));
        Assert.Equal(1, AgentArenaProgressMapper.ResolveGenesisStage(0));
        Assert.Equal("Closing", AgentArenaProgressMapper.ResolveGenesisPhase(90));
        Assert.Equal(5, AgentArenaProgressMapper.ResolveGenesisStage(90));
    }

    [Fact]
    public void FormatSourceCaption_RecognizesSources()
    {
        Assert.Equal(
            "Source · Agent Arena",
            ProgressGenesisFormatter.FormatSourceCaption(ProgressGenesisSourceKinds.AgentArena));
        Assert.Equal(
            "Source · Progress + Genesis",
            ProgressGenesisFormatter.FormatSourceCaption(ProgressGenesisSourceKinds.PersonalSystems));
    }
}
