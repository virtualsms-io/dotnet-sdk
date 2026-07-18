# Changelog

## 2.0.0

Breaking change -- full rewrite as a native **REST v1** client.

- Base URL changed from `https://virtualsms.io/stubs/handler_api.php` (legacy, sms-activate-
  compatible dispatcher) to `https://virtualsms.io/api/v1` (native REST). The v1.x client never
  called this endpoint; v2 never calls the legacy one.
- Constructor changed: `new VirtualSMSClient(apiKey, VirtualSMSClientOptions? options = null)`,
  API key required and first. `baseUrl`/`httpClient` moved into `options`. `X-API-Key` header
  auth replaces the `api_key` query-string param.
- Full method surface: activations/orders (14), rentals (9, Full Access + Platform tiers),
  proxies (10), account (4, including client-side usage stats), webhooks (7, new in 2.0.0),
  browser sessions (1, invite-only beta), and carrier lookup (1). v1.x covered 6 methods
  (balance/number/status/done/cancel/wait) against the legacy dispatcher only.
- Typed exception hierarchy (`BadApiKeyException`, `InsufficientBalanceException`,
  `NotFoundException`, `RateLimitedException`, `ServerErrorException`,
  `CooldownActiveException`) replaces the single `VirtualSMSException`/`NoNumbersException` pair.
- GET requests get a bounded automatic retry (3 attempts, exponential backoff) on network errors
  and 5xx. Mutating calls are never auto-retried.
- Not a drop-in upgrade from 1.x: method names, response shapes, and the constructor all changed.

## 1.0.0

Initial release. Thin client for the legacy `handler_api.php` (sms-activate-compatible)
dispatcher: `GetBalanceAsync`, `GetNumberAsync`, `GetStatusAsync`, `DoneAsync`, `CancelAsync`,
`WaitForCodeAsync`.
