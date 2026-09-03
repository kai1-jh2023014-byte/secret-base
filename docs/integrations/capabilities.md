# Capabilities

A capability is a **named** thing Secret Base may do. It is not a free-form URL.

Examples: `state.read`, `stats.read`, `project.read`, `app.open`, `data.create`, `data.delete`, `event.receive`.

Each capability has a **risk**:

| Risk | Permission | Action lane |
|------|------------|-------------|
| `read` | Read | READ |
| `write` | Write | CONFIRM |
| `execute` | Execute | CONFIRM (includes `app.open`) |
| `destructive` | Destructive + Write | DESTRUCTIVE |
| `external` | External Communication | CONFIRM |

GET endpoints stay READ. Non-GET is at least CONFIRM. DELETE must be declared destructive or it is **FORBIDDEN**.

`shell.run`, `process.start`, and anything with `arbitrary` in the id are forbidden at parse time.

Learning **never** promotes CONFIRM → SAFE_AUTO (`IntegrationSafety.MayLearnAutoExecute = false`, same as `LearningPolicy.MayEscalatePrivilege`).

## Base AI

Tools:

- `integrations_list` — catalog only
- `integration_query` — read capability (`integration_id` + `capability`)
- `integration_invoke` — write/execute after confirmation

If the model sends `url`, `method`, `path`, or `host`, the host refuses.
