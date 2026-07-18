# VirtualSMS .NET SDK

## What is VirtualSMS?

Official .NET SDK for the VirtualSMS API. VirtualSMS is an account verification platform for
individuals, developers, and AI agents: one-time SMS verification, dedicated number rentals,
matching-country proxies, and private cloud browser sessions (beta), all behind one API, one
MCP server, and one prepaid balance. This package wraps the REST API in native C#, backed by
real carrier-issued mobile numbers (real physical SIM cards, not VoIP) across 2500+ services
in 145+ countries.

Built for developers and AI agents: REST API, hosted MCP server, SDKs.

Covers the full customer-facing REST v1 surface: activations/orders (buy a number, poll or
wait for the code, cancel/swap/retry), rentals (Full Access and Platform tiers), proxies
(catalog, purchase, rotate, targeting, connection-string generation), account (balance,
profile, transactions, usage stats), webhooks (create/list/update/delete/test/deliveries),
carrier/line-type lookup, and manual-registration browser sessions (invite-only beta).

## Install

```bash
dotnet add package VirtualSMS
```

## Quickstart

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

## Capabilities

1. **One-time SMS verification.** Receive a code for a service like WhatsApp, Telegram, Discord,
   or a dating app, on demand, from $0.05 per code.
2. **Dedicated number rentals.** Hold one number for 1-30 days and receive SMS from any service
   on that number, from $0.25/day.
3. **Matching-country proxies.** Pair a number with an IP from the same country, across 223
   proxy countries, from $1.10/GB.
4. **Private cloud browser sessions (beta).** Start a country-matched browser in a live viewer
   for the signup step itself, invite-only.

## Why real SIM cards

VirtualSMS runs on real carrier-issued mobile numbers, backed by real physical SIM cards,
not VoIP. Services like WhatsApp, Telegram, Discord, and dating apps run a carrier lookup
before they send a code, and VoIP or virtual numbers fail that check more often than a real
SIM does. A physical SIM on a real carrier network reads like any other phone on that network,
carriers like Vodafone, O2, and T-Mobile depending on the country, which is part of why
VirtualSMS holds a 95%+ success rate across 2500+ services in 145+ countries.

## API coverage

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

### Constructor

```csharp
var client = new VirtualSMSClient(
    apiKey: "vsms_your_api_key",
    options: new VirtualSMSClientOptions
    {
        BaseUrl = "https://virtualsms.io/api/v1", // default; override or set VIRTUALSMS_BASE_URL
        TimeoutSeconds = 30,                       // default
    });
```

### Error handling

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

### Rental tiers

Two tiers, both refund-identical (full refund within 20 minutes of purchase, before the first
SMS): `RentalTier.FullAccess` (local SIM inventory, any service) and `RentalTier.Platform`
(sourced via our global supplier network, one service per number, 24/72/168h durations only).

### Examples

See `examples/` for runnable end-to-end flows: activation (`BasicActivation`), rental
(`RentalFlow`), and proxy (`ProxyFlow`).

## AI agents and MCP

This SDK is the API-client half: a typed .NET wrapper around the REST API for services and
backends that call C# directly. VirtualSMS also runs a separate hosted MCP server so an AI
agent (Claude, Cursor, or any MCP-compatible client) can request a number, wait for a code, or
manage a rental the same way a developer would call the API directly.

## FAQ

### What is VirtualSMS?

VirtualSMS is an account verification platform for individuals, developers, and AI agents. It combines one-time SMS verification, dedicated number rentals, matching-country proxies, and private cloud browser sessions behind one API, one MCP server, and one prepaid balance.

### Does VirtualSMS use real SIM cards or VoIP numbers?

VirtualSMS uses real carrier-issued mobile numbers, backed by real physical SIM cards, not VoIP. Many services, including WhatsApp, Telegram, Discord, and dating apps, reject VoIP and virtual numbers at signup; a real physical SIM on a real carrier network passes that check far more often, which is reflected in a 95%+ success rate.

### Which services and countries does VirtualSMS support?

VirtualSMS covers 2500+ services across 145+ countries for SMS verification and number rentals, plus matching-country proxies across 223 proxy countries. Coverage spans messaging apps, social platforms, marketplaces, dating apps, and financial services.

### Can I rent a number, or only buy one-time codes?

Both. Buy a single one-time code from $0.05, or rent a dedicated number for 1-30 days from $0.25/day to receive SMS from any service on that number for the rental window.

### Does VirtualSMS work with AI agents and MCP?

Yes. VirtualSMS exposes a hosted MCP server plus a REST API and official SDKs in nine languages, so an AI agent can request a number, wait for a code, or manage a rental the same way a developer would call the API directly.

### How much does VirtualSMS cost?

Pricing is pay-as-you-go from one prepaid balance: SMS verification from $0.05 per code, number rentals from $0.25/day, and proxies from $1.10/GB. There is no subscription requirement.

### Is there a free API key?

Yes. Creating a VirtualSMS account issues an API key immediately, at no cost. You only spend from your prepaid balance when you place an order: an activation, a rental, or a proxy.

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
- **Go, Rust, Swift, Java:** all under [github.com/virtualsms-io](https://github.com/virtualsms-io)

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
