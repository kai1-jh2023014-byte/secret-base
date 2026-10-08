using SecretBase.Core.Progress;

namespace SecretBase.Core.Tests;

public class ProgressLearningMapperTests
{
    [Fact]
    public void FromReadiness_MatchesDashboardZeroState()
    {
        var track = ProgressLearningMapper.FromReadiness(new ProgressReadinessSummary
        {
            ProfessionalReadinessPercent = 0,
            RequiredSkillsCompleted = 0,
            RequiredSkillsTotal = 29,
            SkillMap =
            [
                new ProgressSkillMapEntry { Name = "Programming Fundamentals", Percent = 0 },
                new ProgressSkillMapEntry { Name = "Python", Percent = 0 }
            ]
        });

        Assert.Equal(0, track.Percent);
        Assert.Contains("Professional Readiness 0%", track.Status);
        Assert.Contains("必須Skill 0/29", track.Status);
        Assert.Contains("Programming Fundamentals 0%", track.Detail);
    }

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
    public void CreateOffline_IsZeroPercent()
    {
        var track = ProgressLearningMapper.CreateOffline();
        Assert.Equal(0, track.Percent);
        Assert.Contains("offline", track.Status, StringComparison.OrdinalIgnoreCase);
    }
}

public class GenesisMusicLabMapperTests
{
    [Fact]
    public void FromStatus_DoesNotInventPercentFromLaunchers()
    {
        var track = GenesisMusicLabMapper.FromStatus(new GenesisMusicLabStatus
        {
            IsInstalled = true,
            IsRunning = true,
            HasDesktopLaunchers = true,
            Percent = null
        });

        Assert.Equal(0, track.Percent);
        Assert.Contains("running", track.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromStatus_UsesExplicitPercentOnly()
    {
        var track = GenesisMusicLabMapper.FromStatus(new GenesisMusicLabStatus
        {
            IsInstalled = true,
            Percent = 10,
            Status = "Session"
        });

        Assert.Equal(10, track.Percent);
        Assert.Equal("Session", track.Status);
    }

    [Fact]
    public void Combine_UsesPersonalSystemsSource()
    {
        var snapshot = GenesisMusicLabMapper.Combine(
            new ProgressTrack { Percent = 0, Status = "Professional Readiness 0% ・ 必須Skill 0/29" },
            new GenesisTrack { Percent = 0, Status = "ready" });
        Assert.Equal(ProgressGenesisSourceKinds.PersonalSystems, snapshot.SourceKind);
        Assert.Equal(0, snapshot.Progress.Percent);
    }

    [Fact]
    public void MinimalLines_AreCompact()
    {
        Assert.Equal(
            "Progress  0%",
            ProgressGenesisFormatter.FormatMinimalProgressLine(new ProgressTrack { Percent = 0 }));
        Assert.Equal(
            "MusicLab  12%",
            ProgressGenesisFormatter.FormatMinimalGenesisLine(new GenesisTrack
            {
                Phase = "MusicLab",
                Percent = 12
            }));
    }
}
