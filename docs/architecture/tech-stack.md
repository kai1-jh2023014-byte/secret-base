# Tech Stack Decision (2026-08)

## Selected versions

| Component | Version | Why |
|-----------|---------|-----|
| .NET | **10.0 (LTS)** SDK 10.0.302 | Active LTS until 2028-11-14. .NET 9 STS ends 2026-11; .NET 8 LTS also ends 2026-11. |
| Windows App SDK | **2.3.1** (stable) | Latest stable WASDK/WinUI 3 line as of 2026-07-16. Highest priority for UI/platform compatibility. |
| WinUI | 3 (via WASDK 2.3) | Official modern Windows UI framework |
| WebView2 | via WASDK dependency | Web Widget host (`WidgetTypes.Web`); no Electron |
| TFM (App) | `net10.0-windows10.0.26100.0` | Matches current Windows SDK projections used by WASDK tooling |
| Min OS | `10.0.17763.0` (declared) | WASDK support floor; development target is Windows 11 |
| IDE | Rider 2026.1 + `dotnet` CLI | Visual Studio not required; WinUI `dotnet new` templates used |

## Rejected / deferred

| Option | Reason |
|--------|--------|
| .NET 9 | STS ends Nov 2026 — poor long-term base for a new product |
| .NET 8 | LTS ending Nov 2026 alongside .NET 9 |
| Electron | Weak Windows integration vs product goals |
| Full Visual Studio install | Optional; CLI templates + Rider are enough for this milestone |

## Packaging choice

**Unpackaged + Windows App SDK self-contained** for v0.1.

Why:

1. Exit = process end → normal Desktop returns immediately.
2. Avoids leftover MSIX state while iterating.
3. Self-contained reduces "runtime not installed" friction during early development.

MSIX packaging can be reintroduced later without changing Core.
