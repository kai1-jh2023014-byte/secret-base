using SecretBase.Core.Progress;

namespace SecretBase.Core.Tests;

public class ProgressGenesisHistoryTests
{
    [Fact]
    public void LatestSnapshot_ReturnsMostRecent()
    {
        var doc = new ProgressGenesisHistoryDocument
        {
            Entries =
            [
                new ProgressGenesisHistoryEntry
                {
                    RecordedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    Snapshot = new ProgressGenesisSnapshot
                    {
                        Progress = new ProgressTrack { Percent = 10 },
                        Genesis = new GenesisTrack { Percent = 5, Phase = "MusicLab" }
                    }
                },
                new ProgressGenesisHistoryEntry
                {
                    RecordedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    Snapshot = new ProgressGenesisSnapshot
                    {
                        Progress = new ProgressTrack { Percent = 42 },
                        Genesis = new GenesisTrack { Percent = 12, Phase = "MusicLab" }
                    }
                }
            ]
        };

        var latest = doc.LatestSnapshot();
        Assert.NotNull(latest);
        Assert.Equal(42, latest!.Progress.Percent);
        Assert.Equal(12, latest.Genesis.Percent);
    }

    [Fact]
    public void IsMeaningfullyDifferent_IgnoresTinyPercentNoise()
    {
        var a = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Percent = 10, Status = "ok" },
            Genesis = new GenesisTrack { Percent = 5, Status = "ready", Phase = "MusicLab" }
        };
        var b = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Percent = 10.02, Status = "ok" },
            Genesis = new GenesisTrack { Percent = 5.01, Status = "ready", Phase = "MusicLab" }
        };
        Assert.False(ProgressGenesisHistoryComparer.IsMeaningfullyDifferent(a, b));
    }

    [Fact]
    public void IsMeaningfullyDifferent_DetectsPercentChange()
    {
        var a = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Percent = 10 },
            Genesis = new GenesisTrack { Percent = 0, Phase = "MusicLab" }
        };
        var b = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Percent = 15 },
            Genesis = new GenesisTrack { Percent = 0, Phase = "MusicLab" }
        };
        Assert.True(ProgressGenesisHistoryComparer.IsMeaningfullyDifferent(a, b));
        Assert.True(ProgressGenesisHistoryComparer.IsMeaningfullyDifferent(null, b));
    }
}
