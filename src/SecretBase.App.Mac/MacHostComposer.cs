using SecretBase.Core;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Infrastructure.Assistant;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Music;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Startup;
using SecretBase.Infrastructure.Storage;
using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Mac;

namespace SecretBase.App.Mac;

public sealed class MacHostSession
{
    public required IAppLogger Logger { get; init; }
    public required ISafeExitService SafeExit { get; init; }
    public required CompatibilityInfo Compatibility { get; init; }
    public required ILayoutStore LayoutStore { get; init; }
    public required IThemeStore ThemeStore { get; init; }
    public required ITimeProvider TimeProvider { get; init; }
    public required ITargetLaunchService Launcher { get; init; }
    public required ICursorLaunchService CursorLaunch { get; init; }
    public required ISecureSecretStore Secrets { get; init; }
    public required IAutoStartService AutoStart { get; init; }
    public required IAppLaunchSettingsStore LaunchSettings { get; init; }
    public required IAssistantSettingsStore AssistantSettings { get; init; }
    public required IAssistantService Assistant { get; init; }
    public required IPathPickService PathPicker { get; init; }
}

public static class MacHostComposer
{
    public static MacHostSession Create(IAppLogger logger, Action requestExit)
    {
        var compatibility = new MacCompatibilityService().GetCurrent();
        logger.Info("startup", $"{AppInfo.Name} v{AppInfo.Version} starting (macOS workspace host).");
        logger.Info(
            "compatibility",
            $"OS={compatibility.OsDescription}; OSVersion={compatibility.OsVersion}; Arch={compatibility.OsArchitecture}; DotNet={compatibility.DotNetVersion}; WASDK={compatibility.WindowsAppSdkPackageVersion}");

        var secrets = new MacSecureSecretStore();
        var cursorLaunch = new MacCursorLaunchService();
        var launcher = new MacTargetLaunchService();
        var launchSettings = new JsonAppLaunchSettingsStore();
        var autoStart = new MacLaunchAgentAutoStartService();
        AutoStartCoordinator.SynchronizeAtStartup(autoStart, launchSettings, logger);

        var projectService = new CreativeProjectService(new JsonCreativeProjectStore());
        var aiCommands = new AiCommandService(
            AiWorkspaceWidgetConfiguration.CreateDefault(),
            projectService,
            () => cursorLaunch.IsAvailable);
        var creativeCommands = new CreativeCommandService(
            new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()),
            projectService,
            aiCommands);
        var appCommands = new AppCommandService(
            new CustomAppService(new JsonCustomAppStore()),
            projectService);
        var time = new SystemTimeProvider();
        bool OpenHttps(string url) => MacHttpsLauncher.TryOpen(url);
        var calendarCommands = new CalendarCommandService(
            CalendarServiceFactory.Create(
                CalendarWidgetConfiguration.CreateDefault(),
                time,
                secretStore: secrets,
                openBrowser: OpenHttps),
            time);
        var musicCommands = new MusicCommandService(MusicServiceFactory.Create(secrets, OpenHttps));
        var integrationCommands = new IntegrationCommandService(
            calendar: calendarCommands,
            music: musicCommands,
            creative: creativeCommands,
            apps: appCommands,
            ai: aiCommands);

        var assistantSettings = new JsonAssistantSettingsStore();
        var assistantProviders = new AssistantProviderFactory(secrets);
        var assistantRegistry = BuiltinAssistantToolRegistry.Instance;
        var assistantContext = new AssistantContextService(
            calendar: calendarCommands,
            creative: creativeCommands,
            apps: appCommands,
            music: musicCommands,
            settings: () => assistantSettings.LoadOrCreate(),
            isOpenAiKeyConfigured: () =>
                secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var key)
                && !string.IsNullOrWhiteSpace(key),
            isGeminiKeyConfigured: () =>
                secrets.TryGetSecret(AssistantSecretKeys.GeminiApiKey, out var gemini)
                && !string.IsNullOrWhiteSpace(gemini));
        var assistant = new AssistantService(
            assistantRegistry,
            new AssistantToolExecutor(
                assistantRegistry,
                calendarCommands,
                creativeCommands,
                aiCommands,
                integrationCommands,
                appCommands,
                musicCommands,
                assistantContext),
            () => assistantProviders.Create(assistantSettings.LoadOrCreate()),
            () => assistantSettings.LoadOrCreate(),
            assistantContext);

        return new MacHostSession
        {
            Logger = logger,
            SafeExit = new MacSafeExitService(requestExit),
            Compatibility = compatibility,
            LayoutStore = new JsonLayoutStore(logger: logger),
            ThemeStore = new JsonThemeStore(logger: logger),
            TimeProvider = time,
            Launcher = launcher,
            CursorLaunch = cursorLaunch,
            Secrets = secrets,
            AutoStart = autoStart,
            LaunchSettings = launchSettings,
            AssistantSettings = assistantSettings,
            Assistant = assistant,
            PathPicker = new MacPathPickService()
        };
    }
}
