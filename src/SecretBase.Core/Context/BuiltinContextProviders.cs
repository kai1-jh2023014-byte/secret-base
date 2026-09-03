using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Focus;
using SecretBase.Core.Music;
using SecretBase.Core.Time;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Context;

public static class BuiltinContextProviders
{
    public static IReadOnlyList<IBaseContextProvider> Create(
        ITimeProvider time,
        Func<IReadOnlyList<CalendarEvent>> events,
        Func<TodoList> todos,
        Func<IReadOnlyList<CreativeProject>> projects,
        Func<IReadOnlyList<CustomApp>> apps,
        Func<AssistantMusicState> music,
        Func<WorkspaceSession?> workspace,
        Func<FocusSession> focus,
        Func<AssistantProviderStatusInfo> provider,
        Func<IReadOnlyList<string>> integrations,
        Func<string> atmosphere)
    {
        return
        [
            new StaticContextProvider(BaseContextSliceIds.Time, () =>
            {
                var now = time.GetLocalNow();
                return new BaseContextSlice(
                    BaseContextSliceIds.Time,
                    now.ToString("yyyy-MM-dd HH:mm dddd"),
                    [$"{now:HH:mm}", now.ToString("dddd, MMMM d")]);
            }),
            new StaticContextProvider(BaseContextSliceIds.Calendar, () =>
            {
                var now = time.GetLocalNow();
                var list = events() ?? [];
                var today = list.Where(item => item.OccursOn(new DateOnly(now.Year, now.Month, now.Day))).Take(8).ToList();
                var next = list.Where(item => item.Start >= now).OrderBy(item => item.Start).FirstOrDefault();
                var facts = today.Select(item =>
                    item.IsAllDay ? item.Title : $"{item.Start:HH:mm} {item.Title}").ToList();
                if (next is not null)
                {
                    facts.Insert(0, $"next: {next.Start:HH:mm} {next.Title}");
                }

                return new BaseContextSlice(
                    BaseContextSliceIds.Calendar,
                    today.Count == 0 ? "No events today" : $"{today.Count} event(s) today",
                    facts);
            }),
            new StaticContextProvider(BaseContextSliceIds.Todo, () =>
            {
                var open = (todos()?.Items ?? []).Where(item => !item.IsDone).Take(8).ToList();
                return new BaseContextSlice(
                    BaseContextSliceIds.Todo,
                    open.Count == 0 ? "No open tasks" : $"{open.Count} open task(s)",
                    open.Select(item => item.Title).ToList());
            }),
            new StaticContextProvider(BaseContextSliceIds.Project, () =>
            {
                var list = projects() ?? [];
                return new BaseContextSlice(
                    BaseContextSliceIds.Project,
                    list.Count == 0 ? "No projects registered" : $"{list.Count} project(s)",
                    list.Take(8).Select(project => project.Name).ToList());
            }),
            new StaticContextProvider(BaseContextSliceIds.Files, () =>
            {
                var names = (projects() ?? [])
                    .SelectMany(project => project.Resources)
                    .Where(resource => resource.Kind == CreativeProjectResourceKind.File)
                    .Select(resource => resource.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Take(8)
                    .ToList();
                return new BaseContextSlice(
                    BaseContextSliceIds.Files,
                    names.Count == 0 ? "No registered files" : $"{names.Count} registered file(s)",
                    names);
            }),
            new StaticContextProvider(BaseContextSliceIds.Applications, () =>
            {
                var list = apps() ?? [];
                return new BaseContextSlice(
                    BaseContextSliceIds.Applications,
                    list.Count == 0 ? "No registered apps" : $"{list.Count} registered app(s)",
                    list.Take(8).Select(app => app.Name).ToList());
            }),
            new StaticContextProvider(BaseContextSliceIds.Music, () =>
            {
                var state = music();
                var facts = new List<string>();
                if (!string.IsNullOrWhiteSpace(state.CurrentTrackTitle))
                {
                    facts.Add($"{state.CurrentTrackTitle} — {state.CurrentTrackArtist}");
                }

                facts.Add(state.IsPlaying ? "playing" : "paused");
                return new BaseContextSlice(
                    BaseContextSliceIds.Music,
                    state.CurrentTrackTitle ?? "Nothing playing",
                    facts);
            }),
            new StaticContextProvider(BaseContextSliceIds.Workspace, () =>
            {
                var session = workspace();
                if (session is null)
                {
                    return new BaseContextSlice(BaseContextSliceIds.Workspace, "No workspace prepared", []);
                }

                return new BaseContextSlice(
                    BaseContextSliceIds.Workspace,
                    session.StatusLine,
                    session.PreparedChecks.Take(8).ToList());
            }),
            new StaticContextProvider(BaseContextSliceIds.Focus, () =>
            {
                var session = focus();
                return new BaseContextSlice(
                    BaseContextSliceIds.Focus,
                    session.StatusLine(time.GetLocalNow()),
                    session.IsRunning ? [session.Label] : []);
            }),
            new StaticContextProvider(BaseContextSliceIds.Provider, () =>
            {
                var status = provider();
                return new BaseContextSlice(
                    BaseContextSliceIds.Provider,
                    BaseAiStatusFormatter.Format(status),
                    [status.DisplayName, status.StatusLabel]);
            }),
            new StaticContextProvider(BaseContextSliceIds.Integrations, () =>
            {
                var list = integrations() ?? [];
                return new BaseContextSlice(
                    BaseContextSliceIds.Integrations,
                    list.Count == 0 ? "No integrations remembered" : string.Join(", ", list.Take(6)),
                    list.Take(8).ToList());
            }),
            new StaticContextProvider(BaseContextSliceIds.Preferences, () =>
            {
                var name = atmosphere();
                return new BaseContextSlice(
                    BaseContextSliceIds.Preferences,
                    string.IsNullOrWhiteSpace(name) ? "Calm" : name,
                    []);
            })
        ];
    }
}
