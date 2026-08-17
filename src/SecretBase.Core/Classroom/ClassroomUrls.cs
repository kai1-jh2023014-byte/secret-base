namespace SecretBase.Core.Classroom;

/// <summary>
/// Official Google Classroom entry points. Secret Base does not ship a Classroom Widget
/// or a Classroom API client in this repo — open the existing Web Widget / browser.
/// </summary>
public static class ClassroomUrls
{
    public const string Official = "https://classroom.google.com/";

    public const string NoApiProviderMessage =
        "Classroom API is not connected. Open Google Classroom with the existing Web Widget. Secret Base does not invent Classroom assignments.";
}
