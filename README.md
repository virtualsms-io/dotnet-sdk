# VirtualSMS .NET SDK

Native .NET client for the **VirtualSMS REST API v1**: SMS verification on real carrier SIM
cards (not VoIP), number rentals, and residential/mobile/datacenter proxies, across 2500+
services in 145+ countries.

Built for developers and AI agents: REST API, hosted MCP server, SDKs in 9 languages.

## What this SDK does

Covers the full customer-facing REST v1 surface: activations/orders (buy a number, poll or
wait for the code, cancel/swap/retry), rentals (Full Access and Platform tiers), proxies
(catalog, purchase, rotate, targeting, connection-string generation), account (balance,
profile, transactions, usage stats), webhooks (create/list/update/delete/test/deliveries),
carrier/line-type lookup, and manual-registration browser sessions (invite-only beta).

## Installation

```bash
dotnet add package VirtualSMS
```

## Quick Start

```csharp
using VirtualSMS;

// 1. Get your API key at https://virtualsms.io/dashboard (Settings -> API Keys)
using var client = new VirtualSMSClient("vsms_your_api_key");

// 2. Buy a number
var order = await client.CreateOrderAsync(service: "wa", country: "GB"); // WhatsApp, UK
Console.WriteLine($"Number: {order.PhoneNumber} (order {order.OrderId})");

// 3. Poll/wait for the code
var result = await client.WaitForSmsAsync(order.OrderId, timeoutSeconds: 120);
if (result.Success)
{
    Console.WriteLine($"Code: {result.Code}");
}
else
{
    Console.WriteLine("No SMS yet -- call GetSmsAsync later or CancelOrderAsync to refund.");
}
```

## Method groups

| Group | Examples |
|---|---|
| Activations / Orders | `CreateOrderAsync`, `GetOrderAsync`, `WaitForSmsAsync`, `CancelOrderAsync`, `SwapNumberAsync`, `RetryOrderAsync`, `ListOrdersAsync`, `OrderHistoryAsync`, `CancelAllOrdersAsync`, `SearchServicesAsync`, `FindCheapestAsync` |
| Rentals | `RentalsAvailableAsync`, `CreateFullAccessRentalAsync`, `CreatePlatformRentalAsync`, `ListRentalsAsync`, `ExtendRentalAsync`, `CancelRentalAsync` |
| Proxies | `ListProxyCatalogAsync`, `BuyProxyAsync`, `RotateProxyAsync`, `SetProxyTargetingAsync`, `TestProxyAsync`, `GenerateProxyEndpointAsync` |
| Account | `GetBalanceAsync`, `GetProfileAsync`, `GetTransactionsAsync`, `GetStatsAsync` |
| Session (beta) | `StartManualRegistrationSessionAsync` |
| Tools | `CheckNumberAsync` |
| Webhooks | `ListWebhooksAsync`, `CreateWebhookAsync`, `UpdateWebhookAsync`, `DeleteWebhookAsync`, `TestWebhookAsync`, `ListWebhookDeliveriesAsync` |

Full docs: [virtualsms.io/docs](https://virtualsms.io/docs).

## Constructor

```csharp
var client = new VirtualSMSClient(
    apiKey: "vsms_your_api_key",
    options: new VirtualSMSClientOptions
    {
        BaseUrl = "https://virtualsms.io/api/v1", // default; override or set VIRTUALSMS_BASE_URL
        TimeoutSeconds = 30,                       // default
    });
```

## Error handling

Every error the SDK raises derives from `VirtualSMSException`. Typed subclasses map to HTTP
status codes: `BadApiKeyException` (401), `InsufficientBalanceException` (402),
`NotFoundException` (404), `RateLimitedException` (429, never auto-retried), and
`ServerErrorException` (5xx -- `IsMutating` tells you whether the failed call may have already
changed state server-side; if so, verify with a read call like `ListOrdersAsync` before
retrying, never blind-retry a purchase/cancel/rotate/extend). `CooldownActiveException` is a
purely local, no-network-call guard that `CancelOrderAsync`/`SwapNumberAsync` throw when the
post-purchase cooldown hasn't elapsed yet.

```csharp
try
{
    var order = await client.CreateOrderAsync("wa", "GB");
}
catch (InsufficientBalanceException)
{
    Console.WriteLine("Top up your balance first.");
}
catch (ServerErrorException ex) when (ex.IsMutating)
{
    // May have gone through despite the error -- verify before retrying.
    var recent = await client.ListOrdersAsync();
}
```

GET requests get a bounded, automatic retry (up to 3 attempts, exponential backoff) on network
errors and 5xx responses. Mutating calls (POST/PATCH/DELETE) are never auto-retried by the SDK.

## Rental tiers

Two tiers, both refund-identical (full refund within 20 minutes of purchase, before the first
SMS): `RentalTier.FullAccess` (local SIM inventory, any service) and `RentalTier.Platform`
(sourced via our global supplier network, one service per number, 24/72/168h durations only).

## Examples

See `examples/` for runnable end-to-end flows: activation (`BasicActivation`), rental
(`RentalFlow`), and proxy (`ProxyFlow`).

## Links

- **Homepage:** [virtualsms.io](https://virtualsms.io)
- **Docs:** [virtualsms.io/docs](https://virtualsms.io/docs)
- **MCP server:** [virtualsms.io/mcp](https://virtualsms.io/mcp)
- **Pricing:** [virtualsms.io/pricing](https://virtualsms.io/pricing)
- **REST API:** [virtualsms.io/api/v1](https://virtualsms.io/api/v1)

## Ecosystem

- [Official MCP registry](https://registry.modelcontextprotocol.io): server id `io.github.virtualsms-io/sms`
- [VirtualSMS on Glama](https://glama.ai/mcp/servers)
- [Smithery](https://smithery.ai/servers/virtualsms/virtualsms-mcp)
- [mcp.so](https://mcp.so/servers/mcp-server-virtualsms-io)
- [npm: virtualsms-mcp](https://www.npmjs.com/package/virtualsms-mcp)

## Other SDKs

- **Python:** [pypi.org/project/virtualsms](https://pypi.org/project/virtualsms/)
- **Node.js:** [npmjs.com/package/virtualsms-sdk](https://www.npmjs.com/package/virtualsms-sdk)
- **PHP:** [packagist.org/packages/virtualsms/sdk](https://packagist.org/packages/virtualsms/sdk)
- **Ruby:** [rubygems.org/gems/virtualsms-sdk](https://rubygems.org/gems/virtualsms-sdk)

## Development

Run `sh scripts/check-positioning.sh` before committing copy changes. It fails on stale service
or country counts and other banned positioning wording.

This repo was built without a local .NET SDK -- `dotnet build`/`test`/`pack` run in CI (see
`.github/workflows/`) rather than locally. See `PROVENANCE.md` for the v1 -> v2 rewrite history.

## Changelog

**2.0.0** -- Breaking change. Full rewrite as a native REST v1 client (`https://virtualsms.io/api/v1`).
The v1.x client talked to the legacy `handler_api.php` (sms-activate-compatible) dispatcher and
covered only balance/number/status/done/cancel/wait; v2 SDKs never call `handler_api.php` and
cover the full 40+ method REST v1 surface (activations, rentals, proxies, account, webhooks,
tools, and beta browser sessions). If you're on 1.x, this is not a drop-in upgrade -- method
names, the constructor, and the base URL all changed.

## License

MIT
