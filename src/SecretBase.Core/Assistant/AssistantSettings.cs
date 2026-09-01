namespace SecretBase.Core.Assistant;

/// <summary>Provider/model prefs and autonomy caps. API keys never live here.</summary>
public sealed class AssistantSettings
{
    public const int CurrentSchemaVersion = 2;

    public const string DefaultOpenAiModel = "gpt-4o-mini";

    public const int DefaultMaxSteps = 5;

    public const int MaxStepsHardCap = 8;

    public const int MinMaxSteps = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string ProviderId { get; set; } = AssistantProviderIds.OpenAi;

    public string Model { get; set; } = DefaultOpenAiModel;

    /// <summary>Max plan / tool steps per user turn (not an infinite agent loop).</summary>
    public int MaxSteps { get; set; } = DefaultMaxSteps;

    /// <summary>
    /// v0.5: always treated as true. Launch tools never auto-run; kept for JSON schema compatibility only.
    /// </summary>
    public bool RequireConfirmationForActions { get; set; } = true;
}

public interface IAssistantSettingsStore
{
    AssistantSettings LoadOrCreate();

    void Save(AssistantSettings settings);
}

public static class AssistantSettingsMigrator
{
    public static AssistantSettings MigrateToCurrent(AssistantSettings? settings)
    {
        var doc = settings ?? new AssistantSettings();
        if (doc.SchemaVersion < 1)
        {
            doc.SchemaVersion = 1;
        }

        if (string.IsNullOrWhiteSpace(doc.ProviderId))
        {
            doc.ProviderId = AssistantProviderIds.OpenAi;
        }

        if (string.IsNullOrWhiteSpace(doc.Model))
        {
            doc.Model = AssistantSettings.DefaultOpenAiModel;
        }

        if (doc.SchemaVersion < 2)
        {
            if (doc.MaxSteps <= 0)
            {
                doc.MaxSteps = AssistantSettings.DefaultMaxSteps;
            }

            doc.RequireConfirmationForActions = true;
            doc.SchemaVersion = AssistantSettings.CurrentSchemaVersion;
        }

        doc.MaxSteps = Math.Clamp(doc.MaxSteps, AssistantSettings.MinMaxSteps, AssistantSettings.MaxStepsHardCap);
        doc.RequireConfirmationForActions = true;
        return doc;
    }
}
