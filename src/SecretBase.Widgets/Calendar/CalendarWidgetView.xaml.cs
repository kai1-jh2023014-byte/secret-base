using System.Globalization;
using Microsoft.UI;
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
/// Today agenda widget. Uses <see cref="CalendarService"/> (provider-agnostic).
/// Does not own Overlay / HWND logic — hosted inside WidgetFrame only.
/// </summary>
public sealed partial class CalendarWidgetView : UserControl
{
    private CalendarWidgetConfiguration _configuration = CalendarWidgetConfiguration.CreateDefault();
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private CalendarService? _service;
    private ICalendarAgendaCache? _cache;
    private Func<string, bool>? _openUrl;
    private Action<CalendarWidgetConfiguration>? _onConfigurationChanged;
    private IIntegrationMemory? _integrations;
    private ThemeDefinition? _theme;
    private int _refreshGate;
    private int _connectGate;

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
        Action<CalendarWidgetConfiguration>? onConfigurationChanged = null,
        ICalendarAgendaCache? cache = null,
        IIntegrationMemory? integrations = null)
    {
        _configuration = configuration;
        _service = service;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _openUrl = openUrl;
        _onConfigurationChanged = onConfigurationChanged;
        _cache = cache;
        _integrations = integrations;
        UpdateProviderLabel();
        UpdateConnectVisibility();
        _ = RefreshAgendaAsync();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, null, theme);
        HeaderAccent.Background = ThemePainter.Brush(theme.Accent, 0.9);
        WidgetSurfaceStyle.ApplyMuted(ProviderLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyGhostButton(RefreshButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(OpenCalendarButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(AddEventButton, theme);
        WidgetSurfaceStyle.ApplyActionButton(ConnectButton, theme, accent: true);
        RestyleAgendaItems();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = RefreshAgendaAsync();

    private void AddEventButton_Click(object sender, RoutedEventArgs e)
    {
        var local = _service?.Providers.OfType<LocalCalendarProvider>().FirstOrDefault();
        if (local is null)
        {
            StatusLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = "Local calendar is unavailable.";
            return;
        }

        var title = AddTitleBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = "Enter an event title.";
            return;
        }

        var now = _timeProvider.GetLocalNow();
        var day = DateOnly.FromDateTime(now.DateTime);
        var timeText = AddTimeBox.Text?.Trim() ?? string.Empty;
        DateTimeOffset start;
        if (string.IsNullOrWhiteSpace(timeText))
        {
            start = new DateTimeOffset(day.ToDateTime(new TimeOnly(now.Hour, 0)), now.Offset);
        }
        else if (TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, out var parsed)
                 || TimeOnly.TryParse(timeText, CultureInfo.CurrentCulture, out parsed))
        {
            start = new DateTimeOffset(day.ToDateTime(parsed), now.Offset);
        }
        else
        {
            StatusLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = "Use time like 14:00.";
            return;
        }

        local.AddEvent(title, start, start.AddHours(1));
        AddTitleBox.Text = string.Empty;
        AddTimeBox.Text = string.Empty;
        StatusLabel.Visibility = Visibility.Visible;
        StatusLabel.Text = "Added to Secret Base (local). Not pushed to Google.";
        _ = RefreshAgendaAsync();
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

            StatusLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = $"Connecting {authProvider.DisplayName}…";
            await authProvider.AuthenticateAsync();
            StatusLabel.Text = $"{authProvider.DisplayName} connected.";
            _integrations?.RememberConnected(
                IntegrationMemoryIds.GoogleCalendar,
                authProvider.DisplayName,
                inAppExperience: true);
            await RefreshAgendaAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = $"Connect failed: {ex.Message}";
        }
        finally
        {
            UpdateConnectVisibility();
            Interlocked.Exchange(ref _connectGate, 0);
        }
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
            CalendarAgendaSnapshot snapshot;
            try
            {
                snapshot = await _service.GetTodaySnapshotAsync(now);
            }
            catch (Exception ex)
            {
                // Should be rare — CalendarService already isolates providers.
                AgendaList.Children.Clear();
                AgendaList.Children.Add(CreateMutedLine($"Could not load agenda: {ex.Message}"));
                StatusLabel.Visibility = Visibility.Visible;
                StatusLabel.Text = "Calendar unavailable";
                return;
            }

            var events = snapshot.Events;
            var anySuccess = snapshot.ProviderResults.Any(r => r.Succeeded);
            var failures = snapshot.ProviderResults.Where(r => !r.Succeeded).ToList();

            if (anySuccess)
            {
                try
                {
                    _cache?.Save(events, DateTimeOffset.UtcNow);
                }
                catch
                {
                    // Cache is best-effort.
                }
            }
            else if (events.Count == 0 && _cache is not null)
            {
                var cached = _cache.TryLoad(out var savedAt);
                if (cached is { Count: > 0 })
                {
                    events = cached;
                    StatusLabel.Visibility = Visibility.Visible;
                    StatusLabel.Text = savedAt is null
                        ? "Showing cached agenda (providers unavailable)."
                        : $"Showing cached agenda ({savedAt:u}).";
                }
            }

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

            if (failures.Count > 0)
            {
                StatusLabel.Visibility = Visibility.Visible;
                var parts = failures.Select(f =>
                    string.IsNullOrWhiteSpace(f.ErrorMessage)
                        ? $"{f.DisplayName} unavailable"
                        : $"{f.DisplayName} unavailable");
                var failureText = string.Join(" · ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
                if (StatusLabel.Text.StartsWith("Showing cached", StringComparison.Ordinal))
                {
                    StatusLabel.Text = $"{StatusLabel.Text} {failureText}";
                }
                else
                {
                    StatusLabel.Text = failureText;
                }
            }
            else if (!StatusLabel.Text.StartsWith("Showing cached", StringComparison.Ordinal)
                     && !StatusLabel.Text.StartsWith("Connecting", StringComparison.Ordinal)
                     && !StatusLabel.Text.Contains("connected", StringComparison.OrdinalIgnoreCase)
                     && !StatusLabel.Text.StartsWith("Connect failed", StringComparison.Ordinal))
            {
                StatusLabel.Visibility = Visibility.Collapsed;
                StatusLabel.Text = string.Empty;
            }

            UpdateProviderLabel(events, snapshot);
            UpdateConnectVisibility();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshGate, 0);
        }
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

        // Show Connect only when OAuth client is present but disconnected/error.
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

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 0, 0),
            Fill = new SolidColorBrush(ParseColor(ev.Color) ?? (_theme is not null
                ? ThemePainter.ParseColor(_theme.Accent)
                : Colors.CornflowerBlue))
        };
        Grid.SetColumn(dot, 0);
        Grid.SetColumn(textPanel, 1);
        row.Children.Add(dot);
        row.Children.Add(textPanel);
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
}
