# Secret Base Design Language

Secret Base should feel like a **personal space that already exists on the computer**, not a dashboard of widgets and not a chatbot chrome.

## Concept

```text
Quiet · Personal · Intelligent · Warm · Premium · Adaptive · Slightly mysterious
```

Quietly capable. Not futuristic for its own sake.

## Hierarchy

Every surface answers, in this order:

1. **Now** — time, current event, focus
2. **Next** — upcoming event, next task, continue?
3. **Context** — project, last session, Base AI status
4. **Action** — Continue, Capture, Command Center — still confirmed when it launches anything

Clock (Base style) + Base card + Command Center are one space. Independent widget chrome is secondary.

## Tokens

| Axis | Token examples |
|------|----------------|
| Color | Background, BackgroundSecondary, WidgetBackground, SurfaceElevated, Foreground, ForegroundMuted, Accent, OnAccent, Border, FocusRing, StatusSuccess / Warning / Error |
| Type | FontFamily, TitleSize, BodySize, CaptionSize |
| Space | Spacing, WidgetMinWidth / Height, Density |
| Shape | CornerRadius, Shape (Soft / Balanced / Sharp) |
| Motion | MotionDurationMs, ReducedMotion |
| Depth | Transparency, ShadowOpacity, Border — no live compositor blur |

Default language: **Atelier** — deep slate, warm ivory, restrained jade, radius 18.

## Motion

Fast, subtle, predictable, purposeful. Reduced Motion sets duration to 0 and skips fade/pulse.

## Accessibility

High Contrast is a full visual system (opaque surfaces, 7:1 text, sharp corners, reduced motion). Accents never retint warning/error. Touch targets stay at least 32px. Command Center is keyboard-first (↑↓ Enter Esc).

## What we refuse

Neon, cyberpunk, always-on animation, giant gradients, stacked glass, game HUD, “AI glow.” Completeness over spectacle.
