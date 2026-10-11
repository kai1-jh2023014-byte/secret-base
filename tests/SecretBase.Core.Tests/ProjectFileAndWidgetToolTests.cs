using SecretBase.Core.Assistant;
using SecretBase.Core.Base;
using SecretBase.Core.Creative;
using SecretBase.Core.Focus;
using SecretBase.Core.Todo;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Tests;

public class ProjectFileAccessTests
{
    [Fact]
    public void RejectsTraversalAndAbsolutePaths()
    {
        var root = CreateTempProjectRoot();
        try
        {
            Assert.False(ProjectFileAccess.TryResolveUnderRoot(root, "../secret.txt", out _, out var err));
            Assert.Contains("relative", err, StringComparison.OrdinalIgnoreCase);

            Assert.False(ProjectFileAccess.TryResolveUnderRoot(root, "/etc/passwd", out _, out err));
            Assert.Contains("relative", err, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void ListReadWrite_RoundTrip_UnderRoot()
    {
        var root = CreateTempProjectRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "README.md"), "hello");
            Directory.CreateDirectory(Path.Combine(root, "notes"));

            var listing = ProjectFileAccess.ListRelative(root, null, out var listError);
            Assert.Equal(string.Empty, listError);
            Assert.Contains("README.md", listing, StringComparison.Ordinal);
            Assert.Contains("notes/", listing, StringComparison.Ordinal);

            Assert.True(ProjectFileAccess.TryResolveUnderRoot(root, "README.md", out var full, out _));
            var text = ProjectFileAccess.ReadText(full, out var readError);
            Assert.Equal(string.Empty, readError);
            Assert.Equal("hello", text);

            Assert.True(ProjectFileAccess.TryResolveUnderRoot(root, "notes/todo.txt", out var writePath, out _));
            Assert.True(ProjectFileAccess.WriteText(writePath, "ship it", out var writeError, out var previous));
            Assert.Equal(string.Empty, writeError);
            Assert.Null(previous);
            Assert.Equal("ship it", File.ReadAllText(writePath));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void RejectsDisallowedExtension()
    {
        var root = CreateTempProjectRoot();
        try
        {
            var exe = Path.Combine(root, "tool.exe");
            File.WriteAllBytes(exe, [0x4D, 0x5A]);
            Assert.False(ProjectFileAccess.IsAllowedFileExtension(exe));
            Assert.Equal(string.Empty, ProjectFileAccess.ReadText(exe, out var error));
            Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempProjectRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "secret-base-pf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

public class FocusStopAndWidgetToolTests
{
    [Fact]
    public async Task FocusStop_StopsRunningSession()
    {
        var now = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var focus = new FocusSessionStore();
        focus.Start(now);
        var baseServices = new BaseExperienceServices(
            new MemoryTodoStore(),
            focus,
            () => [],
            () => [],
            () => [],
            () => now.AddMinutes(2));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: baseServices);

        var result = await executor.ExecuteAsync(AssistantToolNames.FocusStop, "{}");

        Assert.True(result.Succeeded);
        Assert.False(focus.Current.IsRunning);
        Assert.Equal(WidgetTypes.Pomodoro, result.EnsureWidgetType);
        Assert.Contains("Stopped", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WidgetEnsure_ReturnsEnsureType()
    {
        var executor = new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance);
        var result = await executor.ExecuteAsync(
            AssistantToolNames.WidgetEnsure,
            """{"widget_type":"calendar"}""");

        Assert.True(result.Succeeded);
        Assert.Equal(WidgetTypes.Calendar, result.EnsureWidgetType);
        Assert.False(result.ShouldArrangeDesktop);
    }

    [Fact]
    public async Task WidgetArrange_SetsArrangeFlag_AndOptionalEnsure()
    {
        var executor = new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance);
        var result = await executor.ExecuteAsync(
            AssistantToolNames.WidgetArrange,
            """{"widget_types":"calendar,pomodoro"}""");

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldArrangeDesktop);
        Assert.Contains(WidgetTypes.Calendar, result.EnsureWidgetType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WidgetTypes.Pomodoro, result.EnsureWidgetType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProjectFileTools_ListReadWrite_ThroughExecutor()
    {
        var root = Path.Combine(Path.GetTempPath(), "secret-base-exec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "notes.md"), "v1");
            var project = new CreativeProject
            {
                Id = "proj1",
                Name = "Demo",
                RootFolder = root,
                ProjectType = CreativeProjectType.Other
            };
            var baseServices = new BaseExperienceServices(
                new MemoryTodoStore(),
                new FocusSessionStore(),
                () => [project],
                () => [],
                () => [],
                () => new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
            var executor = new AssistantToolExecutor(
                BuiltinAssistantToolRegistry.Instance,
                baseExperience: baseServices);

            var list = await executor.ExecuteAsync(
                AssistantToolNames.ProjectFileList,
                """{"project_id":"proj1"}""");
            Assert.True(list.Succeeded);
            Assert.Contains("notes.md", list.ContentForModel, StringComparison.Ordinal);

            var read = await executor.ExecuteAsync(
                AssistantToolNames.ProjectFileRead,
                """{"project_id":"proj1","relative_path":"notes.md"}""");
            Assert.True(read.Succeeded);
            Assert.Contains("v1", read.ContentForModel, StringComparison.Ordinal);

            var write = await executor.ExecuteAsync(
                AssistantToolNames.ProjectFileWrite,
                """{"project_id":"proj1","relative_path":"notes.md","content":"v2"}""");
            Assert.True(write.Succeeded);
            Assert.Equal("v2", File.ReadAllText(Path.Combine(root, "notes.md")));

            var prompt = AssistantConfirmationPolicy.Prompt(
                AssistantToolNames.ProjectFileWrite,
                """{"project_id":"proj1","relative_path":"notes.md","content":"v2"}""");
            Assert.Contains("notes.md", prompt, StringComparison.Ordinal);
            Assert.Contains("proj1", prompt, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
