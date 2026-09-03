using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SecretBase.Core.Observation;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Privacy-first foreground + idle observer using documented user32 APIs.
/// Not a keylogger: no keystrokes, clipboard, or document contents.
/// </summary>
public sealed class WindowsForegroundObservationService : IComputerObservationService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2.5);
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(3);

    private readonly object _gate = new();
    private readonly Func<IReadOnlyList<string>> _projectNames;
    private Timer? _timer;
    private bool _started;
    private bool _idle;
    private bool _startupEmitted;
    private string? _lastApp;
    private string? _lastTitle;
    private DateTimeOffset _idleSince = DateTimeOffset.MinValue;

    public WindowsForegroundObservationService(Func<IReadOnlyList<string>>? projectNames = null)
    {
        _projectNames = projectNames ?? (() => []);
    }

    public bool IsSupported => true;

    public string CapabilityNote =>
        "Foreground process name and idle state only. Browser titles are dropped unless they name a registered project.";

    public event EventHandler<ObservationEvent>? Observed;

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, PollInterval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _started = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose() => Stop();

    private void Tick()
    {
        try
        {
            var now = DateTimeOffset.Now;
            if (!_startupEmitted)
            {
                _startupEmitted = true;
                Raise(new ObservationEvent { Kind = ObservationKind.SystemStartup, At = now });
            }

            var idle = ReadIdle(now);
            if (idle && !_idle)
            {
                _idle = true;
                _idleSince = now;
                Raise(new ObservationEvent { Kind = ObservationKind.IdleStarted, At = now });
                return;
            }

            if (!idle && _idle)
            {
                _idle = false;
                var kind = now - _idleSince >= TimeSpan.FromHours(2)
                    ? ObservationKind.SystemResume
                    : ObservationKind.IdleEnded;
                Raise(new ObservationEvent { Kind = kind, At = now });
            }

            if (_idle)
            {
                return;
            }

            if (!TryReadForeground(out var app, out var title))
            {
                return;
            }

            var projects = _projectNames() ?? [];
            var safeApp = ObservationSanitizer.SafeApplicationName(app);
            var safeTitle = ObservationSanitizer.SafeWindowTitle(title, safeApp, projects);
            if (string.Equals(_lastApp, safeApp, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_lastTitle, safeTitle, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var kindChanged = !string.Equals(_lastApp, safeApp, StringComparison.OrdinalIgnoreCase)
                ? ObservationKind.ApplicationActivated
                : ObservationKind.WindowTitleChanged;
            _lastApp = safeApp;
            _lastTitle = safeTitle;
            Raise(new ObservationEvent
            {
                Kind = kindChanged,
                At = now,
                ApplicationName = safeApp,
                WindowTitle = safeTitle
            });
        }
        catch (Exception)
        {
            // Observation must never crash the overlay.
        }
    }

    private void Raise(ObservationEvent observation) => Observed?.Invoke(this, observation);

    private static bool ReadIdle(DateTimeOffset _)
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return false;
        }

        var idleMs = unchecked((uint)Environment.TickCount) - info.LastInput;
        return TimeSpan.FromMilliseconds(idleMs) >= IdleAfter;
    }

    private static bool TryReadForeground(out string? processName, out string? title)
    {
        processName = null;
        title = null;
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var builder = new StringBuilder(256);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        title = builder.ToString();
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return !string.IsNullOrWhiteSpace(title);
        }

        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (Exception)
        {
            processName = null;
        }

        return !string.IsNullOrWhiteSpace(processName) || !string.IsNullOrWhiteSpace(title);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint LastInput;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
