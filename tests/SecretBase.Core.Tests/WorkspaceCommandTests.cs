using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Time;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class WorkspaceCommandServiceTests
{
    [Fact]
    public void OpenNamed_LaunchesRegisteredApp()
    {
        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(apps.Apps.TryAdd(new CustomApp
        {
            Name = "DTM AI",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Tools\dtm.exe"
        }, out _, out _));
        var service = new WorkspaceCommandService(apps: apps);
        var result = service.Execute(WorkspaceCommand.OpenNamed("DTM"));
        Assert.True(result.Succeeded);
        Assert.True(result.ShouldLaunch);
        Assert.Equal(@"C:\Tools\dtm.exe", result.LaunchTarget);
    }

    [Fact]
    public void RemoveNamed_UnregistersApp_DoesNotInventDiskDelete()
    {
        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(apps.Apps.TryAdd(new CustomApp
        {
            Name = "Sketch",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Tools\sketch.exe"
        }, out var saved, out _));
        var service = new WorkspaceCommandService(apps: apps);
        var result = service.Execute(WorkspaceCommand.RemoveNamed("Sketch"));
        Assert.True(result.Succeeded);
        Assert.Contains("not deleted", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(apps.Apps.TryGet(saved!.Id, out _));
    }

    [Fact]
    public void RemoveNamed_Unknown_RefusesOsDelete()
    {
        var service = new WorkspaceCommandService();
        var result = service.Execute(WorkspaceCommand.RemoveNamed("random-notes"));
        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceCommandService.DiskDeleteRefused, result.ErrorMessage);
    }

    [Fact]
    public void RemoveNamed_RestoresHiddenBlockItem()
    {
        var catalog = new FakeCatalog();
        var service = new WorkspaceCommandService(catalog: catalog);
        var result = service.Execute(WorkspaceCommand.RemoveNamed("Game"));
        Assert.True(result.Succeeded);
        Assert.True(catalog.Removed);
        Assert.Contains("Desktop", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeCatalog : IWorkspaceCatalog
    {
        public bool Removed { get; private set; }

        public IReadOnlyList<WorkspaceNamedEntry> ListBlockItems() =>
        [
            new()
            {
                Name = "Game",
                Kind = WorkspaceEntryKind.BlockItem,
                BlockId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                LaunchTarget = "/tmp/game",
                HiddenFromDesktop = true
            }
        ];

        public bool TryRemoveBlockItem(Guid blockId, Guid itemId, out string? error)
        {
            Removed = true;
            error = null;
            return true;
        }
    }
}

public class LocalCalendarProviderWriteTests
{
    [Fact]
    public async Task AddAndUsualSchedule_PersistInStore()
    {
        var store = new MemoryLocalCalendarStore();
        var local = new LocalCalendarProvider(store);
        Assert.True(local.Capabilities.HasFlag(CalendarProviderCapabilities.CreateEvents));
        var offset = TimeSpan.FromHours(9);
        var day = new DateOnly(2026, 9, 3);
        var created = local.AddEvent(
            "東進",
            new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 0)), offset),
            new DateTimeOffset(day.ToDateTime(new TimeOnly(16, 0)), offset));
        Assert.Equal("東進", created.Title);

        var time = new CalendarWriteTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset));
        var commands = new CalendarCommandService(new CalendarService([local]), time);
        var remembered = await commands.ExecuteAsync(CalendarCommand.RememberUsual("東進", 14, 0, 120));
        Assert.True(remembered.Succeeded);
        Assert.Equal("東進", remembered.Usual?.Title);

        local.ReplaceAll([]);
        var applied = await commands.ExecuteAsync(CalendarCommand.ApplyUsual());
        Assert.True(applied.Succeeded);
        Assert.Contains(applied.Events, e => e.Title == "東進" && e.Start.Hour == 14);
    }

    private sealed class CalendarWriteTime(DateTimeOffset now) : ITimeProvider
    {
        public DateTimeOffset GetLocalNow() => now;
    }
}
