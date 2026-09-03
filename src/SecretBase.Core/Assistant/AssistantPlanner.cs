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

        if (LooksLikeSearch(text))
        {
            return Clamp(BuildSearchPlan(), maxSteps);
        }

        if (LooksLikeRecall(text))
        {
            return Clamp(BuildRecallPlan(), maxSteps);
        }

        if (LooksLikeContinue(text) || LooksLikeStartProject(text)
            || (intent == AssistantIntentKind.ActionRequest && LooksLikeProject(text)))
        {
            return Clamp(BuildContinueWorkspacePlan(text, snapshot), maxSteps);
        }

        if (LooksLikeFocus(text))
        {
            return Clamp(BuildFocusPlan(), maxSteps);
        }

        if (LooksLikeCleanup(text))
        {
            return Clamp(BuildCleanupPlan(), maxSteps);
        }

        if (LooksLikeTodo(text))
        {
            return Clamp(BuildTodoPlan(text), maxSteps);
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

    private static AssistantPlan BuildContinueWorkspacePlan(string text, AssistantContextSnapshot? snapshot)
    {
        var projectName = ResolveProjectHint(text, snapshot) ?? "対象プロジェクト";
        return new AssistantPlan
        {
            Summary = $"{projectName}の Workspace を準備します。開く操作は確認が必要です。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "Recall last session",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.MemoryRecall
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "Workspace を準備",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.WorkspacePrepare
                },
                new AssistantPlanStep
                {
                    Index = 3,
                    Title = "プロジェクトを開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.WorkspaceContinue,
                    RequiresConfirmation = true
                }
            ]
        };
    }

    private static AssistantPlan BuildSearchPlan() =>
        new()
        {
            Summary = "Secret Base の記憶・活動・プロジェクトから探します。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "Search Base",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.SearchBase
                }
            ]
        };

    private static AssistantPlan BuildRecallPlan() =>
        new()
        {
            Summary = "前回の作業と現在の状態を確認します。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "User state",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.UserState
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "Memory",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.MemoryRecall
                }
            ]
        };

    private static AssistantPlan BuildFocusPlan() =>
        new()
        {
            Summary = "Pomodoro を開始します。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "Focus timer",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.FocusStart
                }
            ]
        };

    private static AssistantPlan BuildCleanupPlan() =>
        new()
        {
            Summary = "使っていない登録ファイルの候補を提示します。削除はしません。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "Cleanup candidates",
                    Kind = AssistantPlanStepKind.Suggest,
                    ToolName = AssistantToolNames.FilesSuggestCleanup
                }
            ]
        };

    private static AssistantPlan BuildTodoPlan(string text)
    {
        var adding = text.Contains("追加", StringComparison.Ordinal)
                     || text.Contains("add", StringComparison.OrdinalIgnoreCase);
        return adding
            ? new AssistantPlan
            {
                Summary = "Todo を追加します（確認が必要）。",
                Steps =
                [
                    new AssistantPlanStep
                    {
                        Index = 1,
                        Title = "Add todo（確認が必要）",
                        Kind = AssistantPlanStepKind.ConfirmAction,
                        ToolName = AssistantToolNames.TodoAdd,
                        RequiresConfirmation = true
                    }
                ]
            }
            : new AssistantPlan
            {
                Summary = "Todo を確認します。",
                Steps =
                [
                    new AssistantPlanStep
                    {
                        Index = 1,
                        Title = "List todos",
                        Kind = AssistantPlanStepKind.Read,
                        ToolName = AssistantToolNames.TodoList
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

    private static bool LooksLikeSearch(string text) =>
        text.Contains("検索", StringComparison.Ordinal)
        || text.Contains("search", StringComparison.OrdinalIgnoreCase)
        || text.Contains("昨日見", StringComparison.Ordinal)
        || text.Contains("先週読", StringComparison.Ordinal)
        || text.Contains("最近作った", StringComparison.Ordinal);

    private static bool LooksLikeRecall(string text) =>
        text.Contains("前回止めた", StringComparison.Ordinal)
        || text.Contains("last session", StringComparison.OrdinalIgnoreCase)
        || text.Contains("覚えて", StringComparison.Ordinal)
        || text.Contains("where did I", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeContinue(string text) =>
        text.Contains("続け", StringComparison.Ordinal)
        || text.Contains("再開", StringComparison.Ordinal)
        || text.Contains("continue", StringComparison.OrdinalIgnoreCase)
        || text.Contains("resume", StringComparison.OrdinalIgnoreCase)
        || text.Contains("yesterday", StringComparison.OrdinalIgnoreCase)
        || text.Contains("昨日", StringComparison.Ordinal)
        || text.Contains("前回", StringComparison.Ordinal)
        || (text.Contains("開発", StringComparison.Ordinal)
            && (text.Contains("したい", StringComparison.Ordinal) || text.Contains("続け", StringComparison.Ordinal)));

    private static bool LooksLikeFocus(string text) =>
        text.Contains("ポモドーロ", StringComparison.Ordinal)
        || text.Contains("pomodoro", StringComparison.OrdinalIgnoreCase)
        || ((text.Contains("focus", StringComparison.OrdinalIgnoreCase)
             || text.Contains("集中", StringComparison.Ordinal))
            && (text.Contains("開始", StringComparison.Ordinal)
                || text.Contains("start", StringComparison.OrdinalIgnoreCase)
                || text.Contains("始めて", StringComparison.Ordinal)));

    private static bool LooksLikeCleanup(string text) =>
        text.Contains("cleanup", StringComparison.OrdinalIgnoreCase)
        || text.Contains("使ってない", StringComparison.Ordinal)
        || (text.Contains("ファイル", StringComparison.Ordinal)
            && (text.Contains("整理", StringComparison.Ordinal) || text.Contains("不要", StringComparison.Ordinal)));

    private static bool LooksLikeTodo(string text) =>
        text.Contains("todo", StringComparison.OrdinalIgnoreCase)
        || text.Contains("タスク", StringComparison.Ordinal)
        || text.Contains("やること", StringComparison.Ordinal);

    private static bool LooksLikeStartProject(string text) =>
        (text.Contains("始め", StringComparison.Ordinal) || text.Contains("開始", StringComparison.Ordinal)
                                                         || text.Contains("start", StringComparison.OrdinalIgnoreCase)
                                                         || text.Contains("開発", StringComparison.Ordinal))
        && LooksLikeProject(text);

    private static bool LooksLikeProject(string text) =>
        text.Contains("プロジェクト", StringComparison.Ordinal)
        || text.Contains("project", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Pokemon", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Cursor", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Secret Base", StringComparison.OrdinalIgnoreCase);

    private static AssistantPlan Clamp(AssistantPlan plan, int maxSteps) =>
        new()
        {
            Summary = plan.Summary,
            Steps = plan.Steps.Take(maxSteps).ToList()
        };
}
