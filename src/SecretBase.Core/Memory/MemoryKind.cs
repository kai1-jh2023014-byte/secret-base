namespace SecretBase.Core.Memory;

public enum MemoryKind
{
    Fact = 0,
    Preference = 1,
    Project = 2,
    Workflow = 3,
    Decision = 4,
    Session = 5,
    Todo = 6,
    Idea = 7,
    Note = 8
}

public enum MemoryRetention
{
    Ephemeral = 0,
    Temporary = 1,
    Session = 2,
    LongTerm = 3,
    Permanent = 4
}
