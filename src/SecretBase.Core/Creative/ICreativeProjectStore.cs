namespace SecretBase.Core.Creative;

public interface ICreativeProjectStore
{
    CreativeProjectDocument LoadOrCreate();

    void Save(CreativeProjectDocument document);
}
