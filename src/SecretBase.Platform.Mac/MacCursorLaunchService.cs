using System.Diagnostics;
using SecretBase.Core.Ai;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Resolves Cursor.app (Applications or PATH) and launches with at most one folder argument.
/// Uses <c>/usr/bin/open -a</c> for .app bundles; never PowerShell, never free-form argv.
/// </summary>
public sealed class MacCursorLaunchService : ICursorLaunchService
{
    private readonly object _gate = new();
    private readonly string? _pathEnvironment;
    private readonly string? _homeDirectory;
    private readonly string? _applicationsDirectory;
    private readonly string? _localAppData;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, IReadOnlyList<string>, CursorLaunchResult> _start;
    private string? _resolved;
    private bool _resolvedChecked;

    public MacCursorLaunchService(
        string? pathEnvironment = null,
        string? homeDirectory = null,
        string? applicationsDirectory = null,
        string? localAppData = null,
        Func<string, bool>? fileExists = null,
        Func<string, IReadOnlyList<string>, CursorLaunchResult>? start = null)
    {
        _pathEnvironment = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH");
        _homeDirectory = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _applicationsDirectory = applicationsDirectory ?? "/Applications";
        _localAppData = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _fileExists = fileExists ?? File.Exists;
        _start = start ?? DefaultStart;
    }

    public bool IsAvailable
    {
        get
        {
            EnsureResolved();
            return !string.IsNullOrWhiteSpace(_resolved);
        }
    }

    public string? ResolvedExecutablePath
    {
        get
        {
            EnsureResolved();
            return _resolved;
        }
    }

    public CursorLaunchResult TryOpenApp()
    {
        EnsureResolved();
        if (string.IsNullOrWhiteSpace(_resolved))
        {
            return new CursorLaunchResult(false, "Cursor is not available.", OfferWebsiteFallback: true);
        }

        return Launch(_resolved!, folder: null);
    }

    public CursorLaunchResult TryOpenFolder(string absoluteFolderPath)
    {
        if (!AiCursorFolderValidator.TryNormalizeProjectRoot(absoluteFolderPath, out var folder, out var error))
        {
            return new CursorLaunchResult(false, error);
        }

        if (!Directory.Exists(folder))
        {
            return new CursorLaunchResult(false, "Project root not found.");
        }

        EnsureResolved();
        if (string.IsNullOrWhiteSpace(_resolved))
        {
            return new CursorLaunchResult(false, "Cursor is not available.", OfferWebsiteFallback: true);
        }

        return Launch(_resolved!, folder);
    }

    private CursorLaunchResult Launch(string resolved, string? folder)
    {
        var isBundle = resolved.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                       || resolved.EndsWith(".app/", StringComparison.OrdinalIgnoreCase);
        if (isBundle)
        {
            var args = new List<string> { "-a", resolved.TrimEnd('/') };
            if (!string.IsNullOrWhiteSpace(folder))
            {
                args.Add("--args");
                args.Add(folder);
            }

            return _start(MacTargetLaunchService.OpenExecutable, args);
        }

        var binaryArgs = new List<string>();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            binaryArgs.Add(folder);
        }

        return _start(resolved, binaryArgs);
    }

    private void EnsureResolved()
    {
        lock (_gate)
        {
            if (_resolvedChecked)
            {
                return;
            }

            _resolved = CursorInstallLocator.TryResolve(
                _pathEnvironment,
                _localAppData,
                fileExists: _fileExists,
                homeDirectory: _homeDirectory,
                applicationsDirectory: _applicationsDirectory);
            _resolvedChecked = true;
        }
    }

    internal static CursorLaunchResult DefaultStart(string fileName, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsMacOS() && fileName == MacTargetLaunchService.OpenExecutable)
        {
            return new CursorLaunchResult(false, "macOS open is not available on this OS.", OfferWebsiteFallback: true);
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                ErrorDialog = false
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            _ = Process.Start(psi);
            return new CursorLaunchResult(true, null);
        }
        catch (Exception ex)
        {
            return new CursorLaunchResult(false, ex.Message, OfferWebsiteFallback: true);
        }
    }
}
