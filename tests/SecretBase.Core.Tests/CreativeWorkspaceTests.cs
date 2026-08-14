using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Creative;

namespace SecretBase.Core.Tests;

public class CreativePathValidatorTests
{
    [Fact]
    public void TryNormalize_RejectsRelativeAndEmpty()
    {
        Assert.False(CreativePathValidator.TryNormalize("relative\\path", CreativeItemType.File, out _, out _));
        Assert.False(CreativePathValidator.TryNormalize(" ", CreativeItemType.Folder, out _, out _));
        Assert.False(CreativePathValidator.TryNormalize(@"C:\tools\app.exe --flag", CreativeItemType.File, out _, out _));
    }

    [Fact]
    public void TryNormalize_AcceptsAbsoluteFileAndFolder()
    {
        Assert.True(CreativePathValidator.TryNormalize(@"C:\Users\demo\song.wav", CreativeItemType.File, out var file, out _));
        Assert.Equal(@"C:\Users\demo\song.wav", file);
        Assert.True(CreativePathValidator.TryNormalize(@"C:\Users\demo\Projects\", CreativeItemType.Project, out var folder, out _));
        Assert.Equal(@"C:\Users\demo\Projects", folder);
    }
}

public class CreativeWorkspaceServiceTests
{
    [Fact]
    public void Add_Favorite_Search_Recent_AndDuplicate()
    {
        var store = new MemoryCreativeWorkspaceStore();
        var service = new CreativeWorkspaceService(store);

        Assert.True(service.TryAdd(@"C:\Users\demo\My Songs", CreativeItemType.Folder, "My Songs", out var songs, out _));
        Assert.True(service.TryAdd(@"C:\Users\demo\SecretBase.sln", CreativeItemType.File, null, out var sln, out _));
        Assert.False(service.TryAdd(@"C:\Users\demo\My Songs\", CreativeItemType.Folder, null, out _, out var dupError));
        Assert.Contains("already", dupError, StringComparison.OrdinalIgnoreCase);

        Assert.True(service.TrySetFavorite(songs!.Id, true, out _));
        Assert.Single(service.GetFavorites());

        Assert.True(service.TryMarkOpened(sln!.Id, DateTimeOffset.UtcNow, out _, out _));
        Assert.Single(service.GetRecent());

        var search = service.Search("secret");
        Assert.Contains(search, i => i.Id == sln.Id);

        var reloaded = new CreativeWorkspaceService(store);
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Contains(reloaded.Items, i => i.IsFavorite);
        Assert.Contains(reloaded.Items, i => i.LastOpened is not null);
    }
}

public class CreativeCommandServiceTests
{
    [Fact]
    public void Search_Open_ToggleFavorite_Flow()
    {
        var workspace = new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore());
        Assert.True(workspace.TryAdd(@"C:\Users\demo\Secret Base", CreativeItemType.Project, "Secret Base", out var project, out _));
        var commands = new CreativeCommandService(workspace);

        var search = commands.Execute(CreativeCommand.SearchItems("Secret"));
        Assert.True(search.Succeeded);
        Assert.Single(search.Items);

        var open = commands.Execute(CreativeCommand.OpenItem(project!.Id));
        Assert.True(open.Succeeded);
        Assert.True(open.ShouldLaunch);
        Assert.NotNull(open.Item!.LastOpened);

        var openFolder = commands.Execute(CreativeCommand.OpenFolder(project.Id));
        Assert.True(openFolder.Succeeded);

        var openProject = commands.Execute(CreativeCommand.OpenProject(project.Id));
        Assert.True(openProject.Succeeded);

        var fav = commands.Execute(CreativeCommand.ToggleFavorite(project.Id));
        Assert.True(fav.Succeeded);
        Assert.True(fav.Item!.IsFavorite);

        Assert.False(commands.Execute(CreativeCommand.OpenItem("missing")).Succeeded);
        Assert.False(commands.Execute(CreativeCommand.SearchItems("https://evil")).Succeeded);
    }

    [Fact]
    public void OpenItem_RejectsUnregisteredIds_NeverRawPaths()
    {
        var commands = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()));
        var result = commands.Execute(CreativeCommand.OpenItem("not-registered"));
        Assert.False(result.Succeeded);
        Assert.False(result.ShouldLaunch);
    }
}

public class CreativeWidgetFactoryTests
{
    [Fact]
    public void CreateCreative_DoesNotSeedDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateCreative();
        Assert.Equal(WidgetTypes.Creative, widget.Type);
        var layout = DesktopLayout.CreateDefault();
        Assert.DoesNotContain(layout.Widgets, w => w.Type == WidgetTypes.Creative);
    }

    [Fact]
    public void WidgetConfiguration_RoundTrip()
    {
        var original = new CreativeWorkspaceWidgetConfiguration { AddFoldersAsProjects = true };
        var restored = CreativeWorkspaceWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.True(restored.AddFoldersAsProjects);
    }
}
