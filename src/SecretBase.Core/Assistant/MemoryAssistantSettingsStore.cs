namespace SecretBase.Core.Assistant;

public sealed class MemoryAssistantSettingsStore : IAssistantSettingsStore
{
    private AssistantSettings _settings = new();

    public AssistantSettings LoadOrCreate() =>
        AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings
        {
            SchemaVersion = _settings.SchemaVersion,
            ProviderId = _settings.ProviderId,
            Model = _settings.Model
        });

    public void Save(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(settings);
        _settings = new AssistantSettings
        {
            SchemaVersion = AssistantSettings.CurrentSchemaVersion,
            ProviderId = migrated.ProviderId,
            Model = migrated.Model
        };
    }
}
