using SecretBase.Core.Creative;

namespace SecretBase.Core.Files;

public enum FileCandidateKind
{
    Unused = 0,
    Recent = 1,
    Relevant = 2,
    Duplicate = 3,
    Temporary = 4,
    Important = 5,
    ProjectRelated = 6,
    OldExport = 7,
    BuildArtifact = 8
}

/// <summary>
/// Suggests registered-file candidates. Never crawls the disk and never deletes.
/// The three-argument constructor stays compatible with existing callers.
/// </summary>
public sealed record FileCleanupCandidate(
    string Name,
    string Reason,
    string? ProjectName,
    FileCandidateKind Kind = FileCandidateKind.Unused);

public static class FileIntelligence
{
    public static readonly TimeSpan DefaultUnusedAge = TimeSpan.FromDays(30);
    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(7);

    public static IReadOnlyList<FileCleanupCandidate> SuggestCleanup(
        IReadOnlyList<CreativeProject> projects,
        DateTimeOffset now,
        TimeSpan? unusedAge = null)
    {
        return Classify(projects, now, currentProjectName: null, unusedAge)
            .Where(candidate => candidate.Kind is FileCandidateKind.Unused
                or FileCandidateKind.Temporary
                or FileCandidateKind.OldExport
                or FileCandidateKind.BuildArtifact)
            .Take(12)
            .ToList();
    }

    public static IReadOnlyList<FileCleanupCandidate> Classify(
        IReadOnlyList<CreativeProject> projects,
        DateTimeOffset now,
        string? currentProjectName = null,
        TimeSpan? unusedAge = null)
    {
        projects ??= [];
        var age = unusedAge ?? DefaultUnusedAge;
        var nameOwners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            foreach (var resource in project.Resources.Where(item =>
                         item.Kind == CreativeProjectResourceKind.File
                         && !string.IsNullOrWhiteSpace(item.Name)))
            {
                if (!nameOwners.TryGetValue(resource.Name, out var owners))
                {
                    owners = [];
                    nameOwners[resource.Name] = owners;
                }

                if (!owners.Contains(project.Name, StringComparer.OrdinalIgnoreCase))
                {
                    owners.Add(project.Name);
                }
            }
        }

        var candidates = new List<FileCleanupCandidate>();
        foreach (var project in projects)
        {
            var recentNames = new HashSet<string>(
                project.RecentItems
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .Select(item => item.Name),
                StringComparer.OrdinalIgnoreCase);
            var lastOpened = project.RecentItems
                .Select(item => (DateTimeOffset?)item.OpenedAt)
                .DefaultIfEmpty()
                .Max();
            var isCurrent = !string.IsNullOrWhiteSpace(currentProjectName)
                            && string.Equals(project.Name, currentProjectName, StringComparison.OrdinalIgnoreCase);

            foreach (var resource in project.Resources)
            {
                if (resource.Kind != CreativeProjectResourceKind.File
                    || string.IsNullOrWhiteSpace(resource.Name))
                {
                    continue;
                }

                var kind = ClassifyOne(
                    resource.Name,
                    recentNames.Contains(resource.Name),
                    lastOpened,
                    now,
                    age,
                    isCurrent,
                    nameOwners.TryGetValue(resource.Name, out var owners) && owners.Count > 1);
                var reason = kind switch
                {
                    FileCandidateKind.Important => "Looks like a durable project document",
                    FileCandidateKind.Relevant or FileCandidateKind.ProjectRelated =>
                        "Related to the current project",
                    FileCandidateKind.Recent => "Opened from Secret Base recently",
                    FileCandidateKind.Duplicate => "Same name registered in more than one project",
                    FileCandidateKind.Temporary => "Looks temporary",
                    FileCandidateKind.OldExport => "Looks like an old export",
                    FileCandidateKind.BuildArtifact => "Looks like a build artifact",
                    _ => lastOpened is not null && now - lastOpened.Value > age
                        ? $"Not used in {(int)(now - lastOpened.Value).TotalDays} days"
                        : "Not opened from Secret Base recently"
                };
                candidates.Add(new FileCleanupCandidate(resource.Name, reason, project.Name, kind));
            }
        }

        return candidates
            .GroupBy(candidate => candidate.Name + "|" + candidate.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
    }

    public static string FormatSuggestion(IReadOnlyList<FileCleanupCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "No unused registered files to review.";
        }

        var lines = new List<string>
        {
            "Possible cleanup",
            string.Empty,
            "These registered files haven't been used recently:",
            string.Empty
        };
        foreach (var candidate in candidates)
        {
            var project = string.IsNullOrWhiteSpace(candidate.ProjectName)
                ? string.Empty
                : $" ({candidate.ProjectName})";
            var kind = candidate.Kind == FileCandidateKind.Unused
                ? string.Empty
                : $" [{candidate.Kind}]";
            lines.Add($"○ {candidate.Name}{project}{kind}");
        }

        lines.Add(string.Empty);
        lines.Add("Secret Base will not delete files on disk. [Review]");
        return string.Join(Environment.NewLine, lines);
    }

    private static FileCandidateKind ClassifyOne(
        string name,
        bool recentlyOpened,
        DateTimeOffset? lastOpened,
        DateTimeOffset now,
        TimeSpan unusedAge,
        bool currentProject,
        bool duplicate)
    {
        if (LooksTemporary(name))
        {
            return FileCandidateKind.Temporary;
        }

        if (LooksExport(name))
        {
            return FileCandidateKind.OldExport;
        }

        if (LooksArtifact(name))
        {
            return FileCandidateKind.BuildArtifact;
        }

        if (LooksImportant(name))
        {
            return FileCandidateKind.Important;
        }

        if (duplicate)
        {
            return FileCandidateKind.Duplicate;
        }

        if (currentProject && recentlyOpened)
        {
            return FileCandidateKind.ProjectRelated;
        }

        if (recentlyOpened && lastOpened is not null && now - lastOpened.Value <= RecentWindow)
        {
            return FileCandidateKind.Recent;
        }

        return FileCandidateKind.Unused;
    }

    private static bool LooksExport(string name) =>
        name.Contains("export", StringComparison.OrdinalIgnoreCase)
        || name.Contains("-copy", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool LooksArtifact(string name) =>
        name.Contains("bin\\", StringComparison.OrdinalIgnoreCase)
        || name.Contains("bin/", StringComparison.OrdinalIgnoreCase)
        || name.Contains("obj\\", StringComparison.OrdinalIgnoreCase)
        || name.Contains("obj/", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);

    private static bool LooksTemporary(string name) =>
        name.Contains(".tmp", StringComparison.OrdinalIgnoreCase)
        || name.Contains(".bak", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("tmp", StringComparison.OrdinalIgnoreCase)
        || name.Contains("temp", StringComparison.OrdinalIgnoreCase);

    private static bool LooksImportant(string name) =>
        name.Contains("readme", StringComparison.OrdinalIgnoreCase)
        || name.Contains("agents", StringComparison.OrdinalIgnoreCase)
        || name.Contains("important", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
}
