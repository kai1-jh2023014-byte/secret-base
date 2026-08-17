namespace SecretBase.Core.Classroom;

/// <summary>Allowed Classroom operations for UI and future AI. No OS / fake API.</summary>
public enum ClassroomCommandKind
{
    Open = 0,
    Refresh = 1,
    GetAssignments = 2,
    GetCourses = 3
}

/// <summary>
/// Validated Classroom intent. Future AI must emit these — never call Google APIs or Shell.
/// </summary>
public sealed class ClassroomCommand
{
    public ClassroomCommandKind Kind { get; init; }

    public static ClassroomCommand Open() => new() { Kind = ClassroomCommandKind.Open };

    public static ClassroomCommand Refresh() => new() { Kind = ClassroomCommandKind.Refresh };

    public static ClassroomCommand GetAssignments() =>
        new() { Kind = ClassroomCommandKind.GetAssignments };

    public static ClassroomCommand GetCourses() =>
        new() { Kind = ClassroomCommandKind.GetCourses };
}
