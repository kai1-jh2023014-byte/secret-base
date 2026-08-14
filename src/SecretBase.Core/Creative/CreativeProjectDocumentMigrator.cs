namespace SecretBase.Core.Creative;

/// <summary>
/// Migrates creative/projects.json documents toward <see cref="CreativeProjectDocument.CurrentSchemaVersion"/>.
/// v1 → v2 adds Dashboard fields with safe defaults (no data loss).
/// </summary>
public static class CreativeProjectDocumentMigrator
{
    public static CreativeProjectDocument MigrateToCurrent(CreativeProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Projects ??= [];

        if (document.SchemaVersion < 1)
        {
            document.SchemaVersion = 1;
        }

        foreach (var project in document.Projects)
        {
            project.Resources ??= [];
            project.RecentItems ??= [];
            foreach (var resource in project.Resources)
            {
                // IsQuickAction defaults false for v1 payloads — no explicit set needed.
                _ = resource.IsQuickAction;
            }
        }

        if (document.SchemaVersion < CreativeProjectDocument.CurrentSchemaVersion)
        {
            document.SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion;
        }

        return document;
    }
}
