using SecretBase.Core.Creative;

namespace SecretBase.Core.Files;

/// <summary>
/// Suggests unused registered files. Never crawls the disk and never deletes.
/// </summary>
public sealed record FileCleanupCandidate(
    string Name,
    string Reason,
    string? ProjectName);

public static class FileIntelligence
{
    public static readonly TimeSpan DefaultUnusedAge = TimeSpan.FromDays(30);

    public static IReadOnlyList<FileCleanupCandidate> SuggestCleanup(
        IReadOnlyList<CreativeProject> projects,
        DateTimeOffset now,
        TimeSpan? unusedAge = null)
    {
        projects ??= [];
        var age = unusedAge ?? DefaultUnusedAge;
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

            foreach (var resource in project.Resources)
            {
                if (resource.Kind != CreativeProjectResourceKind.File
                    || string.IsNullOrWhiteSpace(resource.Name))
                {
                    continue;
                }

                if (!recentNames.Contains(resource.Name))
                {
                    candidates.Add(new FileCleanupCandidate(
                        resource.Name,
                        "Not opened from Secret Base recently",
                        project.Name));
                    continue;
                }

                if (lastOpened is not null && now - lastOpened.Value > age)
                {
                    candidates.Add(new FileCleanupCandidate(
                        resource.Name,
                        $"Not used in {(int)(now - lastOpened.Value).TotalDays} days",
                        project.Name));
                }
            }
        }

        return candidates
            .GroupBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(12)
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
            lines.Add($"○ {candidate.Name}{project}");
        }

        lines.Add(string.Empty);
        lines.Add("Secret Base will not delete files on disk. [Review]");
        return string.Join(Environment.NewLine, lines);
    }
}
