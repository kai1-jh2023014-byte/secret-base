# Integration Manifest

File name: `secretbase.integration.json`

Required fields:

| Field | Meaning |
|-------|---------|
| `id` | Stable lowercase id (`my-tetris-ai`) |
| `name` | Quiet display name |
| `version` | Your app version |
| `description` | One or two sentences |
| `transport` | `local` · `http` · `https` · `webhook` · `deepLink` |
| `capabilities` | What Secret Base may call |
| `events` | What Secret Base may ingest |
| `authentication` | `none` · `apiKey` · `oauth2` · `oauth2Pkce` |
| `endpoints` | Declared HTTP routes only |
| `permissions` | Requested permission names |

Optional: `baseUrl`, `allowedHosts`, `allowLoopback`, `deepLink`, `linkedAppId`.

`allowPrivateNetwork` is **always rejected**. Name an exact host instead.

## Transports

| Kind | Now | Later |
|------|-----|--------|
| `local` / `http` / `https` | Generic REST connector (declared endpoints) | |
| `webhook` | Signed ingest into Activity | Overlay does not bind a public port |
| `deepLink` | Validate scheme/host/path; launch goes through **My Apps** allowlist | |
| `webSocket` / `gRPC` / `mcp` | Declared as extension points | Not executable in v1.1 |

HTTPS is required except explicit loopback (`127.0.0.1` / `localhost`) with `allowLoopback` or `local` transport.

## Endpoints

Each endpoint has `id`, `method` (`GET|POST|PUT|PATCH|DELETE`), `path`, optional JSON Schema, timeout (max 15s), and response size (max 256 KiB).

Paths may include `{id}` tokens. Tokens must be `[A-Za-z0-9_-]{1,64}`. No `..`, query strings, or caller-supplied URLs.

## Discovery

v1.1 is **explicit registration** (paste/import a manifest, or enable the known Calendar adapter and demo apps). Secret Base does not scan the LAN or the internet for devices.
