using System.Globalization;

namespace SecretBase.Core.Progress;

/// <summary>Pure display helpers for the Progress / Genesis widget.</summary>
public static class ProgressGenesisFormatter
{
    public static string FormatPercent(double percent) =>
        Math.Clamp(percent, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string FormatProgressHeadline(ProgressTrack progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        progress.Normalize();
        return $"{progress.Title} · {FormatPercent(progress.Percent)}";
    }

    public static string FormatGenesisHeadline(GenesisTrack genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        genesis.Normalize();
        return $"{genesis.Title} · {genesis.Phase} · Stage {genesis.Stage}/{genesis.StageCount}";
    }

    public static string FormatGenesisPercentLine(GenesisTrack genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        genesis.Normalize();
        var milestones = genesis.Milestones.Count == 0
            ? string.Empty
            : $" · {genesis.CompletedMilestoneCount}/{genesis.Milestones.Count} milestones";
        return $"{FormatPercent(genesis.Percent)}{milestones}";
    }

    public static string FormatUpdatedAt(DateTimeOffset updatedAt, DateTimeOffset? now = null)
    {
        if (updatedAt == default)
        {
            return "Updated —";
        }

        var reference = now ?? DateTimeOffset.Now;
        var delta = reference - updatedAt;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta < TimeSpan.FromMinutes(1))
        {
            return "Updated just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)delta.TotalMinutes);
            return $"Updated {minutes}m ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)delta.TotalHours);
            return $"Updated {hours}h ago";
        }

        return "Updated " + updatedAt.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
    }

    public static string FormatSourceCaption(string? sourceKind) =>
        sourceKind?.Trim().ToLowerInvariant() switch
        {
            ProgressGenesisSourceKinds.Http => "Source · HTTP JSON",
            ProgressGenesisSourceKinds.Memory => "Source · memory",
            _ => "Source · local JSON"
        };
}
