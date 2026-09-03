# Universal Integration Platform

Secret Base v1.1 is a **Personal AI OS** that connects to the apps and APIs you already have — without rewriting Core for each one.

```
Your app / Windows app / Local API / Web API
        ↓  Integration Manifest
Secret Base Integration Host
        ↓  Capability → Permission → Safety → Action
Context / Activity / Search / Automation / Base AI
```

**Connect, don't couple.** Core never takes a dependency on HTTP SDKs, Google, GitHub, or Win32. Connectors talk through contracts. One broken integration cannot take Secret Base down.

## What you get

| Surface | Behavior |
|---------|----------|
| **My Integrations** | Name, health, transport, capabilities, last used, enabled |
| **Permissions** | Per-integration Read / Write / Execute / External / Destructive / Background Event |
| **Base AI** | Lists named capabilities only. Never invents URLs or HTTP methods |
| **Command Center** | “Tetris AI の状態”, “open my app” go through the same Safety gate |
| **Search** | Integration metadata + recent events. No crawling |
| **Privacy** | Observed / Shared / Remembered / Permissions |
| **Automation** | External events can suggest — never silent destructive actions |

## Connect your own app

1. Build your app (any language) with a small HTTP API or events.
2. Copy [`examples/integrations/secretbase.integration.json`](../../examples/integrations/secretbase.integration.json) and rename the `id`.
3. Declare **capabilities**, **endpoints**, **events**, and **authentication**.
4. In Secret Base: **My Integrations** — paste your JSON (Register pasted) or leave the box empty to enable the example apps.
5. Grant permissions. Reads can run; writes and `app.open` confirm.
6. Ask Base AI: "Show my app state."

Details: [manifest](manifest.md) · [capabilities](capabilities.md) · [security](security.md) · [authentication](authentication.md) · [events](events.md) · [example](example.md)

## Safety (non-negotiable)

```
Connector → Capability → Permission → Safety Gate → Confirm if required → Execute → Sanitized result
```

Forbidden: arbitrary URLs, arbitrary HTTP, shell, unrestricted filesystem, silent external writes, silent deletion, learning that turns Confirm into Safe Auto, secrets in Memory / Activity / logs / LLM prompts.

External strings are **untrusted data**, never system instructions.
