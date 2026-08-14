using System.Diagnostics;
using SecretBase.Core.Ai;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Resolves Cursor via PATH and LocalAppData Programs (no hardcoded username paths),
/// then launches with at most one validated absolute folder argument.
/// </summary>
public sealed class WindowsCursorLaunchService : ICursorLaunchService
{
    private readonly object _gate = new();
    private string? _resolved;
    private bool _resolvedChecked;

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

        return Start(_resolved!, arguments: null);
    }

    public CursorLaunchResult TryOpenFolder(string absoluteFolderPath)
    {
        if (!AiCursorFolderValidator.TryNormalizeProjectRoot(absoluteFolderPath, out var folder, out var error))
        {
            return new CursorLaunchResult(false, error);
        }

        // Existence check — do not create folders.
        if (!Directory.Exists(folder))
        {
            return new CursorLaunchResult(false, "Project root not found.");
        }

        EnsureResolved();
        if (string.IsNullOrWhiteSpace(_resolved))
        {
            return new CursorLaunchResult(false, "Cursor is not available.", OfferWebsiteFallback: true);
        }

        if (!CursorInstallLocator.TryQuoteWindowsArgument(folder, out var quoted, out var quoteError))
        {
            return new CursorLaunchResult(false, quoteError);
        }

        return Start(_resolved!, arguments: quoted);
    }

    private CursorLaunchResult Start(string exe, string? arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                ErrorDialog = false
            };
            if (!string.IsNullOrEmpty(arguments))
            {
                psi.Arguments = arguments;
            }

            _ = Process.Start(psi);
            return new CursorLaunchResult(true, null);
        }
        catch (Exception ex)
        {
            return new CursorLaunchResult(false, ex.Message, OfferWebsiteFallback: true);
        }
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
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            _resolvedChecked = true;
        }
    }
}
