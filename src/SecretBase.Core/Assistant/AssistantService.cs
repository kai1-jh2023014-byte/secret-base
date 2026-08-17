namespace SecretBase.Core.Assistant;

/// <summary>
/// Chat loop with tool calls. Caps history. Does not persist secrets or launch OS.
/// </summary>
public sealed class AssistantService : IAssistantService
{
    public const int MaxVisibleMessages = 20;
    public const int MaxToolRounds = 4;

    private const string SystemPrompt =
        "You are Secret Base AI, a desktop assistant. Use tools to read calendar, projects, apps, and music. "
        + "Never claim you ran a shell, PowerShell, or deleted files. "
        + "If a tool fails, tell the user honestly. Do not invent Spotify, Classroom assignments, or OS actions.";

    private readonly IAiToolRegistry _registry;
    private readonly IAiToolExecutor _executor;
    private readonly Func<IAiProvider> _provider;
    private readonly Func<AssistantSettings> _settings;
    private readonly List<AiMessage> _history = [];
    private AssistantPendingConfirmation? _pending;
    private readonly List<AssistantActivity> _turnActivities = [];
    private AssistantToolResult? _lastLaunch;

    public AssistantService(
        IAiToolRegistry registry,
        IAiToolExecutor executor,
        Func<IAiProvider> provider,
        Func<AssistantSettings>? settings = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _settings = settings ?? (() => new AssistantSettings());
    }

    public IReadOnlyList<AiMessage> VisibleHistory =>
        _history.Where(m => m.Role is AiMessageRole.User or AiMessageRole.Assistant)
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(MaxVisibleMessages)
            .ToList();

    public void ClearSession()
    {
        _history.Clear();
        _pending = null;
        _turnActivities.Clear();
        _lastLaunch = null;
    }

    public async Task<AssistantTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default)
    {
        _pending = null;
        _turnActivities.Clear();
        _lastLaunch = null;

        var text = userText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return AssistantTurnResult.Fail("Message is empty.");
        }

        if (text.Length > 2000)
        {
            return AssistantTurnResult.Fail("Message is too long.");
        }

        _history.Add(new AiMessage { Role = AiMessageRole.User, Content = text });
        TrimHistory();
        return await ContinueModelAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssistantTurnResult> ConfirmPendingAsync(CancellationToken cancellationToken = default)
    {
        if (_pending is null)
        {
            return AssistantTurnResult.Fail("Nothing to confirm.");
        }

        var pending = _pending;
        _pending = null;
        _turnActivities.Clear();
        _lastLaunch = null;
        var executed = await ExecuteAndRecordAsync(pending.ToolCallId, pending.ToolName, pending.ArgumentsJson, cancellationToken)
            .ConfigureAwait(false);
        if (!executed.Succeeded && string.Equals(
                executed.ErrorMessage,
                AssistantUserMessages.CursorOpenFailed,
                StringComparison.Ordinal))
        {
            return Finish(AssistantTurnResult.Fail(AssistantUserMessages.CursorOpenFailed));
        }

        return await ContinueModelAsync(cancellationToken).ConfigureAwait(false);
    }

    public void CancelPending()
    {
        if (_pending is null)
        {
            return;
        }

        _history.Add(new AiMessage
        {
            Role = AiMessageRole.Tool,
            ToolCallId = _pending.ToolCallId,
            Content = "User cancelled this action."
        });
        _pending = null;
    }

    public Task<AssistantTurnResult> ContinueAfterCancelAsync(CancellationToken cancellationToken = default)
    {
        _turnActivities.Clear();
        _lastLaunch = null;
        return ContinueModelAsync(cancellationToken);
    }

    private async Task<AssistantTurnResult> ContinueModelAsync(CancellationToken cancellationToken)
    {
        var settings = _settings();
        var provider = _provider();
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var response = await provider.ChatAsync(
                BuildModelMessages(),
                _registry.Tools,
                settings.Model,
                cancellationToken).ConfigureAwait(false);

            if (response.Status == AiProviderStatus.NotConfigured)
            {
                return Finish(AssistantTurnResult.Fail(
                    AssistantUserMessages.NotConfigured + " " + AssistantUserMessages.OpenSettings,
                    needsConfiguration: true));
            }

            if (response.Status is AiProviderStatus.Unavailable or AiProviderStatus.Failed)
            {
                return Finish(AssistantTurnResult.Fail(
                    string.IsNullOrWhiteSpace(response.ErrorMessage)
                        ? AssistantUserMessages.Unavailable
                        : response.ErrorMessage!));
            }

            if (response.ToolCalls.Count == 0)
            {
                var content = response.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    _history.Add(new AiMessage { Role = AiMessageRole.Assistant, Content = content });
                }

                return Finish(AssistantTurnResult.Ok(content, SnapshotActivities()));
            }

            _history.Add(new AiMessage
            {
                Role = AiMessageRole.Assistant,
                Content = response.Content,
                ToolCalls = response.ToolCalls
            });

            foreach (var call in response.ToolCalls)
            {
                var tool = _registry.Find(call.Name);
                if (tool is null)
                {
                    _history.Add(new AiMessage
                    {
                        Role = AiMessageRole.Tool,
                        ToolCallId = call.Id,
                        Content = AssistantUserMessages.ToolUnavailable
                    });
                    continue;
                }

                if (tool.RequiresConfirmation)
                {
                    var prompt = AssistantConfirmationPolicy.Prompt(tool.Name, call.ArgumentsJson);
                    _pending = new AssistantPendingConfirmation
                    {
                        ToolCallId = call.Id,
                        ToolName = tool.Name,
                        ArgumentsJson = call.ArgumentsJson,
                        Prompt = prompt
                    };
                    _turnActivities.Add(new AssistantActivity { Text = ActivityFor(tool.Name) });
                    return Finish(AssistantTurnResult.Confirm(_pending, SnapshotActivities()));
                }

                var executed = await ExecuteAndRecordAsync(call.Id, call.Name, call.ArgumentsJson, cancellationToken)
                    .ConfigureAwait(false);
                if (!executed.Succeeded && string.Equals(
                        executed.ErrorMessage,
                        AssistantUserMessages.CursorOpenFailed,
                        StringComparison.Ordinal))
                {
                    return Finish(AssistantTurnResult.Fail(AssistantUserMessages.CursorOpenFailed));
                }
            }
        }

        return Finish(AssistantTurnResult.Fail(AssistantUserMessages.Unavailable));
    }

    private async Task<AssistantToolResult> ExecuteAndRecordAsync(
        string callId,
        string name,
        string args,
        CancellationToken cancellationToken)
    {
        _turnActivities.Add(new AssistantActivity { Text = ActivityFor(name) });
        var result = await _executor.ExecuteAsync(name, args, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(result.Activity))
        {
            _turnActivities.Add(new AssistantActivity { Text = result.Activity! });
        }

        _history.Add(new AiMessage
        {
            Role = AiMessageRole.Tool,
            ToolCallId = callId,
            Content = result.ContentForModel
        });

        if (result.ShouldLaunch || result.ShouldOpenCursorAtFolder)
        {
            _lastLaunch = result;
        }

        return result;
    }

    private AssistantTurnResult Finish(AssistantTurnResult result)
    {
        if (_lastLaunch is null)
        {
            return result;
        }

        return new AssistantTurnResult
        {
            Succeeded = result.Succeeded,
            AssistantText = result.AssistantText,
            ErrorMessage = result.ErrorMessage,
            NeedsConfiguration = result.NeedsConfiguration,
            Activities = result.Activities.Count > 0 ? result.Activities : SnapshotActivities(),
            PendingConfirmation = result.PendingConfirmation,
            ShouldLaunch = _lastLaunch.ShouldLaunch,
            LaunchTarget = _lastLaunch.LaunchTarget,
            LaunchIsExternalLink = _lastLaunch.LaunchIsExternalLink,
            ShouldOpenCursorAtFolder = _lastLaunch.ShouldOpenCursorAtFolder,
            CursorFolderPath = _lastLaunch.CursorFolderPath
        };
    }

    private IReadOnlyList<AiMessage> BuildModelMessages()
    {
        var list = new List<AiMessage>
        {
            new() { Role = AiMessageRole.System, Content = SystemPrompt }
        };
        list.AddRange(_history.TakeLast(MaxVisibleMessages));
        return list;
    }

    private void TrimHistory()
    {
        while (_history.Count > MaxVisibleMessages * 2)
        {
            _history.RemoveAt(0);
        }
    }

    private IReadOnlyList<AssistantActivity> SnapshotActivities() => _turnActivities.ToList();

    private static string ActivityFor(string toolName) => toolName switch
    {
        AssistantToolNames.CalendarGetToday => "Looking up today's calendar…",
        AssistantToolNames.CalendarGetUpcoming => "Looking up upcoming events…",
        AssistantToolNames.CreativeListProjects => "Searching Creative Projects…",
        AssistantToolNames.CreativeOpenProject => "Opening a Creative Project…",
        AssistantToolNames.CursorOpenProject => "Preparing to open Cursor…",
        AssistantToolNames.IntegrationOpen => "Opening an integration…",
        AssistantToolNames.AppsList => "Listing My Apps…",
        AssistantToolNames.AppsOpen => "Preparing to launch an app…",
        AssistantToolNames.MusicSearch => "Searching the music catalog…",
        AssistantToolNames.MusicPlay => "Preparing to play a track…",
        _ => "Running a Secret Base action…"
    };
}
