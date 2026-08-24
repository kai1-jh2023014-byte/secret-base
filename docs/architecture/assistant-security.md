# Assistant Security

Security spine for Secret Base AI. Prefer predictability over autonomy.

## Boundary chain

```
User
 ↓
Assistant Widget
 ↓
LLM (IAiProvider)
 ↓
Tool Registry (no HostAction)
 ↓
AssistantToolExecutor
 ↓
Existing Command services
 ↓
Host (Platform launch APIs only)
```

## Must never exist

| Forbidden path | Status |
|----------------|--------|
| LLM → `Process.Start` | Absent |
| LLM → `File.Delete` / arbitrary write | Absent |
| LLM → PowerShell / cmd | Absent |
| LLM → arbitrary URL launch without Command | Absent |
| LLM → Host Bridge | Absent |
| WebView2 ↔ AI direct bridge | Absent |
| API key in Git / layout / logs / history | Forbidden |

## Secrets

- Store: `ISecureSecretStore` / Windows Credential Manager only
- Key id: `Assistant/OpenAI/ApiKey`
- Settings JSON: provider/model/maxSteps only
- Chat: reject/strip `sk-`, Bearer, passwords

## Confirmation

RequiresConfirmation tools never auto-run when `RequireConfirmationForActions` is true (default). Host launches only after **Run**.

## Provider honesty

Unimplemented providers (Gemini, Local) return **AI provider is unavailable.** — never pretend success.

## Music / integrations honesty

Demo catalog and stub integrations must be labeled. Do not invent Spotify or Classroom OAuth.

## Audit checklist (v0.5)

- [x] Tools only from `BuiltinAssistantToolRegistry`
- [x] HostAction not registered
- [x] Executor rejects HostAction
- [x] Argument validator blocks path/scheme/command strings
- [x] Context formatter omits absolute paths and secrets
- [x] Context is labeled as untrusted data (calendar/project/app/music metadata)
- [x] Max steps / history caps
- [x] Action results reflect success/failure honestly

## Related

- [security-boundaries.md](security-boundaries.md)
- [ai-assistant.md](ai-assistant.md)
