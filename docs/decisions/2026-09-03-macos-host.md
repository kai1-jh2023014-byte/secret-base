# 2026-09-03 — macOS workspace host (second head)

**Decision:** Keep the WinUI 3 Windows overlay. Add `SecretBase.Platform.Mac` + `SecretBase.App.Mac` (Avalonia) that share Core / Infrastructure / Abstractions. Do not rewrite Windows UI to Avalonia. Do not replace Finder, Dock, or the menu bar.

**Why:** WinUI / WASDK cannot run on macOS. Core was already `net10.0`. Users asked for Mac support; an honest workspace window is shippable. A fake wallpaper overlay would over-claim capabilities we do not have on AppKit.

**v1 surface:** Clock, Secret Base AI, LaunchAgent auto-start, Keychain secrets, `/usr/bin/open` launch, Safe Exit.

See [macos.md](../architecture/macos.md).
