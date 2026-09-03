# Example: connect a homemade app

This is the Tetris AI / Local Test path used in tests.

## 1. App API

Loopback only:

```
GET http://127.0.0.1:3848/state
GET http://127.0.0.1:3848/stats
```

POST an event (from your app, not from the LLM):

```json
{
  "summary": "top out"
}
```

Sign webhooks with HMAC-SHA256 of `{unixSeconds}.{nonce}.{body}` using the webhook secret in Credential Manager.

## 2. Manifest

See [`examples/integrations/secretbase.integration.json`](../../examples/integrations/secretbase.integration.json).

## 3. Register

Secret Base → **My Integrations** → Enable example apps, or import the JSON. Grant Read (and Execute if you want `app.open`).

Link `app.open` by registering the same app in **My Apps** and setting `linkedAppId`.

## 4. Use

- Command palette / Base AI: “Tetris AI の状態”
- “Tetris AI を起動して” → confirmation → My Apps launch
- `game.finished` → Activity → optional quiet suggestion

## 5. Calendar

`secretbase.calendar` is a **known** adapter over existing Calendar commands. It does not embed a Google SDK in Core.

## Generic REST

`demo.generic-rest` shows HTTPS + API key + JSON Schema + DELETE as destructive. Point `baseUrl` at a host you own; Secret Base will still refuse undeclared paths.
