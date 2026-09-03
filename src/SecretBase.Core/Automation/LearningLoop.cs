using SecretBase.Core.Intent;

namespace SecretBase.Core.Automation;

public sealed class LearningInsight
{
    public DetectedIntentKind Intent { get; init; }

    public int Accepts { get; init; }

    public int Dismissals { get; init; }

    public double AcceptanceRate { get; init; }

    public string Adjustment { get; init; } = "unchanged";

    public bool PrivilegeUnchanged { get; init; } = true;
}

/// <summary>
/// Feedback → aggregation → ranking adjustment. Never Confirmation → Auto Action.
/// </summary>
public static class LearningLoop
{
    public static IReadOnlyList<LearningInsight> Detect(IReadOnlyList<AutomationFeedback> feedback)
    {
        feedback ??= [];
        return feedback
            .GroupBy(item => item.Intent)
            .Select(group =>
            {
                var accepts = group.Count(item => item.Accepted);
                var dismissals = group.Count(item => !item.Accepted);
                var rate = accepts + dismissals == 0 ? 0.5 : accepts / (double)(accepts + dismissals);
                var adjustment = dismissals >= 3 && rate < 0.35
                    ? "reduce ranking"
                    : accepts >= 3 && rate >= 0.7
                        ? "increase ranking"
                        : "unchanged";
                return new LearningInsight
                {
                    Intent = group.Key,
                    Accepts = accepts,
                    Dismissals = dismissals,
                    AcceptanceRate = Math.Round(rate, 2),
                    Adjustment = adjustment,
                    PrivilegeUnchanged = true
                };
            })
            .OrderByDescending(item => item.Accepts + item.Dismissals)
            .ToList();
    }

    public static bool MayEscalatePrivilege => LearningPolicy.MayEscalatePrivilege;
}
