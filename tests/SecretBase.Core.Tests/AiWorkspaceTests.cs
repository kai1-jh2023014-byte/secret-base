using System.Text.Json;
using SecretBase.Core.Ai;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Ai;

namespace SecretBase.Core.Tests;

public class AiBuiltinToolsTests
{
    [Fact]
    public void Catalog_HasCursorAndOfficialWebsites_Validated()
    {
        Assert.Contains(AiBuiltinTools.Catalog, t => t.Id == AiBuiltinTools.CursorId && t.IsDesktopApp);
        Assert.True(AiBuiltinTools.TryGetOfficialWebsite(AiBuiltinTools.ChatGptId, out var gpt, out _));
        Assert.StartsWith("https://", gpt, StringComparison.OrdinalIgnoreCase);
        Assert.True(AiBuiltinTools.TryGetOfficialWebsite(AiBuiltinTools.ClaudeId, out _, out _));
        Assert.True(AiBuiltinTools.TryGetOfficialWebsite(AiBuiltinTools.GeminiId, out _, out _));
        Assert.False(AiBuiltinTools.TryGetOfficialWebsite("unknown", out _, out _));
    }
}

public class AiWorkspaceWidgetConfigurationTests
{
    [Fact]
    public void RoundTrip_AndMalformedDictionary_FallsBackSafely()
    {
        var config = AiWorkspaceWidgetConfiguration.CreateDefault();
        config.EnabledToolIds = [AiBuiltinTools.CursorId, AiBuiltinTools.ChatGptId];
        var dict = config.ToDictionary();
        var restored = AiWorkspaceWidgetConfiguration.FromDictionary(dict);
        Assert.Equal(AiWorkspaceWidgetConfiguration.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(2, restored.EnabledToolIds.Count);

        var malformed = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["EnabledToolIds"] = JsonSerializer.SerializeToElement(new[] { "chatgpt", "not-a-tool" })
        };
        var recovered = AiWorkspaceWidgetConfiguration.FromDictionary(malformed);
        Assert.Contains(AiBuiltinTools.ChatGptId, recovered.EnabledToolIds);
        Assert.DoesNotContain("not-a-tool", recovered.EnabledToolIds);

        var empty = AiWorkspaceWidgetConfiguration.FromDictionary(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal));
        Assert.Equal(AiBuiltinTools.Catalog.Count, empty.EnabledToolIds.Count);
    }
}

public class AiCursorFolderValidatorTests
{
    [Fact]
    public void RejectsMissingAndRelative_AcceptsAbsoluteFolder()
    {
        Assert.False(AiCursorFolderValidator.TryNormalizeProjectRoot(null, out _, out var missing));
        Assert.Contains("root", missing, StringComparison.OrdinalIgnoreCase);
        Assert.False(AiCursorFolderValidator.TryNormalizeProjectRoot("relative", out _, out _));
        Assert.True(AiCursorFolderValidator.TryNormalizeProjectRoot(@"D:\Projects\secret-base\", out var root, out _));
        Assert.Equal(@"D:\Projects\secret-base", root);
    }
}

public class CursorInstallLocatorTests
{
    [Fact]
    public void ResolvesFromPath_AndKnownLocalAppData_WithoutHardcodedUser()
    {
        var bin = Path.Combine("fake-bin");
        var pathHit = CursorInstallLocator.TryResolve(
            bin,
            localAppData: null,
            fileExists: p => p.EndsWith("cursor.cmd", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Path.Combine(bin, "cursor.cmd"), pathHit);

        var local = Path.Combine("Users", "demo", "AppData", "Local");
        var expected = Path.Combine(local, "Programs", "cursor", "Cursor.exe");
        var localHit = CursorInstallLocator.TryResolve(
            pathEnvironment: null,
            localAppData: local,
            fileExists: p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expected, localHit);

        Assert.Null(CursorInstallLocator.TryResolve(null, null, _ => false));
    }

    [Fact]
    public void QuoteWindowsArgument_HandlesSpaces_RejectsQuotes()
    {
        Assert.True(CursorInstallLocator.TryQuoteWindowsArgument(@"D:\src\repo", out var plain, out _));
        Assert.Equal(@"D:\src\repo", plain);
        Assert.True(CursorInstallLocator.TryQuoteWindowsArgument(@"D:\my projects\repo", out var quoted, out _));
        Assert.Equal("\"D:\\my projects\\repo\"", quoted);
        Assert.False(CursorInstallLocator.TryQuoteWindowsArgument("bad\"path", out _, out _));
    }
}

public class AiCommandServiceTests
{
    [Fact]
    public void OpenTool_Web_And_CursorUnavailable_Fallback()
    {
        var ai = new AiCommandService(isCursorAvailable: () => false);
        var gpt = ai.Execute(AiCommand.OpenTool(AiBuiltinTools.ChatGptId));
        Assert.True(gpt.Succeeded);
        Assert.True(gpt.ShouldOpenUrl);
        Assert.StartsWith("https://", gpt.Url!, StringComparison.OrdinalIgnoreCase);

        var cursor = ai.Execute(AiCommand.OpenTool(AiBuiltinTools.CursorId));
        Assert.False(cursor.Succeeded);
        Assert.True(cursor.OfferCursorWebsiteFallback);

        var website = ai.Execute(AiCommand.OpenCursorWebsite());
        Assert.True(website.Succeeded);
        Assert.True(website.ShouldOpenUrl);
    }

    [Fact]
    public void OpenProjectInCursor_RequiresRoot_AndRecordsWhenAvailable()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Secret Base", null, CreativeProjectType.Programming, null, out var noRoot, out _));
        var aiMissing = new AiCommandService(projects: projects, isCursorAvailable: () => true);
        var failRoot = aiMissing.Execute(AiCommand.OpenProjectInCursor(noRoot!.Id));
        Assert.False(failRoot.Succeeded);
        Assert.Contains("root", failRoot.ErrorMessage!, StringComparison.OrdinalIgnoreCase);

        Assert.True(projects.TryCreate(
            "With Root",
            null,
            CreativeProjectType.Programming,
            @"D:\Projects\secret-base",
            out var withRoot,
            out _));
        var ai = new AiCommandService(projects: projects, isCursorAvailable: () => true);
        var open = ai.Execute(AiCommand.OpenProjectInCursor(withRoot!.Id));
        Assert.True(open.Succeeded);
        Assert.True(open.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\Projects\secret-base", open.FolderPath);
        Assert.NotEmpty(projects.FindById(withRoot.Id)!.RecentItems);
    }

    [Fact]
    public void CreativeCommand_OpenProjectInCursor_DelegatesToAi()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "App",
            null,
            CreativeProjectType.Programming,
            @"C:\src\app",
            out var project,
            out _));
        var ai = new AiCommandService(projects: projects, isCursorAvailable: () => true);
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects,
            ai);

        var result = creative.Execute(CreativeCommand.OpenProjectInCursor(project!.Id));
        Assert.True(result.Succeeded);
        Assert.True(result.ShouldOpenCursorAtFolder);
        Assert.Equal(@"C:\src\app", result.CursorFolderPath);

        var web = creative.Execute(CreativeCommand.OpenAiTool(AiBuiltinTools.GeminiId));
        Assert.True(web.Succeeded);
        Assert.True(web.ShouldLaunch);
        Assert.True(web.LaunchIsExternalLink);
    }
}

public class AiWidgetFactoryTests
{
    [Fact]
    public void CreateAi_DoesNotSeedDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateAi();
        Assert.Equal(WidgetTypes.Ai, widget.Type);
        var layout = DesktopLayout.CreateDefault();
        Assert.DoesNotContain(layout.Widgets, w => w.Type == WidgetTypes.Ai);
    }

    [Fact]
    public void CreateClock_AndCreateText_UseNewIds_NotSeedDefaults()
    {
        var clock = DefaultWidgetFactory.CreateClock();
        var text = DefaultWidgetFactory.CreateText();
        Assert.Equal(WidgetTypes.Clock, clock.Type);
        Assert.Equal(WidgetTypes.Text, text.Type);
        Assert.NotEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), clock.Id);
        Assert.NotEqual(Guid.Parse("22222222-2222-2222-2222-222222222222"), text.Id);
    }
}
