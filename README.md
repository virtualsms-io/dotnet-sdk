# VirtualSMS .NET SDK

.NET client for [VirtualSMS](https://virtualsms.io) — SMS verification using real physical SIM cards.

Real SIM cards in European and US mobile networks. Near-100% delivery on WhatsApp, Telegram, and platforms that block VoIP.

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

- **Website:** [virtualsms.io](https://virtualsms.io)
- **API Docs:** [virtualsms.io/api](https://virtualsms.io/api)
- **Pricing:** [virtualsms.io/pricing](https://virtualsms.io/pricing)
- **Python SDK:** [pypi.org/project/virtualsms](https://pypi.org/project/virtualsms/)
- **Node.js SDK:** [npmjs.com/package/virtualsms-sdk](https://www.npmjs.com/package/virtualsms-sdk)
- **PHP SDK:** [packagist.org/packages/virtualsms/sdk](https://packagist.org/packages/virtualsms/sdk)
- **Ruby SDK:** [rubygems.org/gems/virtualsms-sdk](https://rubygems.org/gems/virtualsms-sdk)

## License

MIT
