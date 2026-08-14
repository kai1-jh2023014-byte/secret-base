using SecretBase.Core.Creative;
using SecretBase.Core.Widgets.Ai;

namespace SecretBase.Core.Ai;

/// <summary>
/// Validates AI hub commands. Never launches OS processes — host + Platform do after validation.
/// </summary>
public sealed class AiCommandService
{
    private readonly AiWorkspaceWidgetConfiguration _configuration;
    private readonly CreativeProjectService? _projects;
    private readonly Func<bool> _isCursorAvailable;

    public AiCommandService(
        AiWorkspaceWidgetConfiguration? configuration = null,
        CreativeProjectService? projects = null,
        Func<bool>? isCursorAvailable = null)
    {
        _configuration = configuration ?? AiWorkspaceWidgetConfiguration.CreateDefault();
        _projects = projects;
        _isCursorAvailable = isCursorAvailable ?? (() => false);
    }

    public AiWorkspaceWidgetConfiguration Configuration => _configuration;

    public AiCommandResult Execute(AiCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Kind switch
        {
            AiCommandKind.ListTools => ListTools(),
            AiCommandKind.OpenTool => OpenTool(command),
            AiCommandKind.OpenProjectInCursor => OpenProjectInCursor(command),
            AiCommandKind.OpenCursorWebsite => OpenCursorWebsite(),
            _ => AiCommandResult.Fail(command.Kind, "Unknown AI command.")
        };
    }

    private AiCommandResult ListTools() =>
        AiCommandResult.Ok(AiCommandKind.ListTools, tools: _configuration.GetEnabledTools());

    private AiCommandResult OpenTool(AiCommand command)
    {
        var tool = AiBuiltinTools.FindById(command.ToolId);
        if (tool is null)
        {
            return AiCommandResult.Fail(AiCommandKind.OpenTool, "Unknown AI tool.");
        }

        if (!_configuration.GetEnabledTools().Any(t =>
                string.Equals(t.Id, tool.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return AiCommandResult.Fail(AiCommandKind.OpenTool, "AI tool is disabled.");
        }

        if (tool.Kind == AiToolKind.Cursor)
        {
            if (_isCursorAvailable())
            {
                return AiCommandResult.Ok(
                    AiCommandKind.OpenTool,
                    tool: tool,
                    shouldOpenCursorApp: true);
            }

            AiBuiltinTools.TryGetOfficialWebsite(tool.Id, out var fallbackUrl, out _);
            return new AiCommandResult
            {
                Succeeded = false,
                Kind = AiCommandKind.OpenTool,
                ErrorMessage = "Cursor is not available.",
                OfferCursorWebsiteFallback = true,
                Url = fallbackUrl,
                Tool = tool
            };
        }

        if (!AiBuiltinTools.TryGetOfficialWebsite(tool.Id, out var url, out var error))
        {
            return AiCommandResult.Fail(AiCommandKind.OpenTool, error);
        }

        return AiCommandResult.Ok(
            AiCommandKind.OpenTool,
            tool: tool,
            shouldOpenUrl: true,
            url: url);
    }

    private AiCommandResult OpenProjectInCursor(AiCommand command)
    {
        if (_projects is null)
        {
            return AiCommandResult.Fail(AiCommandKind.OpenProjectInCursor, "Projects are not available.");
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return AiCommandResult.Fail(AiCommandKind.OpenProjectInCursor, "Project id is missing.");
        }

        var project = _projects.FindById(command.ProjectId);
        if (project is null)
        {
            return AiCommandResult.Fail(AiCommandKind.OpenProjectInCursor, "Project is not registered.");
        }

        if (!AiCursorFolderValidator.TryNormalizeProjectRoot(project.RootFolder, out var root, out var rootError))
        {
            return AiCommandResult.Fail(AiCommandKind.OpenProjectInCursor, rootError);
        }

        if (!_isCursorAvailable())
        {
            AiBuiltinTools.TryGetOfficialWebsite(AiBuiltinTools.CursorId, out var fallbackUrl, out _);
            return new AiCommandResult
            {
                Succeeded = false,
                Kind = AiCommandKind.OpenProjectInCursor,
                ErrorMessage = "Cursor is not available.",
                OfferCursorWebsiteFallback = true,
                Url = fallbackUrl,
                Tool = AiBuiltinTools.FindById(AiBuiltinTools.CursorId)
            };
        }

        _ = _projects.TryRecordRootOpened(project.Id, DateTimeOffset.UtcNow, out _, out _);

        return AiCommandResult.Ok(
            AiCommandKind.OpenProjectInCursor,
            tool: AiBuiltinTools.FindById(AiBuiltinTools.CursorId),
            shouldOpenCursorAtFolder: true,
            folderPath: root);
    }

    private AiCommandResult OpenCursorWebsite()
    {
        if (!AiBuiltinTools.TryGetOfficialWebsite(AiBuiltinTools.CursorId, out var url, out var error))
        {
            return AiCommandResult.Fail(AiCommandKind.OpenCursorWebsite, error);
        }

        return AiCommandResult.Ok(
            AiCommandKind.OpenCursorWebsite,
            tool: AiBuiltinTools.FindById(AiBuiltinTools.CursorId),
            shouldOpenUrl: true,
            url: url);
    }
}
