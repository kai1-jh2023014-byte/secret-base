using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Calendar;
using SecretBase.Core.Integration;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Widgets.Theming;
using Windows.UI;

namespace SecretBase.Widgets.Calendar;

/// <summary>
/// Month calendar + day agenda. Uses <see cref="CalendarService"/> (provider-agnostic).
/// Does not own Overlay / HWND logic — hosted inside WidgetFrame only.
/// </summary>
public sealed partial class CalendarWidgetView : UserControl, IDisposable
{
    private static readonly string[] WeekdayLabels = ["S", "M", "T", "W", "T", "F", "S"];

    private CalendarWidgetConfiguration _configuration = CalendarWidgetConfiguration.CreateDefault();
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private CalendarService? _service;
    private ICalendarAgendaCache? _cache;
    private Func<string, bool>? _openUrl;
    private Action<CalendarWidgetConfiguration>? _onConfigurationChanged;
    private Action<string>? _onReminder;
    private IIntegrationMemory? _integrations;
    private ThemeDefinition? _theme;
    private readonly CalendarReminderMonitor _reminders = new();
    private int _refreshGate;
    private int _connectGate;
    private DispatcherTimer? _pollTimer;
    private DispatcherTimer? _reminderHideTimer;
    private bool _disposed;
    private bool _bulkMode;
    private bool _entered;
    private DateOnly _visibleMonth;
    private DateOnly _selectedDay;
    private IReadOnlyList<CalendarEvent> _monthEvents = [];
    private IReadOnlyList<CalendarEvent> _dayEvents = [];

    public CalendarWidgetView()
    {
        InitializeComponent();
        var now = DateOnly.FromDateTime(DateTime.Now);
        _visibleMonth = new DateOnly(now.Year, now.Month, 1);
        _selectedDay = now;
        BuildWeekdayHeader();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        CalendarWidgetConfiguration configuration,
        CalendarService service,
        ITimeProvider? timeProvider = null,
        Func<string, bool>? openUrl = null,
        Action<CalendarWidgetConfiguration>? onConfigurationChanged = null,
        ICalendarAgendaCache? cache = null,
        IIntegrationMemory? integrations = null,
        Action<string>? onReminder = null)
    {
        _configuration = configuration;
        _service = service;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _openUrl = openUrl;
        _onConfigurationChanged = onConfigurationChanged;
        _cache = cache;
        _integrations = integrations;
        _onReminder = onReminder;

        var now = _timeProvider.GetLocalNow();
        _selectedDay = DateOnly.FromDateTime(now.DateTime);
        _visibleMonth = new DateOnly(_selectedDay.Year, _selectedDay.Month, 1);
        NotifyToggle.IsChecked = _configuration.NotifyOnEventStart;
        PrefillAddTimes();
        UpdateProviderLabel();
        UpdateConnectVisibility();
        UpdateAddModeChrome();
        _ = RefreshCalendarAsync();
    }

    /// <summary>Reload agenda (e.g. after Base AI writes local events).</summary>
    public void RequestRefresh() => _ = RefreshCalendarAsync();

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, null, theme);
        HeaderAccent.Background = ThemePainter.Brush(theme.Accent, 0.9);
        WidgetSurfaceStyle.ApplyMuted(ProviderLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(BulkHintLabel, theme);
        WidgetSurfaceStyle.ApplyBody(SelectedDayLabel, theme);
        WidgetSurfaceStyle.ApplyBody(ReminderText, theme);
        ReminderBanner.Background = ThemePainter.Brush(theme.Accent, 0.45);
        ReminderBanner.BorderBrush = ThemePainter.Brush(theme.Border, 0.35);

        WidgetSurfaceStyle.ApplyGhostButton(PrevMonthButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(NextMonthButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(TodayJumpButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(RefreshButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(OpenCalendarButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(AddEventButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(BulkAddButton, theme);
        WidgetSurfaceStyle.ApplyActionButton(ConnectButton, theme, accent: true);
        StyleModeButtons();
        StyleNotifyToggle();
        RestyleAgendaItems();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PrefillAddTimes();
        EnsurePollTimer();
        if (!_entered)
        {
            _entered = true;
            WidgetSurfaceStyle.FadeOpacity(this, 1, 220);
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
        else
        {
            Opacity = 1;
        }

        _ = RefreshCalendarAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopPollTimer();

    private void EnsurePollTimer()
    {
        _pollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _pollTimer.Tick -= PollTimer_Tick;
        _pollTimer.Tick += PollTimer_Tick;
        if (!_pollTimer.IsEnabled)
        {
            _pollTimer.Start();
        }
    }

    private void StopPollTimer()
    {
        if (_pollTimer is null)
        {
            return;
        }

        _pollTimer.Stop();
        _pollTimer.Tick -= PollTimer_Tick;
    }

    private void PollTimer_Tick(object? sender, object e)
    {
        CheckReminders(_monthEvents);
        _ = RefreshCalendarAsync();
    }

    private void PrefillAddTimes()
    {
        var now = _timeProvider.GetLocalNow();
        if (string.IsNullOrWhiteSpace(AddTimeBox.Text))
        {
            AddTimeBox.Text = string.Create(CultureInfo.InvariantCulture, $"{now.Hour:00}:00");
        }

        if (string.IsNullOrWhiteSpace(AddEndTimeBox.Text))
        {
            var endHour = (now.Hour + 1) % 24;
            AddEndTimeBox.Text = string.Create(CultureInfo.InvariantCulture, $"{endHour:00}:00");
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = RefreshCalendarAsync();

    private void PrevMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(-1);
        _ = RefreshCalendarAsync();
    }

    private void NextMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(1);
        _ = RefreshCalendarAsync();
    }

    private void TodayJumpButton_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        _selectedDay = today;
        _visibleMonth = new DateOnly(today.Year, today.Month, 1);
        _ = RefreshCalendarAsync();
    }

    private void NotifyToggle_Click(object sender, RoutedEventArgs e)
    {
        _configuration.NotifyOnEventStart = NotifyToggle.IsChecked == true;
        PersistConfiguration();
        StyleNotifyToggle();
        StatusLabel.Visibility = Visibility.Visible;
        StatusLabel.Text = _configuration.NotifyOnEventStart
            ? "Notifications on — you'll be told when an event starts."
            : "Notifications off.";
    }

    private void SingleModeButton_Click(object sender, RoutedEventArgs e)
    {
        _bulkMode = false;
        UpdateAddModeChrome();
    }

    private void BulkModeButton_Click(object sender, RoutedEventArgs e)
    {
        _bulkMode = true;
        UpdateAddModeChrome();
    }

    private void UpdateAddModeChrome()
    {
        SingleAddPanel.Visibility = _bulkMode ? Visibility.Collapsed : Visibility.Visible;
        BulkAddPanel.Visibility = _bulkMode ? Visibility.Visible : Visibility.Collapsed;
        StyleModeButtons();
    }

    private void StyleModeButtons()
    {
        if (_theme is null)
        {
            return;
        }

        if (_bulkMode)
        {
            WidgetSurfaceStyle.ApplyGhostButton(SingleModeButton, _theme);
            WidgetSurfaceStyle.ApplyActionButton(BulkModeButton, _theme, accent: true);
        }
        else
        {
            WidgetSurfaceStyle.ApplyActionButton(SingleModeButton, _theme, accent: true);
            WidgetSurfaceStyle.ApplyGhostButton(BulkModeButton, _theme);
        }
    }

    private void StyleNotifyToggle()
    {
        if (_theme is null)
        {
            return;
        }

        var on = NotifyToggle.IsChecked == true;
        NotifyToggle.FontFamily = new FontFamily(_theme.FontFamily);
        NotifyToggle.Background = ThemePainter.Brush(_theme.Accent, on ? 0.55 : 0.12);
        NotifyToggle.Foreground = ThemePainter.Brush(_theme.WidgetForeground, on ? 1 : 0.7);
        NotifyToggle.BorderBrush = ThemePainter.Brush(_theme.Border, on ? 0.2 : 0.35);
        NotifyToggle.BorderThickness = new Thickness(1);
        NotifyToggle.CornerRadius = new CornerRadius(Math.Max(8, _theme.CornerRadius * 0.45));
    }

    private void PersistConfiguration()
    {
        try
        {
            _onConfigurationChanged?.Invoke(_configuration);
        }
        catch
        {
            // Host persist is best-effort.
        }
    }

    private TimeOnly? ResolveTime(TextBox box, DateTimeOffset now, int? fallbackHour = null)
    {
        var timeText = box.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(timeText))
        {
            return new TimeOnly(fallbackHour ?? now.Hour, 0);
        }

        if (TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, out var parsed)
            || TimeOnly.TryParse(timeText, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return null;
    }

    private void AddEventButton_Click(object sender, RoutedEventArgs e)
    {
        var local = _service?.Providers.OfType<LocalCalendarProvider>().FirstOrDefault();
        if (local is null)
        {
            ShowStatus("Local calendar is unavailable.");
            return;
        }

        var title = AddTitleBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus("Enter an event title.");
            return;
        }

        var now = _timeProvider.GetLocalNow();
        var startTime = ResolveTime(AddTimeBox, now);
        if (startTime is null)
        {
            ShowStatus("Use start time like 16:00.");
            return;
        }

        var endTime = ResolveTime(AddEndTimeBox, now, (startTime.Value.Hour + 1) % 24);
        if (endTime is null)
        {
            ShowStatus("Use end time like 17:00.");
            return;
        }

        var start = new DateTimeOffset(_selectedDay.ToDateTime(startTime.Value), now.Offset);
        var end = new DateTimeOffset(_selectedDay.ToDateTime(endTime.Value), now.Offset);
        if (end <= start)
        {
            end = start.AddHours(1);
        }

        local.AddEvent(title, start, end);
        AddTitleBox.Text = string.Empty;
        AddTimeBox.Text = string.Create(CultureInfo.InvariantCulture, $"{endTime.Value.Hour:00}:{endTime.Value.Minute:00}");
        var nextEnd = endTime.Value.AddHours(1);
        AddEndTimeBox.Text = string.Create(CultureInfo.InvariantCulture, $"{nextEnd.Hour:00}:{nextEnd.Minute:00}");
        ShowStatus($"Added {FormatClock(startTime.Value)} 〜 {FormatClock(endTime.Value)} (local).");
        WidgetSurfaceStyle.PulseScale(RootBorder);
        _ = RefreshCalendarAsync();
    }

    private void BulkAddButton_Click(object sender, RoutedEventArgs e)
    {
        var local = _service?.Providers.OfType<LocalCalendarProvider>().FirstOrDefault();
        if (local is null)
        {
            ShowStatus("Local calendar is unavailable.");
            return;
        }

        var text = BulkTextBox.Text ?? string.Empty;
        if (!CalendarBulkEntryParser.TryParse(text, out var slots) || slots.Count == 0)
        {
            ShowStatus("Could not parse events. Try: 19時勉強 or 09:00-10:00 Study");
            return;
        }

        var now = _timeProvider.GetLocalNow();
        var added = 0;
        foreach (var slot in slots)
        {
            var start = new DateTimeOffset(
                _selectedDay.ToDateTime(new TimeOnly(slot.Hour, slot.Minute)),
                now.Offset);
            var end = start.AddMinutes(Math.Max(15, slot.DurationMinutes));
            local.AddEvent(slot.Title, start, end);
            added++;
        }

        BulkTextBox.Text = string.Empty;
        ShowStatus($"Added {added} event(s) on {_selectedDay:MMM d}.");
        WidgetSurfaceStyle.PulseScale(RootBorder);
        _ = RefreshCalendarAsync();
    }

    private void DeleteLocalEvent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string eventId } || string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        var local = _service?.Providers.OfType<LocalCalendarProvider>().FirstOrDefault();
        if (local is null)
        {
            ShowStatus("Local calendar is unavailable.");
            return;
        }

        if (!local.TryRemoveEvent(eventId, out var removed) || removed is null)
        {
            ShowStatus("Could not remove that local event.");
            return;
        }

        _reminders.Forget(eventId);
        ShowStatus($"Removed local: {removed.Title}");
        _ = RefreshCalendarAsync();
    }

    private void OpenCalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var url = _configuration.OpenCalendarUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = CalendarWidgetConfiguration.DefaultOpenCalendarUrl;
        }

        var withOpen = _service?.Providers.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.OpenUrl));
        if (withOpen?.OpenUrl is { Length: > 0 } providerUrl)
        {
            url = providerUrl;
        }

        _ = _openUrl?.Invoke(url);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_service is null || Interlocked.Exchange(ref _connectGate, 1) == 1)
        {
            return;
        }

        try
        {
            var authProvider = _service.Providers.FirstOrDefault(p =>
                p.Capabilities.HasFlag(CalendarProviderCapabilities.Authentication)
                && p.AuthStatus is CalendarAuthStatus.Disconnected
                    or CalendarAuthStatus.NotConfigured
                    or CalendarAuthStatus.Error);

            if (authProvider is null)
            {
                return;
            }

            ShowStatus($"Connecting {authProvider.DisplayName}…");
            await authProvider.AuthenticateAsync();
            ShowStatus($"{authProvider.DisplayName} connected.");
            _integrations?.RememberConnected(
                IntegrationMemoryIds.GoogleCalendar,
                authProvider.DisplayName,
                inAppExperience: true);
            await RefreshCalendarAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Connect failed: {ex.Message}");
        }
        finally
        {
            UpdateConnectVisibility();
            Interlocked.Exchange(ref _connectGate, 0);
        }
    }

    private async Task RefreshCalendarAsync()
    {
        if (_service is null || _disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _refreshGate, 1) == 1)
        {
            return;
        }

        try
        {
            HeaderText.Text = CalendarMonthLayout.FormatMonthTitle(_visibleMonth.Year, _visibleMonth.Month);
            var now = _timeProvider.GetLocalNow();
            var today = DateOnly.FromDateTime(now.DateTime);

            CalendarAgendaSnapshot monthSnap;
            try
            {
                monthSnap = await _service.GetAgendaSnapshotAsync(
                    CalendarQuery.ForMonth(_visibleMonth));
            }
            catch (Exception ex)
            {
                AgendaList.Children.Clear();
                AgendaList.Children.Add(CreateMutedLine($"Could not load calendar: {ex.Message}"));
                ShowStatus("Calendar unavailable");
                return;
            }

            _monthEvents = monthSnap.Events;
            CheckReminders(_monthEvents);

            var anySuccess = monthSnap.ProviderResults.Any(r => r.Succeeded);
            if (anySuccess)
            {
                try
                {
                    _cache?.Save(_monthEvents, DateTimeOffset.UtcNow);
                }
                catch
                {
                    // Cache is best-effort.
                }
            }

            RenderMonthGrid(today);
            await RefreshDayAgendaAsync(monthSnap);
            UpdateProviderLabel(_dayEvents, monthSnap);
            UpdateConnectVisibility();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshGate, 0);
        }
    }

    private async Task RefreshDayAgendaAsync(CalendarAgendaSnapshot? monthSnap)
    {
        AgendaList.Children.Clear();
        SelectedDayLabel.Text = FormatSelectedDayLabel(_selectedDay);

        CalendarAgendaSnapshot daySnap;
        try
        {
            daySnap = await (_service?.GetAgendaSnapshotAsync(CalendarQuery.ForDay(_selectedDay))
                             ?? Task.FromResult(new CalendarAgendaSnapshot()));
        }
        catch (Exception ex)
        {
            AgendaList.Children.Add(CreateMutedLine($"Could not load day: {ex.Message}"));
            return;
        }

        _dayEvents = daySnap.Events;
        if (_dayEvents.Count == 0)
        {
            AgendaList.Children.Add(CreateMutedLine("No events this day"));
        }
        else
        {
            foreach (var ev in _dayEvents)
            {
                AgendaList.Children.Add(CreateEventBlock(ev));
            }
        }

        var failures = daySnap.ProviderResults.Where(r => !r.Succeeded).ToList();
        if (failures.Count > 0
            && !StatusLabel.Text.StartsWith("Added", StringComparison.Ordinal)
            && !StatusLabel.Text.StartsWith("Removed", StringComparison.Ordinal)
            && !StatusLabel.Text.StartsWith("Notifications", StringComparison.Ordinal)
            && !StatusLabel.Text.StartsWith("Connecting", StringComparison.Ordinal)
            && !StatusLabel.Text.Contains("connected", StringComparison.OrdinalIgnoreCase))
        {
            var failureText = string.Join(
                " · ",
                failures.Select(f => $"{f.DisplayName} unavailable").Distinct(StringComparer.OrdinalIgnoreCase));
            ShowStatus(failureText);
        }
        else if (monthSnap is not null
                 && StatusLabel.Visibility == Visibility.Visible
                 && StatusLabel.Text.Equals("Calendar unavailable", StringComparison.Ordinal))
        {
            // keep
        }
    }

    private void RenderMonthGrid(DateOnly today)
    {
        MonthGrid.Children.Clear();
        MonthGrid.RowDefinitions.Clear();
        MonthGrid.ColumnDefinitions.Clear();
        for (var c = 0; c < 7; c++)
        {
            MonthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < 6; r++)
        {
            MonthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var cells = CalendarMonthLayout.Build(
            _visibleMonth.Year,
            _visibleMonth.Month,
            today,
            _selectedDay,
            _monthEvents);

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var button = CreateDayCellButton(cell);
            Grid.SetRow(button, i / 7);
            Grid.SetColumn(button, i % 7);
            MonthGrid.Children.Add(button);
        }
    }

    private void BuildWeekdayHeader()
    {
        WeekdayHeader.ColumnDefinitions.Clear();
        WeekdayHeader.Children.Clear();
        for (var i = 0; i < 7; i++)
        {
            WeekdayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock
            {
                Text = WeekdayLabels[i],
                FontSize = 10,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Center,
                CharacterSpacing = 40
            };
            Grid.SetColumn(label, i);
            WeekdayHeader.Children.Add(label);
        }
    }

    private Button CreateDayCellButton(CalendarMonthLayout.Cell cell)
    {
        var stack = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var dayText = new TextBlock
        {
            Text = cell.Date.Day.ToString(CultureInfo.InvariantCulture),
            FontSize = 11,
            FontWeight = cell.IsToday || cell.IsSelected ? FontWeights.SemiBold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = cell.IsCurrentMonth ? 1 : 0.35
        };
        stack.Children.Add(dayText);

        if (cell.EventCount > 0)
        {
            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var count = Math.Min(cell.EventCount, 3);
            for (var i = 0; i < count; i++)
            {
                dots.Children.Add(new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = _theme is not null
                        ? ThemePainter.Brush(_theme.Accent, 0.95)
                        : new SolidColorBrush(Color.FromArgb(255, 122, 158, 134))
                });
            }

            stack.Children.Add(dots);
        }

        var button = new Button
        {
            Content = stack,
            Tag = cell.Date,
            MinWidth = 0,
            MinHeight = 28,
            Padding = new Thickness(2, 3, 2, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += DayCell_Click;

        if (_theme is not null)
        {
            dayText.FontFamily = new FontFamily(_theme.FontFamily);
            dayText.Foreground = ThemePainter.Brush(
                cell.IsSelected ? _theme.WidgetForeground : _theme.ForegroundMuted,
                cell.IsCurrentMonth ? 1 : 0.45);
            button.CornerRadius = new CornerRadius(Math.Max(6, _theme.CornerRadius * 0.35));
            button.BorderThickness = new Thickness(cell.IsToday || cell.IsSelected ? 1 : 0);
            button.BorderBrush = ThemePainter.Brush(_theme.Accent, cell.IsSelected ? 0.7 : 0.35);
            button.Background = cell.IsSelected
                ? ThemePainter.Brush(_theme.Accent, 0.35)
                : cell.IsToday
                    ? ThemePainter.Brush(_theme.Accent, 0.14)
                    : new SolidColorBrush(Colors.Transparent);
        }

        ToolTipService.SetToolTip(
            button,
            cell.EventCount == 0
                ? cell.Date.ToString("ddd MMM d", CultureInfo.CurrentCulture)
                : $"{cell.Date:ddd MMM d} · {cell.EventCount} event(s)");
        return button;
    }

    private void DayCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateOnly day })
        {
            return;
        }

        _selectedDay = day;
        if (day.Month != _visibleMonth.Month || day.Year != _visibleMonth.Year)
        {
            _visibleMonth = new DateOnly(day.Year, day.Month, 1);
        }

        WidgetSurfaceStyle.FadeOpacity(AgendaList, 0.35, 80);
        _ = RefreshCalendarAsync().ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(() => WidgetSurfaceStyle.FadeOpacity(AgendaList, 1, 160));
        });
    }

    private void CheckReminders(IReadOnlyList<CalendarEvent> events)
    {
        if (_disposed || events.Count == 0)
        {
            return;
        }

        var now = _timeProvider.GetLocalNow();
        var due = _reminders.CollectDue(
            now,
            events,
            leadMinutes: _configuration.NotifyLeadMinutes,
            enabled: _configuration.NotifyOnEventStart);

        foreach (var reminder in due)
        {
            var notice = CalendarReminderMonitor.FormatNotice(reminder);
            ShowReminderBanner(notice);
            try
            {
                _onReminder?.Invoke(notice);
            }
            catch
            {
                // Host callback is best-effort.
            }
        }
    }

    private void ShowReminderBanner(string text)
    {
        ReminderText.Text = text;
        ReminderBanner.Visibility = Visibility.Visible;
        WidgetSurfaceStyle.FadeOpacity(ReminderBanner, 1, 180);
        WidgetSurfaceStyle.PulseScale(ReminderBanner);

        _reminderHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _reminderHideTimer.Tick -= ReminderHideTimer_Tick;
        _reminderHideTimer.Tick += ReminderHideTimer_Tick;
        _reminderHideTimer.Stop();
        _reminderHideTimer.Start();
    }

    private void ReminderHideTimer_Tick(object? sender, object e)
    {
        _reminderHideTimer?.Stop();
        WidgetSurfaceStyle.FadeOpacity(ReminderBanner, 0, 220);
        // Collapse after fade.
        var collapse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        collapse.Tick += (_, _) =>
        {
            collapse.Stop();
            if (ReminderBanner.Opacity < 0.05)
            {
                ReminderBanner.Visibility = Visibility.Collapsed;
            }
        };
        collapse.Start();
    }

    private void UpdateConnectVisibility()
    {
        if (_service is null)
        {
            ConnectButton.Visibility = Visibility.Collapsed;
            return;
        }

        var needsAuth = _service.Providers.Any(p =>
            p.Capabilities.HasFlag(CalendarProviderCapabilities.Authentication)
            && p.IsConfigured
            && p.AuthStatus is CalendarAuthStatus.Disconnected or CalendarAuthStatus.Error);

        var missingClient = _service.Providers.Any(p =>
            p.Capabilities.HasFlag(CalendarProviderCapabilities.Authentication)
            && p.AuthStatus == CalendarAuthStatus.NotConfigured);

        var googleConnected = _service.Providers.Any(p =>
            string.Equals(p.ProviderId, CalendarProviderIds.Google, StringComparison.Ordinal)
            && p.AuthStatus == CalendarAuthStatus.Connected);
        if (googleConnected)
        {
            _integrations?.RememberConnected(
                IntegrationMemoryIds.GoogleCalendar,
                "Google Calendar",
                inAppExperience: true);
            OpenCalendarButton.Opacity = 0.55;
            ToolTipService.SetToolTip(
                OpenCalendarButton,
                "Optional: open Google Calendar in the browser. Agenda stays in this widget.");
        }
        else
        {
            OpenCalendarButton.Opacity = 1;
            ToolTipService.SetToolTip(OpenCalendarButton, "Open calendar in the browser");
        }

        ConnectButton.Visibility = needsAuth && !missingClient
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateProviderLabel(
        IReadOnlyList<CalendarEvent>? events = null,
        CalendarAgendaSnapshot? snapshot = null)
    {
        if (_service is null)
        {
            ProviderLabel.Text = "Local";
            return;
        }

        var names = _service.Providers
            .Where(p => p.IsConfigured && p.ProviderId != "empty")
            .Select(p => p.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (events is { Count: > 0 })
        {
            var fromEvents = events
                .Select(e => e.Source ?? e.CalendarName ?? e.Provider)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fromEvents.Count > 0)
            {
                names = fromEvents!;
            }
        }
        else if (snapshot is not null)
        {
            var connected = snapshot.ProviderResults
                .Where(r => r.Succeeded)
                .Select(r => r.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (connected.Count > 0)
            {
                names = connected;
            }
        }

        ProviderLabel.Text = names.Count == 0 ? "Local" : string.Join(" · ", names);
    }

    private UIElement CreateEventBlock(CalendarEvent ev)
    {
        var timeLabel = FormatTimeRange(ev);
        var textPanel = new StackPanel { Spacing = 2 };
        var time = new TextBlock
        {
            Text = timeLabel,
            FontSize = 11,
            Opacity = 0.85,
            CharacterSpacing = 20
        };
        var title = new TextBlock
        {
            Text = ev.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        if (_theme is not null)
        {
            time.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            title.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
            time.FontFamily = new FontFamily(_theme.FontFamily);
            title.FontFamily = new FontFamily(_theme.FontFamily);
        }

        textPanel.Children.Add(time);
        textPanel.Children.Add(title);
        if (!string.IsNullOrWhiteSpace(ev.Location))
        {
            var loc = new TextBlock
            {
                Text = ev.Location,
                FontSize = 11,
                Opacity = 0.75,
                TextWrapping = TextWrapping.WrapWholeWords
            };
            if (_theme is not null)
            {
                loc.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
                loc.FontFamily = new FontFamily(_theme.FontFamily);
            }

            textPanel.Children.Add(loc);
        }

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var accent = ParseColor(ev.Color) ?? (_theme is not null
            ? ThemePainter.ParseColor(_theme.Accent)
            : Colors.CornflowerBlue);

        var rail = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(accent),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 2),
            MinHeight = 28
        };
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(textPanel, 1);
        row.Children.Add(rail);
        row.Children.Add(textPanel);

        var isLocal = string.Equals(ev.Provider, CalendarProviderIds.Local, StringComparison.OrdinalIgnoreCase);
        if (isLocal)
        {
            var delete = new Button
            {
                Content = "×",
                MinWidth = 28,
                MinHeight = 28,
                Padding = new Thickness(0),
                Tag = ev.Id,
                VerticalAlignment = VerticalAlignment.Top
            };
            delete.Click += DeleteLocalEvent_Click;
            ToolTipService.SetToolTip(delete, "Remove local event (not Google / not disk files)");
            if (_theme is not null)
            {
                WidgetSurfaceStyle.ApplyGhostButton(delete, _theme);
            }

            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
        }

        return row;
    }

    private TextBlock CreateMutedLine(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 13,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        if (_theme is not null)
        {
            block.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            block.FontFamily = new FontFamily(_theme.FontFamily);
        }

        return block;
    }

    private void RestyleAgendaItems()
    {
        foreach (var child in WeekdayHeader.Children.OfType<TextBlock>())
        {
            if (_theme is not null)
            {
                child.FontFamily = new FontFamily(_theme.FontFamily);
                child.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            }
        }

        _ = RefreshCalendarAsync();
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Visibility = Visibility.Visible;
        StatusLabel.Text = message;
    }

    private static string FormatSelectedDayLabel(DateOnly day)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var prefix = day == today ? "Today · " : day == today.AddDays(1) ? "Tomorrow · " : string.Empty;
        return prefix + day.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
    }

    private static string FormatTimeRange(CalendarEvent ev)
    {
        if (ev.IsAllDay)
        {
            return "All day";
        }

        var start = ev.Start.DateTime;
        var end = ev.End.DateTime;
        return string.Create(CultureInfo.InvariantCulture, $"{start:HH:mm}  〜  {end:HH:mm}");
    }

    private static string FormatClock(TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hour:00}:{time.Minute:00}");

    private static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var s = hex.Trim();
        if (s.StartsWith('#'))
        {
            s = s[1..];
        }

        if (s.Length == 6
            && byte.TryParse(s[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return Color.FromArgb(255, r, g, b);
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        StopPollTimer();
        if (_reminderHideTimer is not null)
        {
            _reminderHideTimer.Stop();
            _reminderHideTimer.Tick -= ReminderHideTimer_Tick;
        }
    }
}
