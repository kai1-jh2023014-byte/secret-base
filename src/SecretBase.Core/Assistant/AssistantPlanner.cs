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

        if (LooksLikeCodingEnvironment(text))
        {
            return Clamp(BuildCodingEnvironmentPlan(), maxSteps);
        }

        if (LooksLikeUsualSchedule(text))
        {
            return Clamp(BuildUsualSchedulePlan(), maxSteps);
        }

        if (LooksLikeAddSchedule(text))
        {
            return Clamp(BuildAddSchedulePlan(), maxSteps);
        }

        if (LooksLikeOpenApp(text))
        {
            return Clamp(BuildOpenAppPlan(), maxSteps);
        }

        if (LooksLikeOpenNamed(text) && !LooksLikeProject(text))
        {
            return Clamp(BuildOpenNamedPlan(), maxSteps);
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
                    Title = "Workspace を準備",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.WorkspacePrepare
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "プロジェクトを開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.WorkspaceContinue,
                    RequiresConfirmation = true
                }
            ]
        };
    }

    private static AssistantPlan BuildFocusPlan() =>
        new()
        {
            Summary = "Pomodoro を開いて開始します。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "Open Pomodoro and start",
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
            Summary =
                "曲を探して Music ウィジェットで再生します。"
                + " Premium 再生が使えない場合は Spotify のページを開きます（確認が必要）。",
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
                    Title = "再生 / Spotify で開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.MusicPlay,
                    RequiresConfirmation = true
                }
            ]
        };

    private static AssistantPlan BuildCodingEnvironmentPlan() =>
        new()
        {
            Summary =
                "プログラミング環境を整えます：Projects / Pomodoro / Workspace を出し、"
                + "タイマーを開始し、デスクトップを並べます。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "登録プロジェクトを確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CreativeListProjects
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "Todo を確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.TodoList
                },
                new AssistantPlanStep
                {
                    Index = 3,
                    Title = "コーディング環境をセットアップ",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.CodingEnvironmentSetup
                }
            ]
        };

    private static AssistantPlan BuildOpenAppPlan() =>
        new()
        {
            Summary = "登録済みの My Apps / Block / 既知アプリを名前で開きます（確認が必要）。",
            Steps =
            [
                new AssistantPlanStep
                {
                    Index = 1,
                    Title = "登録アプリを確認",
                    Kind = AssistantPlanStepKind.Read,
                    ToolName = AssistantToolNames.AppsList
                },
                new AssistantPlanStep
                {
                    Index = 2,
                    Title = "アプリを開く（確認が必要）",
                    Kind = AssistantPlanStepKind.ConfirmAction,
                    ToolName = AssistantToolNames.AppsOpen,
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
        || text.Contains("の音楽", StringComparison.Ordinal)
        || ((text.Contains("再生", StringComparison.Ordinal)
             || text.Contains("play", StringComparison.OrdinalIgnoreCase)
             || text.Contains("開いて", StringComparison.Ordinal)
             || text.Contains("open", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("曲", StringComparison.Ordinal)
                || text.Contains("音楽", StringComparison.Ordinal)
                || text.Contains("song", StringComparison.OrdinalIgnoreCase)
                || text.Contains("music", StringComparison.OrdinalIgnoreCase)
                || text.Contains("spotify", StringComparison.OrdinalIgnoreCase)
                || text.Contains("アルバム", StringComparison.Ordinal)
                || text.Contains("album", StringComparison.OrdinalIgnoreCase)
                || text.Contains("アーティスト", StringComparison.Ordinal)
                || text.Contains("artist", StringComparison.OrdinalIgnoreCase)));

    private static bool LooksLikeCodingEnvironment(string text) =>
        text.Contains("プログラミング環境", StringComparison.Ordinal)
        || text.Contains("開発環境", StringComparison.Ordinal)
        || text.Contains("コーディング環境", StringComparison.Ordinal)
        || text.Contains("coding environment", StringComparison.OrdinalIgnoreCase)
        || text.Contains("programming environment", StringComparison.OrdinalIgnoreCase)
        || text.Contains("dev environment", StringComparison.OrdinalIgnoreCase)
        || ((text.Contains("プログラミング", StringComparison.Ordinal)
             || text.Contains("コーディング", StringComparison.Ordinal)
             || text.Contains("coding", StringComparison.OrdinalIgnoreCase)
             || text.Contains("programming", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("環境", StringComparison.Ordinal)
                || text.Contains("environment", StringComparison.OrdinalIgnoreCase)
                || text.Contains("workspace", StringComparison.OrdinalIgnoreCase)
                || text.Contains("セットアップ", StringComparison.Ordinal)
                || text.Contains("setup", StringComparison.OrdinalIgnoreCase)))
        || (text.Contains("作業モード", StringComparison.Ordinal)
            && (text.Contains("開発", StringComparison.Ordinal)
                || text.Contains("コーディング", StringComparison.Ordinal)
                || text.Contains("programming", StringComparison.OrdinalIgnoreCase)));

    private static bool LooksLikeOpenApp(string text) =>
        (text.Contains("アプリ", StringComparison.Ordinal)
         || text.Contains(" application", StringComparison.OrdinalIgnoreCase)
         || (text.Contains("app", StringComparison.OrdinalIgnoreCase)
             && !text.Contains("happy", StringComparison.OrdinalIgnoreCase)))
        && (text.Contains("開いて", StringComparison.Ordinal)
            || text.Contains("起動", StringComparison.Ordinal)
            || text.Contains("open", StringComparison.OrdinalIgnoreCase)
            || text.Contains("launch", StringComparison.OrdinalIgnoreCase)
            || text.Contains("start", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeUsualSchedule(string text) =>
        text.Contains("いつも", StringComparison.Ordinal)
        || text.Contains("usual", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAddSchedule(string text)
    {
        if (LooksLikeUsualSchedule(text))
        {
            return false;
        }

        // 「19時勉強、20時食事…をいれて」 — no 予定 word required when times parse.
        if (LocalScheduleParser.LooksLikeScheduleWrite(text))
        {
            return true;
        }

        var wantsWrite = text.Contains("入れて", StringComparison.Ordinal)
                         || text.Contains("いれて", StringComparison.Ordinal)
                         || text.Contains("追加", StringComparison.Ordinal)
                         || text.Contains("反映", StringComparison.Ordinal)
                         || text.Contains("add", StringComparison.OrdinalIgnoreCase);
        if (!wantsWrite)
        {
            return false;
        }

        return text.Contains("予定", StringComparison.Ordinal)
               || text.Contains("スケジュール", StringComparison.Ordinal)
               || text.Contains("カレンダー", StringComparison.Ordinal)
               || text.Contains("ウィジェット", StringComparison.Ordinal)
               || text.Contains("schedule", StringComparison.OrdinalIgnoreCase)
               || text.Contains("calendar", StringComparison.OrdinalIgnoreCase)
               || text.Contains("widget", StringComparison.OrdinalIgnoreCase)
               || text.Contains("event", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ローカル", StringComparison.Ordinal);
    }

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

    private static bool LooksLikeContinue(string text) =>
        text.Contains("続け", StringComparison.Ordinal)
        || text.Contains("再開", StringComparison.Ordinal)
        || text.Contains("continue", StringComparison.OrdinalIgnoreCase)
        || text.Contains("resume", StringComparison.OrdinalIgnoreCase)
        || (text.Contains("開発", StringComparison.Ordinal)
            && (text.Contains("したい", StringComparison.Ordinal) || text.Contains("続け", StringComparison.Ordinal)));

    private static bool LooksLikeFocus(string text)
    {
        // Normalize common hiragana / typo forms (ぽもどーと etc.).
        var folded = text
            .Replace("ぽもどーと", "ポモドーロ", StringComparison.Ordinal)
            .Replace("ぽもどーろ", "ポモドーロ", StringComparison.Ordinal)
            .Replace("ぽもドーロ", "ポモドーロ", StringComparison.Ordinal)
            .Replace("ポモドーと", "ポモドーロ", StringComparison.Ordinal)
            .Replace("ポモドロ", "ポモドーロ", StringComparison.Ordinal);

        if (folded.Contains("ポモドーロ", StringComparison.Ordinal)
            || folded.Contains("pomodoro", StringComparison.OrdinalIgnoreCase)
            || folded.Contains("ポモ", StringComparison.Ordinal)
            || folded.Contains("ぽも", StringComparison.Ordinal))
        {
            return true;
        }

        var wantsTimer = folded.Contains("タイマー", StringComparison.Ordinal)
                         || folded.Contains("timer", StringComparison.OrdinalIgnoreCase);
        if (wantsTimer
            && (folded.Contains("つけて", StringComparison.Ordinal)
                || folded.Contains("付けて", StringComparison.Ordinal)
                || folded.Contains("開始", StringComparison.Ordinal)
                || folded.Contains("start", StringComparison.OrdinalIgnoreCase)
                || folded.Contains("on", StringComparison.OrdinalIgnoreCase)
                || folded.Contains("集中", StringComparison.Ordinal)
                || folded.Contains("focus", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return (folded.Contains("focus", StringComparison.OrdinalIgnoreCase)
                || folded.Contains("集中", StringComparison.Ordinal))
               && (folded.Contains("開始", StringComparison.Ordinal)
                   || folded.Contains("start", StringComparison.OrdinalIgnoreCase)
                   || folded.Contains("始めて", StringComparison.Ordinal)
                   || folded.Contains("したい", StringComparison.Ordinal)
                   || folded.Contains("やりたい", StringComparison.Ordinal)
                   || folded.Contains("now", StringComparison.OrdinalIgnoreCase)
                   || folded.Contains("今", StringComparison.Ordinal));
    }

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
