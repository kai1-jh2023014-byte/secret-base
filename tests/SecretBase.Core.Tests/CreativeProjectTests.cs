using System.Reflection;
using System.Text.Json;
using SecretBase.Core.Creative;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Tests;

public class CreativeProjectValidatorTests
{
    [Fact]
    public void TryValidateName_RequiresNonEmptyBoundedName()
    {
        Assert.False(CreativeProjectValidator.TryValidateName(" ", out _, out _));
        Assert.False(CreativeProjectValidator.TryValidateName(new string('a', CreativeProjectValidator.MaxNameLength + 1), out _, out _));
        Assert.True(CreativeProjectValidator.TryValidateName("  My Song  ", out var name, out _));
        Assert.Equal("My Song", name);
    }

    [Fact]
    public void TryNormalizeRootFolder_AllowsEmpty_AndAbsoluteFolder()
    {
        Assert.True(CreativeProjectValidator.TryNormalizeRootFolder(null, out var empty, out _));
        Assert.Null(empty);
        Assert.True(CreativeProjectValidator.TryNormalizeRootFolder(@"D:\Music\MyFirstSong\", out var root, out _));
        Assert.Equal(@"D:\Music\MyFirstSong", root);
        Assert.False(CreativeProjectValidator.TryNormalizeRootFolder("relative", out _, out _));
    }

    [Fact]
    public void TryNormalizeResource_FileFolderAndHttpsLink()
    {
        Assert.True(CreativeProjectValidator.TryNormalizeResource(
            CreativeProjectResourceKind.File,
            "Song",
            @"D:\Music\Song1\Song1.cwp",
            out var file,
            out _));
        Assert.Equal(CreativeProjectResourceKind.File, file!.Kind);

        Assert.True(CreativeProjectValidator.TryNormalizeResource(
            CreativeProjectResourceKind.Folder,
            "Lyrics",
            @"D:\Music\Song1\",
            out var folder,
            out _));
        Assert.Equal(CreativeProjectResourceKind.Folder, folder!.Kind);

        Assert.True(CreativeProjectValidator.TryNormalizeResource(
            CreativeProjectResourceKind.ExternalLink,
            "YouTube",
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            out var link,
            out _));
        Assert.Equal(CreativeProjectResourceKind.ExternalLink, link!.Kind);
        Assert.StartsWith("https://", link.Target, StringComparison.OrdinalIgnoreCase);

        Assert.False(CreativeProjectValidator.TryNormalizeResource(
            CreativeProjectResourceKind.ExternalLink,
            "Bad",
            "ftp://evil",
            out _,
            out _));
    }

    [Fact]
    public void TryNormalizeNotes_BoundsLength()
    {
        Assert.True(CreativeProjectValidator.TryNormalizeNotes(null, out var empty, out _));
        Assert.Null(empty);
        Assert.True(CreativeProjectValidator.TryNormalizeNotes("サビをもう少し盛り上げる", out var notes, out _));
        Assert.Equal("サビをもう少し盛り上げる", notes);
        Assert.False(CreativeProjectValidator.TryNormalizeNotes(
            new string('x', CreativeProjectValidator.MaxNotesLength + 1),
            out _,
            out _));
    }
}

public class CreativeProjectServiceTests
{
    [Fact]
    public void Create_Update_Favorite_DeleteRegistration_NeverTouchesFilesystemApis()
    {
        var store = new MemoryCreativeProjectStore();
        var service = new CreativeProjectService(store);

        Assert.True(service.TryCreate(
            "My First Song",
            "My first original song.",
            CreativeProjectType.Music,
            @"D:\Music\MyFirstSong\",
            out var project,
            out _));
        Assert.Equal(CreativeProjectType.Music, project!.ProjectType);
        Assert.Equal(@"D:\Music\MyFirstSong", project.RootFolder);

        Assert.True(service.TryUpdate(
            project.Id,
            "My First Song (v2)",
            "Updated",
            CreativeProjectType.Music,
            @"D:\Music\MyFirstSong",
            out var updated,
            out _));
        Assert.Equal("My First Song (v2)", updated!.Name);

        Assert.True(service.TryToggleFavorite(project.Id, out var fav, out _));
        Assert.True(fav!.IsFavorite);
        Assert.Single(service.GetFavorites());

        Assert.True(service.TryAddResource(
            project.Id,
            CreativeProjectResourceKind.File,
            "Project File",
            @"D:\Music\MyFirstSong\MyFirstSong.cwp",
            out _,
            out _));
        Assert.True(service.TryAddResource(
            project.Id,
            CreativeProjectResourceKind.ExternalLink,
            "YouTube",
            "https://youtube.com/watch?v=abc",
            out var withLink,
            out _));
        Assert.Equal(2, withLink!.Resources.Count);

        Assert.True(service.TryDeleteRegistration(project.Id, out _));
        Assert.Empty(service.Projects);

        // Security surface: registration delete only — no FS mutate method names.
        var names = typeof(CreativeProjectService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(CreativeProjectService.TryDeleteRegistration), names);
        Assert.Contains(nameof(CreativeProjectService.TryRemoveResource), names);
        Assert.DoesNotContain("TryDeleteFile", names);
        Assert.DoesNotContain("TryDeleteFolder", names);
        Assert.DoesNotContain("TryMove", names);
        Assert.DoesNotContain("TryRename", names);
        Assert.DoesNotContain("TryExecute", names);
        Assert.DoesNotContain("TryRunProcess", names);
        Assert.DoesNotContain("TryRunPowerShell", names);
    }

    [Fact]
    public void Dashboard_Notes_QuickAction_AndRecentItems()
    {
        var service = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(service.TryCreate(
            "My First Song",
            "Original",
            CreativeProjectType.Music,
            @"D:\Music\MyFirstSong",
            out var project,
            out _));
        Assert.True(service.TryAddResource(
            project!.Id,
            CreativeProjectResourceKind.File,
            "Lyrics",
            @"D:\Music\MyFirstSong\lyrics.txt",
            out _,
            out _));
        var resourceId = service.FindById(project.Id)!.Resources[0].Id;

        Assert.True(service.TrySaveNotes(project.Id, "サビをもう少し盛り上げる", out var noted, out _));
        Assert.Equal("サビをもう少し盛り上げる", noted!.Notes);

        Assert.True(service.TryToggleResourceQuickAction(project.Id, resourceId, out var pinned, out _));
        Assert.True(pinned!.Resources[0].IsQuickAction);
        Assert.Single(service.GetQuickActions(project.Id));

        Assert.True(service.TryRecordResourceOpened(
            project.Id, resourceId, DateTimeOffset.UtcNow, out var opened, out _, out _));
        Assert.Single(opened!.RecentItems);
        Assert.Equal("Lyrics", opened.RecentItems[0].Name);

        Assert.True(service.TryRecordRootOpened(project.Id, DateTimeOffset.UtcNow, out var withRoot, out _));
        Assert.Equal(2, withRoot!.RecentItems.Count);
        Assert.True(withRoot.RecentItems[0].IsRoot);

        // Remove resource registration does not expose FS delete; also clears recent key.
        Assert.True(service.TryRemoveResource(project.Id, resourceId, out var afterRemove, out _));
        Assert.Empty(afterRemove!.Resources);
        Assert.DoesNotContain(afterRemove.RecentItems, r => r.Key == resourceId);
    }

    [Fact]
    public void MissingRootFolder_IsAllowed_AndOpenMarksLastOpened()
    {
        var service = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(service.TryCreate("Draft", null, CreativeProjectType.Writing, rootFolder: null, out var project, out _));
        Assert.Null(project!.RootFolder);
        Assert.True(service.TryMarkOpened(project.Id, DateTimeOffset.UtcNow, out var opened, out _));
        Assert.NotNull(opened!.LastOpened);
    }

    [Fact]
    public void Serialization_RoundTrip_ViaMemoryStore()
    {
        var store = new MemoryCreativeProjectStore();
        var service = new CreativeProjectService(store);
        Assert.True(service.TryCreate("Secret Base", "App", CreativeProjectType.Programming, @"C:\src\secret-base", out var p, out _));
        Assert.True(service.TryAddResource(
            p!.Id,
            CreativeProjectResourceKind.Folder,
            "docs",
            @"C:\src\secret-base\docs",
            out _,
            out _));
        Assert.True(service.TrySaveNotes(p.Id, "ship dashboard", out _, out _));

        var reloaded = new CreativeProjectService(store);
        Assert.Single(reloaded.Projects);
        Assert.Equal("Secret Base", reloaded.Projects[0].Name);
        Assert.Equal(CreativeProjectType.Programming, reloaded.Projects[0].ProjectType);
        Assert.Single(reloaded.Projects[0].Resources);
        Assert.Equal("ship dashboard", reloaded.Projects[0].Notes);
        Assert.Equal(CreativeProjectDocument.CurrentSchemaVersion, store.LoadOrCreate().SchemaVersion);
    }

    [Fact]
    public void Document_Json_IncludesSchemaVersion_AndMigratesV1ToV2()
    {
        var doc = new CreativeProjectDocument
        {
            SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion,
            Projects =
            [
                new CreativeProject
                {
                    Name = "Clip",
                    ProjectType = CreativeProjectType.Video,
                    RootFolder = @"D:\Video\Clip",
                    Notes = "cut tighter"
                }
            ]
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(doc, options);
        Assert.Contains("\"schemaVersion\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"notes\":\"cut tighter\"", json, StringComparison.Ordinal);

        var v1 = new CreativeProjectDocument
        {
            SchemaVersion = 1,
            Projects =
            [
                new CreativeProject
                {
                    Name = "Legacy",
                    ProjectType = CreativeProjectType.Other,
                    Resources = [new CreativeProjectResource { Name = "a", Kind = CreativeProjectResourceKind.File, Target = @"D:\a.txt" }]
                }
            ]
        };
        var migrated = CreativeProjectDocumentMigrator.MigrateToCurrent(v1);
        Assert.Equal(2, migrated.SchemaVersion);
        Assert.NotNull(migrated.Projects[0].RecentItems);
        Assert.False(migrated.Projects[0].Resources[0].IsQuickAction);
    }
}

public class CreativeProjectCommandTests
{
    [Fact]
    public void Dashboard_Open_Notes_QuickAction_Recent_AndDeleteRegistration_Commands()
    {
        var workspace = new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore());
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Illustration",
            null,
            CreativeProjectType.Design,
            @"D:\Art\Illustration",
            out var project,
            out _));
        Assert.True(projects.TryAddResource(
            project!.Id,
            CreativeProjectResourceKind.ExternalLink,
            "Drive",
            "https://docs.google.com/document/d/x",
            out _,
            out _));
        var resourceId = projects.FindById(project.Id)!.Resources[0].Id;

        var commands = new CreativeCommandService(workspace, projects);

        var search = commands.Execute(CreativeCommand.SearchProjects("Illus"));
        Assert.True(search.Succeeded);
        Assert.Single(search.Projects);

        // Open project = Dashboard entry (no auto-launch).
        var open = commands.Execute(CreativeCommand.OpenCreativeProject(project.Id));
        Assert.True(open.Succeeded);
        Assert.False(open.ShouldLaunch);
        Assert.NotNull(open.Project!.LastOpened);

        var openRoot = commands.Execute(CreativeCommand.OpenCreativeProjectRoot(project.Id));
        Assert.True(openRoot.Succeeded);
        Assert.True(openRoot.ShouldLaunch);
        Assert.Equal(@"D:\Art\Illustration", openRoot.LaunchTarget);
        Assert.Contains(openRoot.Project!.RecentItems, r => r.IsRoot);

        var openLink = commands.Execute(CreativeCommand.OpenCreativeProjectResource(project.Id, resourceId));
        Assert.True(openLink.Succeeded);
        Assert.True(openLink.LaunchIsExternalLink);
        Assert.StartsWith("https://", openLink.LaunchTarget!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(openLink.Project!.RecentItems, r => r.Key == resourceId);

        var notes = commands.Execute(CreativeCommand.SaveCreativeProjectNotes(project.Id, "tighten composition"));
        Assert.True(notes.Succeeded);
        Assert.Equal("tighten composition", notes.Project!.Notes);

        var quick = commands.Execute(CreativeCommand.ToggleCreativeProjectResourceQuickAction(project.Id, resourceId));
        Assert.True(quick.Succeeded);
        Assert.True(quick.Project!.Resources.Single(r => r.Id == resourceId).IsQuickAction);

        var fav = commands.Execute(CreativeCommand.ToggleCreativeProjectFavorite(project.Id));
        Assert.True(fav.Succeeded);
        Assert.True(fav.Project!.IsFavorite);

        var delete = commands.Execute(CreativeCommand.DeleteCreativeProjectRegistration(project.Id));
        Assert.True(delete.Succeeded);
        Assert.Empty(projects.Projects);
    }

    [Fact]
    public void LegacyOpenProject_StillUsesCreativeItem()
    {
        var workspace = new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore());
        Assert.True(workspace.TryAdd(@"C:\Users\demo\LegacyProject", CreativeItemType.Project, "Legacy", out var item, out _));
        var commands = new CreativeCommandService(workspace, new CreativeProjectService(new MemoryCreativeProjectStore()));

        var open = commands.Execute(CreativeCommand.OpenProject(item!.Id));
        Assert.True(open.Succeeded);
        Assert.True(open.ShouldLaunch);
        Assert.Equal(item.Path, open.LaunchTarget);
    }

    [Fact]
    public void ExternalLinkValidation_UsesWebUrlValidator()
    {
        Assert.True(WebUrlValidator.TryNormalize("https://www.youtube.com/", out var url, out _));
        Assert.NotNull(url);
        Assert.False(WebUrlValidator.TryNormalize("javascript:alert(1)", out _, out _));
    }
}
