using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Classroom;

/// <summary>
/// Classroom integration without a new widget or Classroom API client.
/// Open/Refresh launch the official site; assignment/course reads stay unavailable
/// until a real provider exists.
/// </summary>
public sealed class ClassroomCommandService
{
    public ClassroomCommandResult Execute(ClassroomCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            ClassroomCommandKind.Open => OpenOfficial(ClassroomCommandKind.Open),
            ClassroomCommandKind.Refresh => OpenOfficial(ClassroomCommandKind.Refresh),
            ClassroomCommandKind.GetAssignments => ClassroomCommandResult.Fail(
                ClassroomCommandKind.GetAssignments,
                ClassroomUrls.NoApiProviderMessage),
            ClassroomCommandKind.GetCourses => ClassroomCommandResult.Fail(
                ClassroomCommandKind.GetCourses,
                ClassroomUrls.NoApiProviderMessage),
            _ => ClassroomCommandResult.Fail(command.Kind, "Unknown classroom command.")
        };
    }

    private static ClassroomCommandResult OpenOfficial(ClassroomCommandKind kind)
    {
        if (!WebUrlValidator.TryNormalize(ClassroomUrls.Official, out var url, out var error) || url is null)
        {
            return ClassroomCommandResult.Fail(kind, error ?? WebUrlValidator.BlockedMessage);
        }

        return ClassroomCommandResult.Ok(
            kind,
            shouldLaunch: true,
            launchTarget: url,
            launchIsExternalLink: true);
    }
}
