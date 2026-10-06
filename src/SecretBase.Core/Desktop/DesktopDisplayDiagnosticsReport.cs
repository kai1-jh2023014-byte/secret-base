using System.Text;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Point-in-time display / viewport / layout diagnostics for remote-desktop
/// and DPI investigations. Pure data — no OS APIs.
/// </summary>
public sealed class DesktopDisplayDiagnosticsReport
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string Reason { get; init; } = string.Empty;
    public int Sequence { get; init; }

    public string DisplayId { get; init; } = string.Empty;
    public string DisplayBounds { get; init; } = string.Empty;
    public string DisplayWorkArea { get; init; } = string.Empty;
    public int WorkAreaWidthPx { get; init; }
    public int WorkAreaHeightPx { get; init; }
    public int BoundsWidthPx { get; init; }
    public int BoundsHeightPx { get; init; }

    public string WindowBounds { get; init; } = string.Empty;
    public int WindowWidthPx { get; init; }
    public int WindowHeightPx { get; init; }

    public double PageActualWidthDip { get; init; }
    public double PageActualHeightDip { get; init; }
    public double RootActualWidthDip { get; init; }
    public double RootActualHeightDip { get; init; }
    public double CanvasActualWidthDip { get; init; }
    public double CanvasActualHeightDip { get; init; }

    public double RasterizationScale { get; init; }
    public double PhysicalWidthFromDip { get; init; }
    public double PhysicalHeightFromDip { get; init; }

    public double ReferenceWidth { get; init; } = DesktopLayoutReference.Width;
    public double ReferenceHeight { get; init; } = DesktopLayoutReference.Height;
    public double? AuthoredViewportWidth { get; init; }
    public double? AuthoredViewportHeight { get; init; }
    public double ResolverInputWidth { get; init; }
    public double ResolverInputHeight { get; init; }
    public double ResolverOutputWidth { get; init; }
    public double ResolverOutputHeight { get; init; }
    public bool ResolverIdentity { get; init; }
    public string SafeArea { get; init; } = string.Empty;

    public bool SavedGeometryMutated { get; init; }
    public bool PersistInvoked { get; init; }

    public List<DesktopObjectDiagnosticsRow> Objects { get; init; } = [];

    public string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DISPLAY CHANGE ===");
        sb.AppendLine($"Time: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Seq: {Sequence}");
        sb.AppendLine($"Reason: {Reason}");
        sb.AppendLine();
        sb.AppendLine($"DisplayId: {DisplayId}");
        sb.AppendLine($"Display Bounds: {DisplayBounds}");
        sb.AppendLine($"Display WorkArea: {DisplayWorkArea}");
        sb.AppendLine($"WorkArea px: {WorkAreaWidthPx} × {WorkAreaHeightPx}");
        sb.AppendLine($"Bounds px: {BoundsWidthPx} × {BoundsHeightPx}");
        sb.AppendLine();
        sb.AppendLine($"Window Bounds: {WindowBounds}");
        sb.AppendLine($"Window px: {WindowWidthPx} × {WindowHeightPx}");
        sb.AppendLine();
        sb.AppendLine($"DesktopPage Actual: {PageActualWidthDip:0.##} × {PageActualHeightDip:0.##} DIP");
        sb.AppendLine($"RootGrid Actual: {RootActualWidthDip:0.##} × {RootActualHeightDip:0.##} DIP");
        sb.AppendLine($"WidgetCanvas Actual: {CanvasActualWidthDip:0.##} × {CanvasActualHeightDip:0.##} DIP");
        sb.AppendLine();
        sb.AppendLine($"RasterizationScale: {RasterizationScale:0.####}");
        sb.AppendLine($"PhysicalFromDip: {PhysicalWidthFromDip:0.##} × {PhysicalHeightFromDip:0.##}");
        sb.AppendLine();
        sb.AppendLine($"Reference: {ReferenceWidth:0} × {ReferenceHeight:0}");
        sb.AppendLine($"AuthoredViewport: {FormatNullableSize(AuthoredViewportWidth, AuthoredViewportHeight)}");
        sb.AppendLine($"Resolver input (authored): {ResolverInputWidth:0.##} × {ResolverInputHeight:0.##}");
        sb.AppendLine($"Resolver output (current): {ResolverOutputWidth:0.##} × {ResolverOutputHeight:0.##}");
        sb.AppendLine($"Resolver identity: {ResolverIdentity}");
        sb.AppendLine($"SafeArea: {SafeArea}");
        sb.AppendLine($"SavedGeometryMutated: {SavedGeometryMutated}");
        sb.AppendLine($"PersistInvoked: {PersistInvoked}");
        sb.AppendLine();
        sb.AppendLine("Objects (Saved → Resolved / Canvas):");
        foreach (var row in Objects)
        {
            sb.AppendLine(
                $"  [{row.Kind}] {row.Label}: saved=({row.SavedX:0.#},{row.SavedY:0.#}) {row.SavedW:0.#}×{row.SavedH:0.#}"
                + $" → resolved=({row.ResolvedX:0.#},{row.ResolvedY:0.#}) {row.ResolvedW:0.#}×{row.ResolvedH:0.#}"
                + $" | canvas=({row.CanvasX:0.#},{row.CanvasY:0.#}) {row.CanvasW:0.#}×{row.CanvasH:0.#}");
        }

        sb.AppendLine("=======================");
        return sb.ToString();
    }

    public string FormatCompactPanel()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Seq #{Sequence} · {Timestamp:HH:mm:ss} · {Reason}");
        sb.AppendLine($"Display WorkArea: {WorkAreaWidthPx} × {WorkAreaHeightPx} px");
        sb.AppendLine($"Display Bounds: {BoundsWidthPx} × {BoundsHeightPx} px");
        sb.AppendLine($"Window: {WindowWidthPx} × {WindowHeightPx} px");
        sb.AppendLine($"Page Actual: {PageActualWidthDip:0.#} × {PageActualHeightDip:0.#} DIP");
        sb.AppendLine($"Canvas Actual: {CanvasActualWidthDip:0.#} × {CanvasActualHeightDip:0.#} DIP");
        sb.AppendLine($"Scale: {RasterizationScale:0.####}");
        sb.AppendLine($"PhysicalFromDip: {PhysicalWidthFromDip:0.#} × {PhysicalHeightFromDip:0.#}");
        sb.AppendLine($"Reference: {ReferenceWidth:0} × {ReferenceHeight:0}");
        sb.AppendLine($"Authored: {FormatNullableSize(AuthoredViewportWidth, AuthoredViewportHeight)}");
        sb.AppendLine($"Resolver: {ResolverInputWidth:0.#}×{ResolverInputHeight:0.#} → {ResolverOutputWidth:0.#}×{ResolverOutputHeight:0.#} (identity={ResolverIdentity})");
        sb.AppendLine($"SafeArea: {SafeArea}");
        sb.AppendLine($"SavedMutated={SavedGeometryMutated} Persist={PersistInvoked}");
        sb.AppendLine();
        foreach (var row in Objects.Take(12))
        {
            sb.AppendLine($"{row.Label}: ({row.SavedX:0},{row.SavedY:0})→({row.ResolvedX:0},{row.ResolvedY:0})");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatNullableSize(double? width, double? height) =>
        width is double w && height is double h ? $"{w:0.##} × {h:0.##}" : "(null)";
}

public sealed class DesktopObjectDiagnosticsRow
{
    public string Kind { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public Guid Id { get; init; }

    public double SavedX { get; init; }
    public double SavedY { get; init; }
    public double SavedW { get; init; }
    public double SavedH { get; init; }

    public double ResolvedX { get; init; }
    public double ResolvedY { get; init; }
    public double ResolvedW { get; init; }
    public double ResolvedH { get; init; }

    public double CanvasX { get; init; }
    public double CanvasY { get; init; }
    public double CanvasW { get; init; }
    public double CanvasH { get; init; }
}
