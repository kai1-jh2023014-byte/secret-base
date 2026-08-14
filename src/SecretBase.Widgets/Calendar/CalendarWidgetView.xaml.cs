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
/// Today agenda widget. Uses <see cref="CalendarService"/> (provider-agnostic).
/// Does not own Overlay / HWND logic — hosted inside WidgetFrame only.
/// </summary>
public sealed partial class CalendarWidgetView : UserControl
{
    private CalendarWidgetConfiguration _configuration = CalendarWidgetConfiguration.CreateDefault();
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private CalendarService? _service;
    private Func<string, bool>? _openUrl;
    private Action<CalendarWidgetConfiguration>? _onConfigurationChanged;
    private ThemeDefinition? _theme;
    private int _refreshGate;

    public CalendarWidgetView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = RefreshAgendaAsync();
    }

    public void Initialize(
        CalendarWidgetConfiguration configuration,
        CalendarService service,
        ITimeProvider? timeProvider = null,
        Func<string, bool>? openUrl = null,
        Action<CalendarWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _service = service;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _openUrl = openUrl;
        _onConfigurationChanged = onConfigurationChanged;
        UpdateProviderLabel();
        _ = RefreshAgendaAsync();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        HeaderText.FontFamily = font;
        HeaderText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        ProviderLabel.FontFamily = font;
        ProviderLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);

        StyleActionButton(RefreshButton, theme);
        StyleActionButton(OpenCalendarButton, theme);
        RestyleAgendaItems();
    }

    private static void StyleActionButton(Button button, ThemeDefinition theme)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Accent, 0.55);
        button.BorderThickness = new Thickness(1);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = RefreshAgendaAsync();

    private void OpenCalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var url = _configuration.OpenCalendarUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = CalendarWidgetConfiguration.DefaultOpenCalendarUrl;
        }

        var google = _service?.Providers.FirstOrDefault(p => p.ProviderId == CalendarProviderIds.Google);
        if (google?.OpenUrl is { Length: > 0 } providerUrl)
        {
            url = providerUrl;
        }

        _ = _openUrl?.Invoke(url);
    }

    private async Task RefreshAgendaAsync()
    {
        if (_service is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _refreshGate, 1) == 1)
        {
            return;
        }

        try
        {
            HeaderText.Text = "Today";
            AgendaList.Children.Clear();
            AgendaList.Children.Add(CreateMutedLine("Loading…"));

            var now = _timeProvider.GetLocalNow();
            var events = await _service.GetTodayAgendaAsync(now);
            AgendaList.Children.Clear();

            if (events.Count == 0)
            {
                AgendaList.Children.Add(CreateMutedLine("No events today"));
            }
            else
            {
                foreach (var ev in events)
                {
                    AgendaList.Children.Add(CreateEventBlock(ev));
                }
            }

            UpdateProviderLabel(events);
        }
        catch (Exception ex)
        {
            AgendaList.Children.Clear();
            AgendaList.Children.Add(CreateMutedLine($"Could not load agenda: {ex.Message}"));
        }
        finally
        {
            Interlocked.Exchange(ref _refreshGate, 0);
        }
    }

    private void UpdateProviderLabel(IReadOnlyList<CalendarEvent>? events = null)
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
            var fromEvents = events.Select(e => e.CalendarName ?? e.Provider)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fromEvents.Count > 0)
            {
                names = fromEvents!;
            }
        }

        ProviderLabel.Text = names.Count == 0 ? "Local" : string.Join(" · ", names);
    }

    private UIElement CreateEventBlock(CalendarEvent ev)
    {
        var timeLabel = FormatTimeRange(ev);
        var panel = new StackPanel { Spacing = 2 };
        var time = new TextBlock
        {
            Text = timeLabel,
            FontSize = 12,
            Opacity = 0.85
        };
        var title = new TextBlock
        {
            Text = ev.Title,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        if (_theme is not null)
        {
            time.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            title.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
            time.FontFamily = new FontFamily(_theme.FontFamily);
            title.FontFamily = new FontFamily(_theme.FontFamily);
        }

        panel.Children.Add(time);
        panel.Children.Add(title);
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

            panel.Children.Add(loc);
        }

        return panel;
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
        // Rebuild so theme brushes apply after ApplyTheme.
        _ = RefreshAgendaAsync();
    }

    private static string FormatTimeRange(CalendarEvent ev)
    {
        if (ev.IsAllDay)
        {
            return "All day";
        }

        var start = ev.Start.DateTime;
        var end = ev.End.DateTime;
        return string.Create(CultureInfo.InvariantCulture, $"{start:HH:mm}  〜 {end:HH:mm}");
    }
}
