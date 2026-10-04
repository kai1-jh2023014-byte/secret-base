# Jev decision layer

Jev decides. Gemini, OpenAI, and Ollama talk. Secret Base executes, and only through the existing tool and safety gate.

Jev is not an `AssistantProviderIds` value. The conversation provider list stays Local, Gemini, and OpenAI.

## Call

Documented request shape (`model`, `state`, `questions` of type `choice`, `score`, or `noul`):

| Key prefix | Endpoint |
|------------|----------|
| `jv_live_` | `POST https://jevtypesafeai.com/api/v1/decide` |
| anything else | `POST https://api.typesafe.ai/v1/systemone` |

Auth is `Authorization: Bearer`. The model field sent by Secret Base is `jev-latest`.

Secret Base asks three `choice` questions in one call:

| Question | Allowed choices |
|----------|-----------------|
| `situation` | `study`, `coding`, `creative`, `music`, `communication`, `entertainment`, `break`, `unknown` |
| `next_step` | `none`, `suggest`, `prepare_workspace`, `continue_workspace`, `start_focus`, `ask_confirmation` |
| `gate` | `allow`, `confirm`, `deny` |

A connection test sends one documented `noul` question named `ready` and the state `Secret Base connection check.`

## State

The `state` string may include local time, the local intent label, today's event count, a todo count, a workspace label, and the current message trimmed to 240 characters. Secrets and paths are removed. Chat history is not sent.

## Safety

```
Jev answers
  → parse (unknown choice = invalid)
  → JevSafetyGate
  → AutomationSafety / confirmation policy
  → existing tool
```

- `deny`, or a choice outside the table, refuses mutating tools.
- `confirm`, a suggestion-only next step, or confidence below 0.5 asks before a Safe Auto tool.
- `allow` still cannot skip `AutomationSafety`. `files_delete` and other confirmation tools stay behind Run.
- Jev has no process, shell, or file API.

When the key is missing or the call fails, a local rule is used: questions do nothing, suggestions stay suggestions, action requests ask for confirmation. That fallback does not change the existing tool policy and does not auto-run an action just because Jev is down.

## Key

`ISecureSecretStore` entry `Jev/ApiKey`. It is not written to `Assistant/OpenAI/ApiKey`, `Assistant/Gemini/ApiKey`, or `assistant.json`. The settings dialog shows a masked box and a saved/not-set line. The raw key is not placed in the transcript.
