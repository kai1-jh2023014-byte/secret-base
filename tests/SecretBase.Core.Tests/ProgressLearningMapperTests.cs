using SecretBase.Core.Progress;

namespace SecretBase.Core.Tests;

public class ProgressLearningMapperTests
{
    [Fact]
    public void FromLearningData_ComputesSolvedPercent()
    {
        var problems = new[]
        {
            new ProgressProblemSummary { Id = "a", Title = "A" },
            new ProgressProblemSummary { Id = "b", Title = "B" },
            new ProgressProblemSummary { Id = "c", Title = "C" }
        };
        var attempts = new[]
        {
            new ProgressAttemptSummary
            {
                ProblemId = "a",
                ProblemTitle = "A",
                Result = "passed",
                Score = 1,
                CreatedAt = DateTimeOffset.Parse("2026-10-01T10:00:00Z")
            },
            new ProgressAttemptSummary
            {
                ProblemId = "b",
                ProblemTitle = "B",
                Result = "failed",
                CreatedAt = DateTimeOffset.Parse("2026-10-02T10:00:00Z")
            },
            new ProgressAttemptSummary
            {
                ProblemId = "a",
                ProblemTitle = "A",
                Result = "passed",
                CreatedAt = DateTimeOffset.Parse("2026-10-03T10:00:00Z")
            }
        };

        var track = ProgressLearningMapper.FromLearningData(problems, attempts);
        Assert.Equal("Progress", track.Title);
        Assert.Equal(100.0 / 3.0, track.Percent, 3);
        Assert.Contains("1/3", track.Status);
        Assert.Contains("Last: A", track.Detail);
    }

    [Fact]
    public void FromLearningData_OfflineEmptyCatalog()
    {
        var track = ProgressLearningMapper.FromLearningData([], []);
        Assert.Equal(0, track.Percent);
        Assert.Contains("progress", track.Status, StringComparison.OrdinalIgnoreCase);
    }
}

public class GenesisMusicLabMapperTests
{
    [Fact]
    public void FromStatus_RunningMusicLab()
    {
        var track = GenesisMusicLabMapper.FromStatus(new GenesisMusicLabStatus
        {
            IsInstalled = true,
            IsRunning = true,
            HasDesktopLaunchers = true,
            HasStatusFile = true,
            Percent = 70,
            Status = "Session open"
        });

        Assert.Equal("Genesis", track.Title);
        Assert.Equal("MusicLab", track.Phase);
        Assert.Equal(70, track.Percent);
        Assert.Equal("Session open", track.Status);
        Assert.Contains(track.Milestones, m => m.Label.Contains("running", StringComparison.OrdinalIgnoreCase) && m.IsComplete);
    }

    [Fact]
    public void Combine_UsesPersonalSystemsSource()
    {
        var snapshot = GenesisMusicLabMapper.Combine(
            new ProgressTrack { Percent = 40, Status = "ok" },
            new GenesisTrack { Percent = 25, Status = "ready" });
        Assert.Equal(ProgressGenesisSourceKinds.PersonalSystems, snapshot.SourceKind);
        Assert.Equal(
            "Source · Progress + Genesis",
            ProgressGenesisFormatter.FormatSourceCaption(snapshot.SourceKind));
    }
}
