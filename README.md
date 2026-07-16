# VirtualSMS .NET SDK

VirtualSMS is an account verification platform that combines real carrier mobile numbers, matching-country proxies and a private cloud browser into one connected workflow.

Built for developers and AI agents: REST API, hosted MCP server, SDKs.

> **Unverified reconstruction.** This repo's source was re-created from the published NuGet package's own shipped `README.md` (see [`PROVENANCE.md`](./PROVENANCE.md)). It has not been built or tested against the live API. Don't treat anything here as production-verified until it's been independently built and smoke-tested.

## What this SDK does

This package is a thin .NET client for VirtualSMS's **SMS verification** endpoint (`handler_api.php`): real physical SIM cards, not VoIP, for receiving SMS codes on WhatsApp, Telegram, Google, and other services. It covers: checking balance, requesting a number, polling/waiting for the code, and marking an activation done or cancelled.

It does **not** cover proxies or number/platform rentals. Both are live on the wider platform but aren't implemented in this client. The private cloud browser is planned, coming soon, not yet available on any surface, this client or otherwise. For live proxies and rentals use:
- **REST API:** [virtualsms.io/docs](https://virtualsms.io/docs)
- **Hosted MCP server** (for AI agents): [virtualsms.io/mcp](https://virtualsms.io/mcp)

Proxy/rental support in this SDK is on the roadmap, not shipped. Treat any future mention of it here as "coming soon" until a versioned release actually adds it.

## Installation

```bash
dotnet add package VirtualSMS
```

## Quick Start

```csharp
var client = new VirtualSMS.VirtualSMSClient("vsms_your_api_key");
var balance = await client.GetBalanceAsync();
var activation = await client.GetNumberAsync("wa", country: 22); // WhatsApp, UK
var code = await client.WaitForCodeAsync(activation.ActivationId);
Console.WriteLine($"Code: {code}");
await client.DoneAsync(activation.ActivationId);
```

## API Reference

### `new VirtualSMSClient(apiKey, baseUrl?, httpClient?)`
Create a client. Get your API key at [virtualsms.io](https://virtualsms.io).

### `GetBalanceAsync() → Task<double>`
Returns account balance in USD.

### `GetNumberAsync(service, country = 187) → Task<Activation>`
Request a number for verification.

### `GetStatusAsync(activationId) → Task<ActivationStatus>`
Check if SMS arrived.

### `WaitForCodeAsync(activationId, timeoutSeconds = 300, pollIntervalSeconds = 5) → Task<string?>`
Poll automatically until code arrives. Default timeout: 5 minutes.

### `DoneAsync(activationId)` / `CancelAsync(activationId)`
Complete or cancel an activation.

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

Run `sh scripts/check-positioning.sh` before committing copy changes. It fails on
stale service or country counts and other banned positioning wording.

## License

MIT
