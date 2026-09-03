namespace SecretBase.Core.Assistant;

/// <summary>
/// Builds thin UI plans for known workflows. Not an autonomous agent loop.
/// </summary>
public static class AssistantPlanner
{
    public static AssistantPlan? TryBuildFromIntent(
        AssistantIntentKind intent,
        string? userText,
        AssistantContextSnapshot? snapshot,
        int maxSteps)
    {
        maxSteps = Math.Clamp(maxSteps, 1, AssistantSettings.MaxStepsHardCap);
        var text = userText?.Trim() ?? string.Empty;

        if (LooksLikeDelete(text))
        {
            return Clamp(BuildRemovePlan(text), maxSteps);
        }

        if (LooksLikePlayMusic(text))
        {
            return Clamp(BuildPlayMusicPlan(), maxSteps);
        }

        if (LooksLikeUsualSchedule(text))
        {
            return Clamp(BuildUsualSchedulePlan(), maxSteps);
        }

        if (LooksLikeAddSchedule(text))
        {
            return Clamp(BuildAddSchedulePlan(), maxSteps);
        }

        if (LooksLikeOpenNamed(text) && !LooksLikeProject(text))
        {
            return Clamp(BuildOpenNamedPlan(), maxSteps);
        }

        if (LooksLikeStartProject(text) || (intent == AssistantIntentKind.ActionRequest && LooksLikeProject(text)))
        {
            return Clamp(BuildStartProjectPlan(text, snapshot), maxSteps);
        }

        if (intent is AssistantIntentKind.Suggestion or AssistantIntentKind.Question
            && LooksLikeTodayPriority(text))
        {
            return Clamp(BuildTodayPriorityPlan(), maxSteps);
        }

        return null;
    }

    public static AssistantPlan FromPendingActions(
        string summary,
        IReadOnlyList<AssistantPendingAction> actions,
        IReadOnlyList<AssistantActivity>? priorReads = null)
    {
        var steps = new List<AssistantPlanStep>();
        var index = 1;
        if (priorReads is not null)
        {
            foreach (var read in priorReads.Where(a => a.Status == AssistantActivityStatus.Done).Take(3))
            {
                steps.Add(new AssistantPlanStep
                {
                    Index = index++,
                    Title = read.Domain is null ? read.Text : $"{read.Domain} checked",
                    Kind = AssistantPlanStepKind.Read,
                    Status = AssistantPlanStepStatus.Done
                });
            }
        }

        foreach (var action in actions)
        {
            steps.Add(new AssistantPlanStep
            {
                Index = index++,
                Title = action.Label,
                Kind = AssistantPlanStepKind.ConfirmAction,
                ToolName = action.ToolName,
                RequiresConfirmation = true,
                Status = AssistantPlanStepStatus.AwaitingConfirmation
            });
        }

        return new AssistantPlan { Summary = summary, Steps = steps };
    }

    private static AssistantPlan BuildTodayPriorityPlan() =>
        new()
        {
            Summary = "今日の予定と登録プロジェクトを見て、候補を提案します。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "今日の予定を確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CalendarGetToday
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "Creative Projectsを確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CreativeListProjects
                },
                new AssistantPlanStep
                {
                    Index = 3,
                    Title = "優先候補を提案",
                    Kind = AssistantPlanStepKind.Suggest,
                    ToolName = AssistantToolNames.ScheduleRecommend
                }
            ]
        };

    private static AssistantPlan BuildStartProjectPlan(string text, AssistantContextSnapshot? snapshot)
    {
        var projectName = ResolveProjectHint(text, snapshot) ?? "対象プロジェクト";
        return new AssistantPlan
        {
            Summary = $"{projectName}の作業を始める準備をします。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "今日の予定を確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CalendarGetToday
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = $"{projectName}を確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CreativeListProjects
                },
                new AssistantPlanStep
                {
                    Index = 3,
                    Title = "Cursorで開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.CursorOpenProject,
                    RequiresConfirmation = true
                }
            ]
        };
    }

    private static AssistantPlan BuildPlayMusicPlan() =>
        new()
        {
            Summary = "曲を探して Music ウィジェットで再生します（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "音楽カタログを確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.MusicGetState
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "再生する（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.MusicPlay,
                    RequiresConfirmation = true
                }
            ]
        };

    private static AssistantPlan BuildUsualSchedulePlan() =>
        new()
        {
            Summary = "いつもの予定を今日のローカルカレンダーに入れます（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "今日の予定を確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CalendarGetToday
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "いつもの予定を入れる（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.CalendarApplyUsual,
                    RequiresConfirmation = true
                }
            ]
        };

    private static AssistantPlan BuildAddSchedulePlan() =>
        new()
        {
            Summary = "ローカルカレンダーに予定を追加します（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "予定を追加（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.CalendarAddEvent,
                    RequiresConfirmation = true
                }
            ]
        };

    private static AssistantPlan BuildOpenNamedPlan() =>
        new()
        {
            Summary = "登録済みのファイル / アプリを名前で開きます（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "名前で開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.WorkspaceOpenNamed,
                    RequiresConfirmation = true
                }
            ]
        };

    private static AssistantPlan BuildRemovePlan(string text) =>
        new()
        {
            Summary = "ディスク上のファイルは削除しません。Block アイテムをデスクトップに戻すか、登録を外します（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = LooksLikeDelete(text)
                        ? "登録を外す / デスクトップに戻す（確認が必要）"
                        : "登録を外す（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.FilesDelete,
                    RequiresConfirmation = true
                }
            ]
        };

    private static string? ResolveProjectHint(string text, AssistantContextSnapshot? snapshot)
    {
        if (snapshot?.Projects is { Count: > 0 })
        {
            var hit = snapshot.Projects.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.Name)
                && text.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit.Name;
            }
        }

        if (text.Contains("Pokemon", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ポケモン", StringComparison.Ordinal))
        {
            return "Pokemon Project";
        }

        return null;
    }

    private static bool LooksLikePlayMusic(string text) =>
        text.Contains("かけて", StringComparison.Ordinal)
        || text.Contains("あの曲", StringComparison.Ordinal)
        || ((text.Contains("再生", StringComparison.Ordinal)
             || text.Contains("play", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("曲", StringComparison.Ordinal)
                || text.Contains("音楽", StringComparison.Ordinal)
                || text.Contains("song", StringComparison.OrdinalIgnoreCase)
                || text.Contains("music", StringComparison.OrdinalIgnoreCase)
                || text.Contains("spotify", StringComparison.OrdinalIgnoreCase)));

    private static bool LooksLikeUsualSchedule(string text) =>
        text.Contains("いつも", StringComparison.Ordinal)
        || text.Contains("usual", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAddSchedule(string text) =>
        (text.Contains("予定", StringComparison.Ordinal)
         || text.Contains("schedule", StringComparison.OrdinalIgnoreCase)
         || text.Contains("event", StringComparison.OrdinalIgnoreCase))
        && (text.Contains("入れて", StringComparison.Ordinal)
            || text.Contains("いれて", StringComparison.Ordinal)
            || text.Contains("追加", StringComparison.Ordinal)
            || text.Contains("add", StringComparison.OrdinalIgnoreCase))
        && !LooksLikeUsualSchedule(text);

    private static bool LooksLikeOpenNamed(string text) =>
        (text.Contains("ファイル", StringComparison.Ordinal)
         || text.Contains("file", StringComparison.OrdinalIgnoreCase)
         || text.Contains("あの", StringComparison.Ordinal))
        && (text.Contains("開いて", StringComparison.Ordinal)
            || text.Contains("open", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeDelete(string text) =>
        text.Contains("削除", StringComparison.Ordinal)
        || text.Contains("delete", StringComparison.OrdinalIgnoreCase)
        || text.Contains("消して", StringComparison.Ordinal);

    private static bool LooksLikeTodayPriority(string text) =>
        text.Contains("今日", StringComparison.Ordinal)
        || text.Contains("優先", StringComparison.Ordinal)
        || text.Contains("やればいい", StringComparison.Ordinal)
        || text.Contains("何をすれ", StringComparison.Ordinal)
        || text.Contains("what should", StringComparison.OrdinalIgnoreCase)
        || text.Contains("today", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeStartProject(string text) =>
        (text.Contains("始め", StringComparison.Ordinal) || text.Contains("開始", StringComparison.Ordinal)
                                                         || text.Contains("start", StringComparison.OrdinalIgnoreCase)
                                                         || text.Contains("開発", StringComparison.Ordinal))
        && LooksLikeProject(text);

    private static bool LooksLikeProject(string text) =>
        text.Contains("プロジェクト", StringComparison.Ordinal)
        || text.Contains("project", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Pokemon", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Cursor", StringComparison.OrdinalIgnoreCase);

    private static AssistantPlan Clamp(AssistantPlan plan, int maxSteps) =>
        new()
        {
            Summary = plan.Summary,
            Steps = plan.Steps.Take(maxSteps).ToList()
        };
}
