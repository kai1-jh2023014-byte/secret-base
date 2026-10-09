namespace SecretBase.Core.Progress;

/// <summary>
/// Maps the personal <c>progress</c> learning system
/// (<see href="https://github.com/kai1-jh2023014-byte/progress"/>) into a Progress track.
/// Prefers the dashboard "Professional Readiness / 必須Skill" metrics when available.
/// </summary>
public static class ProgressLearningMapper
{
    public const string DefaultApiBase = "http://127.0.0.1:8001";

    /// <summary>
    /// Dashboard contract shown in Progress UI:
    /// Professional Readiness % ・ 必須Skill completed/total + Skill Map rows.
    /// </summary>
    public static ProgressTrack FromReadiness(ProgressReadinessSummary readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        var percent = Math.Clamp(readiness.ProfessionalReadinessPercent, 0, 100);
        var requiredTotal = Math.Max(0, readiness.RequiredSkillsTotal);
        var requiredDone = Math.Clamp(readiness.RequiredSkillsCompleted, 0, Math.Max(requiredTotal, readiness.RequiredSkillsCompleted));

        var status = requiredTotal > 0
            ? $"Professional Readiness {FormatPct(percent)} ・ 必須Skill {requiredDone}/{requiredTotal}"
            : $"Professional Readiness {FormatPct(percent)}";

        string? detail = null;
        if (readiness.SkillMap.Count > 0)
        {
            var top = readiness.SkillMap
                .OrderByDescending(s => s.Percent)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(s => $"{s.Name} {FormatPct(s.Percent)}");
            detail = string.Join(" · ", top);
        }
        else if (!string.IsNullOrWhiteSpace(readiness.Detail))
        {
            detail = readiness.Detail.Trim();
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

    /// <summary>
    /// Legacy Phase-1 fallback: unique passed problems ÷ catalog size.
    /// Only used when readiness/dashboard endpoints are unavailable.
    /// </summary>
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

        var status = totalProblems == 0
            ? (attempts.Count == 0 ? "No catalog yet" : $"{attempts.Count} attempts")
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

    public static ProgressTrack CreateOffline() =>
        new()
        {
            Title = "Progress",
            Percent = 0,
            Status = "progress API offline (start on :8001)",
            Detail = null
        };

    public static IReadOnlyList<GenesisMilestone> BuildReadinessMilestones(ProgressReadinessSummary readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        var total = Math.Max(0, readiness.RequiredSkillsTotal);
        var done = Math.Clamp(readiness.RequiredSkillsCompleted, 0, Math.Max(total, readiness.RequiredSkillsCompleted));
        return
        [
            GenesisMilestone.Create("First required skill", done >= 1),
            GenesisMilestone.Create("25% required skills", total > 0 && done * 4 >= total),
            GenesisMilestone.Create("50% required skills", total > 0 && done * 2 >= total),
            GenesisMilestone.Create("All required skills", total > 0 && done >= total)
        ];
    }

    public static bool IsPassed(string? result) =>
        string.Equals(result?.Trim(), "passed", StringComparison.OrdinalIgnoreCase);

    private static string FormatPct(double percent) =>
        Math.Clamp(percent, 0, 100).ToString("0.#") + "%";
}

/// <summary>Dashboard readiness metrics from Progress UI (あなたの現在地).</summary>
public sealed class ProgressReadinessSummary
{
    public double ProfessionalReadinessPercent { get; init; }

    public int RequiredSkillsCompleted { get; init; }

    public int RequiredSkillsTotal { get; init; }

    public string? Detail { get; init; }

    public List<ProgressSkillMapEntry> SkillMap { get; init; } = [];
}

public sealed class ProgressSkillMapEntry
{
    public string Name { get; init; } = string.Empty;

    public double Percent { get; init; }
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
