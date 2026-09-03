using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Classroom;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;
using SecretBase.Core.Security;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Apps;
using SecretBase.Core.Widgets.Assistant;

namespace SecretBase.Core.Tests;

public class CustomAppValidatorTests
{
    [Fact]
    public void AcceptsAbsoluteExe_AndHttps_RejectsCommandLineAndJavascript()
    {
        Assert.True(CustomAppValidator.TryNormalize(
            "Pokemon Calculator",
            "Damage calc",
            CustomAppType.Application,
            @"C:\Apps\PokemonCalc.exe",
            @"D:\src\pokemon-calc",
            null,
            out var app,
            out _));
        Assert.Equal("Pokemon Calculator", app.Name);
        Assert.Equal(@"C:\Apps\PokemonCalc.exe", app.LaunchTarget);
        Assert.Equal(@"D:\src\pokemon-calc", app.ProjectRoot);

        Assert.True(CustomAppValidator.TryNormalize(
            "Classroom Hub",
            null,
            CustomAppType.Website,
            "classroom.google.com",
            null,
            null,
            out var web,
            out _));
        Assert.StartsWith("https://classroom.google.com", web.LaunchTarget, StringComparison.OrdinalIgnoreCase);

        Assert.False(CustomAppValidator.TryNormalize(
            "Bad",
            null,
            CustomAppType.Application,
            @"C:\Apps\tool.exe /c whoami",
            null,
            null,
            out _,
            out var cmdError));
        Assert.Contains("argument", cmdError, StringComparison.OrdinalIgnoreCase);

        Assert.False(CustomAppValidator.TryNormalize(
            "XSS",
            null,
            CustomAppType.Website,
            "javascript:alert(1)",
            null,
            null,
            out _,
            out _));
    }
}

public class AppCommandServiceTests
{
    [Fact]
    public void ListOpenRemove_AndMissingRegistration()
    {
        var service = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(service.Apps.TryAdd(new CustomApp
        {
            Name = "UNO Party",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Games\UnoParty.exe"
        }, out var saved, out _));

        var list = service.Execute(AppCommand.ListApps());
        Assert.True(list.Succeeded);
        Assert.Single(list.Apps);

        var open = service.Execute(AppCommand.OpenApp(saved!.Id));
        Assert.True(open.Succeeded);
        Assert.True(open.ShouldLaunch);
        Assert.Equal(@"C:\Games\UnoParty.exe", open.LaunchTarget);
        Assert.False(open.LaunchIsExternalLink);

        Assert.False(service.Execute(AppCommand.OpenApp("missing")).Succeeded);
        Assert.True(service.Apps.TryRemove(saved.Id, out _));
        Assert.False(service.Execute(AppCommand.OpenApp(saved.Id)).Succeeded);
    }

    [Fact]
    public void OpenAppInCursor_UsesProjectRoot_OrLinkedCreativeProject()
    {
        var apps = new CustomAppService(new MemoryCustomAppStore());
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Pokemon Calc",
            null,
            CreativeProjectType.Programming,
            @"D:\src\pokemon-calc",
            out var project,
            out _));

        Assert.True(apps.TryAdd(new CustomApp
        {
            Name = "Direct root",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Apps\calc.exe",
            ProjectRoot = @"D:\src\direct"
        }, out var withRoot, out _));

        Assert.True(apps.TryAdd(new CustomApp
        {
            Name = "Linked project",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Apps\calc.exe",
            CreativeProjectId = project!.Id
        }, out var linked, out _));

        var commands = new AppCommandService(apps, projects);
        var cursor = commands.Execute(AppCommand.OpenAppInCursor(withRoot!.Id));
        Assert.True(cursor.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\src\direct", cursor.CursorFolderPath);

        var viaProject = commands.Execute(AppCommand.OpenAppInCursor(linked!.Id));
        Assert.True(viaProject.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\src\pokemon-calc", viaProject.CursorFolderPath);

        Assert.True(apps.TryAdd(new CustomApp
        {
            Name = "No root",
            Type = CustomAppType.Website,
            LaunchTarget = "https://example.com/"
        }, out var none, out _));
        Assert.False(commands.Execute(AppCommand.OpenAppInCursor(none!.Id)).Succeeded);
    }
}

public class ClassroomCommandServiceTests
{
    [Fact]
    public void Open_LaunchesOfficialSite_GetAssignmentsDoesNotInventData()
    {
        var service = new ClassroomCommandService();
        var open = service.Execute(ClassroomCommand.Open());
        Assert.True(open.Succeeded);
        Assert.True(open.ShouldLaunch);
        Assert.True(open.LaunchIsExternalLink);
        Assert.StartsWith("https://classroom.google.com", open.LaunchTarget, StringComparison.OrdinalIgnoreCase);

        var assignments = service.Execute(ClassroomCommand.GetAssignments());
        Assert.False(assignments.Succeeded);
        Assert.Contains("not connected", assignments.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(assignments.LaunchTarget);

        var courses = service.Execute(ClassroomCommand.GetCourses());
        Assert.False(courses.Succeeded);
    }
}

public class CalendarCommandServiceTests
{
    [Fact]
    public async Task GetTodayEvents_AndOpenFallbackUrl()
    {
        var day = new DateOnly(2026, 8, 17);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider(
        [
            new CalendarEvent
            {
                Title = "DTM",
                Provider = CalendarProviderIds.Local,
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
            }
        ]);
        var time = new HubFixedTimeProvider(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset));
        var commands = new CalendarCommandService(new CalendarService([local]), time);

        var today = await commands.ExecuteAsync(CalendarCommand.GetTodayEvents());
        Assert.True(today.Succeeded);
        Assert.Single(today.Events);
        Assert.Equal("DTM", today.Events[0].Title);

        var open = await commands.ExecuteAsync(CalendarCommand.Open());
        Assert.True(open.ShouldLaunch);
        Assert.Equal("https://calendar.google.com/", open.LaunchTarget);

        var tomorrow = new CalendarEvent
        {
            Title = "Studio",
            Provider = CalendarProviderIds.Local,
            Start = new DateTimeOffset(day.AddDays(1).ToDateTime(new TimeOnly(10, 0)), offset),
            End = new DateTimeOffset(day.AddDays(1).ToDateTime(new TimeOnly(11, 0)), offset)
        };
        local.ReplaceAll(
        [
            today.Events[0],
            tomorrow
        ]);
        var upcoming = await commands.ExecuteAsync(CalendarCommand.GetUpcoming(2));
        Assert.True(upcoming.Succeeded);
        Assert.Equal(2, upcoming.Events.Count);
        Assert.Contains(upcoming.Events, e => e.Title == "Studio");
    }
}

public class IntegrationCatalogTests
{
    [Fact]
    public void Catalog_ListsExpectedCommands_WithPrivilegeAndTrust()
    {
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.CalendarGetTodayEvents);
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.MusicSearch);
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.ClassroomGetAssignments);
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.CreativeListProjects);
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.AppsOpenApp);
        Assert.Contains(IntegrationCatalog.Commands, c => c.Id == IntegrationCommandIds.CursorOpenProject);

        var cursor = IntegrationCatalog.FindById(IntegrationCommandIds.CursorOpenProject);
        Assert.Equal(ActionPrivilege.UserConfirmationRequired, cursor!.Privilege);
        Assert.Equal(TrustBoundary.AiAgent, cursor.Trust);
        Assert.Null(IntegrationCatalog.FindById("shell.run"));
    }

    [Fact]
    public async Task Dispatcher_RoutesKnownCommands_RejectsUnknownAndShell()
    {
        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(apps.Apps.TryAdd(new CustomApp
        {
            Name = "DTM AI",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Tools\DtmAi.exe"
        }, out var app, out _));

        var hub = new IntegrationCommandService(
            classroom: new ClassroomCommandService(),
            apps: apps,
            music: new MusicCommandService(new MusicService()));

        var listed = await hub.ExecuteAsync(new IntegrationRequest { CommandId = IntegrationCommandIds.AppsListApps });
        Assert.True(listed.Succeeded);
        Assert.Single(listed.Apps);

        var openApp = await hub.ExecuteAsync(new IntegrationRequest
        {
            CommandId = IntegrationCommandIds.AppsOpenApp,
            AppId = app!.Id
        });
        Assert.True(openApp.ShouldLaunch);

        var classroom = await hub.ExecuteAsync(new IntegrationRequest
        {
            CommandId = IntegrationCommandIds.ClassroomOpen
        });
        Assert.True(classroom.ShouldLaunch);

        var assignments = await hub.ExecuteAsync(new IntegrationRequest
        {
            CommandId = IntegrationCommandIds.ClassroomGetAssignments
        });
        Assert.False(assignments.Succeeded);

        var unknown = await hub.ExecuteAsync(new IntegrationRequest { CommandId = "powershell.invoke" });
        Assert.False(unknown.Succeeded);
        Assert.Contains("Unknown", unknown.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

public class WidgetCatalogTests
{
    [Fact]
    public void GroupsExistingWidgets_ClassroomIsWebPreset_NotNewType()
    {
        Assert.Null(WidgetCatalog.FindById("not-a-widget"));
        var classroom = WidgetCatalog.FindById("classroom");
        Assert.NotNull(classroom);
        Assert.Equal(WidgetCatalogKind.WebPreset, classroom!.Kind);
        Assert.Equal(WidgetTypes.Web, classroom.WidgetType);
        Assert.True(WidgetCatalog.TryResolveWebPresetUrl(classroom, out var url, out _));
        Assert.StartsWith("https://classroom.google.com", url, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(WidgetCatalog.Entries, e => e.WidgetType == WidgetTypes.Apps);
        Assert.Equal(WidgetCatalogGroups.Information, classroom.Group);
        Assert.Contains(WidgetCatalog.Entries, e => e.Group == WidgetCatalogGroups.Apps && e.WidgetType == WidgetTypes.Apps);
        Assert.Contains(WidgetCatalog.Entries, e =>
            e.Group == WidgetCatalogGroups.Ai
            && e.WidgetType == WidgetTypes.Assistant
            && e.Label == "Base AI");
        Assert.Contains(WidgetCatalog.Entries, e =>
            e.Group == WidgetCatalogGroups.Ai && e.WidgetType == WidgetTypes.Ai);
    }

    [Fact]
    public void CreateApps_IsNotSeededIntoDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateApps();
        Assert.Equal(WidgetTypes.Apps, widget.Type);
        var config = AppsWidgetConfiguration.FromDictionary(widget.Configuration);
        Assert.Equal(AppsWidgetConfiguration.CurrentSchemaVersion, config.SchemaVersion);
        Assert.DoesNotContain(DesktopLayout.CreateDefault().Widgets, w => w.Type == WidgetTypes.Apps);
    }

    [Fact]
    public void CreateAssistant_IsNotSeededIntoDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateAssistant();
        Assert.Equal(WidgetTypes.Assistant, widget.Type);
        var config = AssistantWidgetConfiguration.FromDictionary(widget.Configuration);
        Assert.Equal(AssistantWidgetConfiguration.CurrentSchemaVersion, config.SchemaVersion);
        Assert.DoesNotContain(DesktopLayout.CreateDefault().Widgets, w => w.Type == WidgetTypes.Assistant);
    }
}

file sealed class HubFixedTimeProvider(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}
