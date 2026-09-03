using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Observation;

/// <summary>
/// Maps sanitized OS observations to Secret Base activity. Drops unmatched browser noise.
/// </summary>
public static class ObservationNormalizer
{
    public static readonly TimeSpan DedupWindow = TimeSpan.FromSeconds(30);

    public static ActivityEvent? ToActivity(
        ObservationEvent observation,
        IReadOnlyList<CustomApp> apps,
        IReadOnlyList<CreativeProject> projects,
        ActivityEvent? last = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        apps ??= [];
        projects ??= [];

        var project = MatchProject(observation, projects);
        var registeredApp = MatchApp(observation, apps);
        var kind = observation.Kind switch
        {
            ObservationKind.IdleStarted => ActivityKind.IdleStarted,
            ObservationKind.IdleEnded or ObservationKind.UserActivityResumed => ActivityKind.IdleEnded,
            ObservationKind.SystemStartup or ObservationKind.SystemResume => ActivityKind.SystemObserved,
            ObservationKind.ApplicationActivated => ActivityKind.ApplicationOpened,
            _ => ActivityKind.ApplicationOpened
        };

        if (kind is ActivityKind.ApplicationOpened
            && registeredApp is null
            && project is null
            && ObservationSanitizer.IsBrowser(observation.ApplicationName))
        {
            return null;
        }

        var title = kind switch
        {
            ActivityKind.IdleStarted => "Idle started",
            ActivityKind.IdleEnded => "Activity resumed",
            ActivityKind.SystemObserved => observation.Kind == ObservationKind.SystemStartup
                ? "System startup"
                : "System resume",
            _ => registeredApp?.Name
                 ?? observation.ApplicationName
                 ?? observation.WindowTitle
                 ?? "Application"
        };

        if (last is not null
            && last.Kind == kind
            && string.Equals(last.Title, title, StringComparison.OrdinalIgnoreCase)
            && observation.At - last.At < DedupWindow)
        {
            return null;
        }

        return new ActivityEvent
        {
            Kind = kind,
            At = observation.At,
            Title = title,
            ProjectName = project?.Name,
            Detail = observation.WindowTitle
        };
    }

    public static CreativeProject? MatchProject(ObservationEvent observation, IReadOnlyList<CreativeProject> projects)
    {
        var haystack = $"{observation.ApplicationName} {observation.WindowTitle}";
        return projects.FirstOrDefault(project =>
            !string.IsNullOrWhiteSpace(project.Name)
            && haystack.Contains(project.Name, StringComparison.OrdinalIgnoreCase));
    }

    public static CustomApp? MatchApp(ObservationEvent observation, IReadOnlyList<CustomApp> apps)
    {
        if (string.IsNullOrWhiteSpace(observation.ApplicationName))
        {
            return null;
        }

        return apps.FirstOrDefault(app =>
            !string.IsNullOrWhiteSpace(app.Name)
            && (string.Equals(app.Name, observation.ApplicationName, StringComparison.OrdinalIgnoreCase)
                || observation.ApplicationName.Contains(app.Name, StringComparison.OrdinalIgnoreCase)
                || app.Name.Contains(observation.ApplicationName, StringComparison.OrdinalIgnoreCase)));
    }
}
