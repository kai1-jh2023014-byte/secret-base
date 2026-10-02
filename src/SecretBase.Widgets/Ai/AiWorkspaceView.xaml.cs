using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Assistant;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Hosting;

namespace SecretBase.Widgets.Ai;

/// <summary>
/// Native AI Workspace block. Chat, provider status, tools, and confirmation run in-block.
/// </summary>
public sealed partial class AiWorkspaceView : UserControl
{
    public AiWorkspaceView()
    {
        InitializeComponent();
    }

    public void Initialize(
        IAssistantService assistant,
        IAssistantSettingsStore settingsStore,
        ISecureSecretStore secrets,
        IAiProviderFactory providers,
        Func<AssistantTurnResult, string?> applyLaunch,
        OverlayDialogInput? dialogInput = null)
    {
        ChatHost.Initialize(
            assistant,
            settingsStore,
            secrets,
            providers,
            applyLaunch,
            headerTitle: "AI Workspace",
            headerSubtitle: "Chat · Context · Tools",
            dialogInput: dialogInput);
    }

    public void ApplyTheme(ThemeDefinition theme) => ChatHost.ApplyTheme(theme);
}
