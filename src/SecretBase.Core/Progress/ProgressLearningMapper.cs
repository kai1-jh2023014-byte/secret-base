namespace SecretBase.Core.Progress;

/// <summary>
/// Maps the personal <c>progress</c> learning API
/// (<see href="https://github.com/kai1-jh2023014-byte/progress"/>) into a Progress track.
/// </summary>
public static class ProgressLearningMapper
{
    public const string DefaultApiBase = "http://127.0.0.1:8001";

    public static ProgressTrack FromLearningData(
        IReadOnlyList<ProgressProblemSummary> problems,
        IReadOnlyList<ProgressAttemptSummary> attempts)
    {
        problems ??= [];
        attempts ??= [];

        var totalProblems = problems.Count;
        var solvedIds = attempts
            .Where(a => IsPassed(a.Result))
            .Select(a => a.ProblemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var solvedCount = solvedIds.Count;
        var percent = totalProblems == 0
            ? 0
            : 100.0 * solvedCount / totalProblems;

        var recent = attempts
            .OrderByDescending(a => a.CreatedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

        var passedAttempts = attempts.Count(a => IsPassed(a.Result));
        var status = totalProblems == 0
            ? (attempts.Count == 0 ? "No problems yet — is progress running?" : $"{attempts.Count} attempts")
            : $"{solvedCount}/{totalProblems} problems solved";

        string? detail = null;
        if (recent is not null)
        {
            var title = string.IsNullOrWhiteSpace(recent.ProblemTitle)
                ? recent.ProblemId
                : recent.ProblemTitle;
            detail = $"Last: {title} · {recent.Result}" +
                     (recent.Score is null ? string.Empty : $" ({recent.Score:0.##})");
        }
        else if (totalProblems > 0)
        {
            detail = $"{passedAttempts} passing attempts logged";
        }

        var track = new ProgressTrack
        {
            Title = "Progress",
            Percent = percent,
            Status = status,
            Detail = detail
        };
        track.Normalize();
        return track;
    }

    public static IReadOnlyList<GenesisMilestone> BuildLearningMilestones(
        IReadOnlyList<ProgressProblemSummary> problems,
        IReadOnlyList<ProgressAttemptSummary> attempts)
    {
        var solved = attempts.Count(a => IsPassed(a.Result) && !string.IsNullOrWhiteSpace(a.ProblemId));
        var uniqueSolved = attempts
            .Where(a => IsPassed(a.Result))
            .Select(a => a.ProblemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var total = problems.Count;

        return
        [
            GenesisMilestone.Create("First attempt", attempts.Count >= 1),
            GenesisMilestone.Create("First problem solved", uniqueSolved >= 1),
            GenesisMilestone.Create("Three problems solved", uniqueSolved >= 3),
            GenesisMilestone.Create("Half the catalog", total > 0 && uniqueSolved * 2 >= total),
            GenesisMilestone.Create("All problems solved", total > 0 && uniqueSolved >= total)
        ];
    }

    public static bool IsPassed(string? result) =>
        string.Equals(result?.Trim(), "passed", StringComparison.OrdinalIgnoreCase);
}

public sealed class ProgressProblemSummary
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Difficulty { get; init; } = string.Empty;
}

public sealed class ProgressAttemptSummary
{
    public string Id { get; init; } = string.Empty;

    public string ProblemId { get; init; } = string.Empty;

    public string ProblemTitle { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public double? Score { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }
}
