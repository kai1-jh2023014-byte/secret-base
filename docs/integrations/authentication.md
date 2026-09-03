# Authentication

| Kind | v1.1 |
|------|------|
| `none` | Allowed for loopback demos |
| `apiKey` | Header from `ISecureSecretStore` via `credentialReference` |
| `oauth2` / `oauth2Pkce` | Contract + reference only. No silent OAuth. User completes auth in the existing integration UI / browser |

Core never sees the secret value. Assistants never receive it.

To attach a key (host / future settings UI):

1. Store the secret under `secretref:integration:{id}:apikey`.
2. Set the registration `credentialReference` to that handle.
3. Manifest `authentication.headerName` defaults to `Authorization` (Bearer prefix added when the stored value has no space).

OAuth refresh tokens stay in the same OS store used by Calendar / Music. Import/export of integrations **omits** credentials; you re-auth after import.
