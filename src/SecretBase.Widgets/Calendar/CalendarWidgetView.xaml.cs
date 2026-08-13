using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Calendar;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Calendar;

/// <summary>
/// Local-only month calendar. Uses Core <see cref="CalendarMonthBuilder"/> +
/// <see cref="ICalendarEventSource"/> — no Outlook/Google network access in v0.1.
/// </summary>
public sealed partial class CalendarWidgetView : UserControl
{
    private CalendarWidgetConfiguration _configuration = CalendarWidgetConfiguration.CreateDefault();
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private Action<CalendarWidgetConfiguration>? _onConfigurationChanged;
    private ThemeDefinition? _theme;
    private int _viewYear;
    private int _viewMonth;
    private IReadOnlyList<CalendarDayCell> _cells = Array.Empty<CalendarDayCell>();

    public CalendarWidgetView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshMonth();
    }

    public void Initialize(
        CalendarWidgetConfiguration configuration,
        ITimeProvider? timeProvider = null,
        Action<CalendarWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _onConfigurationChanged = onConfigurationChanged;

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        if (_configuration.FollowToday || _configuration.PinnedYear < 1 || _configuration.PinnedMonth is < 1 or > 12)
        {
            _viewYear = today.Year;
            _viewMonth = today.Month;
        }
        else
        {
            _viewYear = _configuration.PinnedYear;
            _viewMonth = _configuration.PinnedMonth;
        }

        RefreshMonth();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        MonthTitle.FontFamily = font;
        MonthTitle.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        EventSummary.FontFamily = font;
        EventSummary.Foreground = ThemePainter.Brush(theme.ForegroundMuted);

        StyleNavButton(PrevButton, theme);
        StyleNavButton(NextButton, theme);
        StyleNavButton(TodayButton, theme);
        RefreshMonth();
    }

    private static void StyleNavButton(Button button, ThemeDefinition theme)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Accent, 0.55);
        button.BorderThickness = new Thickness(1);
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        ShiftMonth(-1);
        PersistPinnedMonth(followToday: false);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        ShiftMonth(1);
        PersistPinnedMonth(followToday: false);
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        _viewYear = today.Year;
        _viewMonth = today.Month;
        PersistPinnedMonth(followToday: true);
        RefreshMonth();
    }

    private void ShiftMonth(int delta)
    {
        var date = new DateOnly(_viewYear, _viewMonth, 1).AddMonths(delta);
        _viewYear = date.Year;
        _viewMonth = date.Month;
        RefreshMonth();
    }

    private void PersistPinnedMonth(bool followToday)
    {
        _configuration.FollowToday = followToday;
        _configuration.PinnedYear = _viewYear;
        _configuration.PinnedMonth = _viewMonth;
        _onConfigurationChanged?.Invoke(_configuration);
    }

    private void RefreshMonth()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var source = _configuration.CreateEventSource();
        _cells = CalendarMonthBuilder.Build(
            _viewYear,
            _viewMonth,
            _configuration.FirstDayOfWeek,
            today,
            source);

        MonthTitle.Text = new DateOnly(_viewYear, _viewMonth, 1)
            .ToString("yyyy / MM", CultureInfo.InvariantCulture);

        BuildWeekdayHeader();
        BuildDayGrid();
        UpdateEventSummary(today);
    }

    private void BuildWeekdayHeader()
    {
        WeekdayHeader.Children.Clear();
        WeekdayHeader.ColumnDefinitions.Clear();
        var labels = CalendarMonthBuilder.WeekdayLabels(_configuration.FirstDayOfWeek);
        for (var i = 0; i < labels.Count; i++)
        {
            WeekdayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock
            {
                Text = labels[i],
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.75
            };
            if (_theme is not null)
            {
                text.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
                text.FontFamily = new FontFamily(_theme.FontFamily);
            }

            Grid.SetColumn(text, i);
            WeekdayHeader.Children.Add(text);
        }
    }

    private void BuildDayGrid()
    {
        DayGrid.Children.Clear();
        DayGrid.RowDefinitions.Clear();
        DayGrid.ColumnDefinitions.Clear();

        for (var c = 0; c < CalendarMonthBuilder.DaysInWeek; c++)
        {
            DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < CalendarMonthBuilder.WeeksInGrid; r++)
        {
            DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            var row = i / CalendarMonthBuilder.DaysInWeek;
            var col = i % CalendarMonthBuilder.DaysInWeek;

            var dayButton = new Button
            {
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Tag = cell
            };
            dayButton.Click += DayButton_Click;

            var panel = new StackPanel
            {
                Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dayText = new TextBlock
            {
                Text = cell.Date.Day.ToString(CultureInfo.InvariantCulture),
                FontSize = 13,
                FontWeight = cell.IsToday ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = cell.IsCurrentMonth ? 1.0 : 0.35
            };
            var dot = new TextBlock
            {
                Text = cell.HasEvents ? "•" : " ",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = cell.HasEvents ? 1.0 : 0.0
            };

            if (_theme is not null)
            {
                dayText.Foreground = ThemePainter.Brush(
                    cell.IsToday ? _theme.Accent : _theme.WidgetForeground);
                dayText.FontFamily = new FontFamily(_theme.FontFamily);
                dot.Foreground = ThemePainter.Brush(_theme.Accent);
                dayButton.Background = cell.IsToday
                    ? ThemePainter.Brush(_theme.Accent, 0.22)
                    : ThemePainter.Brush(_theme.WidgetBackground, 0.01);
                dayButton.BorderBrush = cell.IsToday
                    ? ThemePainter.Brush(_theme.Accent, 0.8)
                    : ThemePainter.Brush(_theme.WidgetForeground, 0.08);
                dayButton.BorderThickness = new Thickness(cell.IsToday ? 1 : 0);
            }

            panel.Children.Add(dayText);
            panel.Children.Add(dot);
            dayButton.Content = panel;
            Grid.SetRow(dayButton, row);
            Grid.SetColumn(dayButton, col);
            DayGrid.Children.Add(dayButton);
        }
    }

    private void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CalendarDayCell cell })
        {
            return;
        }

        if (cell.Events.Count == 0)
        {
            EventSummary.Text = cell.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " — no events";
            return;
        }

        EventSummary.Text = string.Join(
            " · ",
            cell.Events.Select(ev => ev.Title).Take(3));
    }

    private void UpdateEventSummary(DateOnly today)
    {
        var monthEvents = _cells
            .Where(c => c.IsCurrentMonth && c.HasEvents)
            .SelectMany(c => c.Events)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (monthEvents.Count == 0)
        {
            EventSummary.Text = today.Month == _viewMonth && today.Year == _viewYear
                ? "No local events this month"
                : "No local events";
            return;
        }

        EventSummary.Text = string.Join(
            " · ",
            monthEvents.Take(3).Select(e => $"{e.Date:MM/dd} {e.Title}"));
    }
}
