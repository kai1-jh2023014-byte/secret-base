namespace SecretBase.Core.Creative;

/// <summary>Loads/saves the Creative Workspace document (Infrastructure implements).</summary>
public interface ICreativeWorkspaceStore
{
    CreativeWorkspaceDocument LoadOrCreate();

    void Save(CreativeWorkspaceDocument document);
}
