using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Creative;

namespace SecretBase.Core.State;

/// <summary>Practical PC habits only. Not a personality profile.</summary>
public sealed class UserModelSnapshot
{
    public List<string> FrequentProjects { get; set; } = [];

    public List<string> FrequentApps { get; set; } = [];

    public int PreferredFocusMinutes { get; set; } = 25;

    public int TypicalWorkStartHour { get; set; } = 9;

    public int TypicalWorkEndHour { get; set; } = 18;

    public string? PreferredMusicProvider { get; set; }
}

public static class UserModelBuilder
{
    public static UserModelSnapshot From(
        IReadOnlyList<ActivityEvent> activities,
        IReadOnlyList<CreativeProject> projects,
        IReadOnlyList<CustomApp> apps,
        int preferredFocusMinutes = 25)
    {
        activities ??= [];
        var projectHits = activities
            .Where(item => !string.IsNullOrWhiteSpace(item.ProjectName))
            .GroupBy(item => item.ProjectName!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(5)
            .ToList();
        if (projectHits.Count == 0)
        {
            projectHits = projects.Where(p => p.IsFavorite).Select(p => p.Name).Take(3).ToList();
        }

        var appHits = activities
            .Where(item => item.Kind == ActivityKind.ApplicationOpened && !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(5)
            .ToList();
        if (appHits.Count == 0)
        {
            appHits = apps.Take(3).Select(app => app.Name).ToList();
        }

        var hours = activities
            .Where(item => ActivityNormalizer.IsWork(item.Kind))
            .Select(item => item.At.Hour)
            .ToList();
        var start = hours.Count == 0 ? 9 : hours.Min();
        var end = hours.Count == 0 ? 18 : Math.Max(start + 1, hours.Max());

        return new UserModelSnapshot
        {
            FrequentProjects = projectHits,
            FrequentApps = appHits,
            PreferredFocusMinutes = preferredFocusMinutes,
            TypicalWorkStartHour = Math.Clamp(start, 6, 12),
            TypicalWorkEndHour = Math.Clamp(end, 13, 22)
        };
    }
}
