namespace SecretBase.Core.Assistant;

/// <summary>Provider/model prefs. API keys never live here.</summary>
public sealed class AssistantSettings
{
    public const int CurrentSchemaVersion = 1;

    public const string DefaultOpenAiModel = "gpt-4o-mini";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string ProviderId { get; set; } = AssistantProviderIds.OpenAi;

    public string Model { get; set; } = DefaultOpenAiModel;
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
            doc.SchemaVersion = AssistantSettings.CurrentSchemaVersion;
        }

        if (string.IsNullOrWhiteSpace(doc.ProviderId))
        {
            doc.ProviderId = AssistantProviderIds.OpenAi;
        }

        if (string.IsNullOrWhiteSpace(doc.Model))
        {
            doc.Model = AssistantSettings.DefaultOpenAiModel;
        }

        return doc;
    }
}
