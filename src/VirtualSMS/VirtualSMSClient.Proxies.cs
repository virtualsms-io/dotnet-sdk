using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    /// <summary>List proxy pool types, countries, price/GB. Public, ~10min cache.</summary>
    public async Task<List<ProxyCatalogPoolType>> ListProxyCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("proxies/catalog", null, requireAuth: false, cancellationToken).ConfigureAwait(false);
        var arr = FindArray(doc.RootElement, "pool_types");
        var result = new List<ProxyCatalogPoolType>();
        foreach (var p in arr)
        {
            var countries = new List<ProxyCatalogCountry>();
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("countries", out var cs) && cs.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cs.EnumerateArray())
                {
                    countries.Add(new ProxyCatalogCountry
                    {
                        Code = StringOf(c, "code") ?? "",
                        Name = StringOf(c, "name") ?? "",
                        Available = BoolOf(c, "available") ?? false,
                        IpCount = (int)(NumberOf(c, "ip_count") ?? 0),
                    });
                }
            }
            result.Add(new ProxyCatalogPoolType
            {
                Id = StringOf(p, "id") ?? "",
                Label = StringOf(p, "label") ?? "",
                PricePerGb = NumberOf(p, "price_per_gb") ?? 0,
                Countries = countries,
            });
        }
        return result;
    }

    /// <summary>List owned proxies with credentials.</summary>
    public Task<List<ProxyListItem>> ListProxiesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<ProxyListItem>>("proxies", null, requireAuth: true, cancellationToken);

    /// <summary>Purchase proxy traffic (GB) for a pool type.</summary>
    public Task<ProxyPurchaseResult> BuyProxyAsync(
        string poolType,
        double gb,
        string? countryCode = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => MutateAsync<ProxyPurchaseResult>(
            HttpMethod.Post,
            "proxies",
            new { pool_type = poolType, gb, country_code = countryCode, idempotency_key = idempotencyKey },
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>Get a fresh exit IP for an existing proxy.</summary>
    public Task<ProxyRotateResult> RotateProxyAsync(string proxyId, int? port = null, CancellationToken cancellationToken = default)
        => MutateAsync<ProxyRotateResult>(
            HttpMethod.Post,
            $"proxies/{Uri.EscapeDataString(proxyId)}/rotate",
            port.HasValue ? new { port = port.Value } : null,
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>Cached GB used/remaining (refreshed ~5min, no upstream call).</summary>
    public Task<ProxyUsage> GetProxyUsageAsync(string proxyId, CancellationToken cancellationToken = default)
        => GetAsync<ProxyUsage>($"proxies/{Uri.EscapeDataString(proxyId)}/usage", null, requireAuth: true, cancellationToken);

    /// <summary>Per-day GB/requests series, 7d or 30d.</summary>
    public Task<ProxyUsageHistoryResult> GetProxyUsageHistoryAsync(string proxyId, string range = "7d", CancellationToken cancellationToken = default)
        => GetAsync<ProxyUsageHistoryResult>($"proxies/{Uri.EscapeDataString(proxyId)}/usage-history", Q(("range", range)), requireAuth: true, cancellationToken);

    /// <summary>
    /// Persist default geo-targeting on a proxy sub-user. Country-only is
    /// free; cities/asns bill the customer's own funded GB at 2x on
    /// non-premium pools (free on residential_premium). Backend does NOT
    /// accept state/zip here (only country_code + cities + asns) -- state/zip
    /// refinement is a per-connection username param only, see
    /// GenerateProxyEndpoint.
    /// </summary>
    public Task<ProxyTargetingResult> SetProxyTargetingAsync(
        string proxyId,
        string countryCode,
        List<string>? cities = null,
        List<int>? asns = null,
        CancellationToken cancellationToken = default)
        => MutateAsync<ProxyTargetingResult>(
            HttpMethod.Post,
            $"proxies/{Uri.EscapeDataString(proxyId)}/targeting",
            new { country_code = countryCode, cities, asns },
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>Dial out through the proxy and report exit IP/country/city/ISP/latency. Rate-limited ~1/20s per proxy.</summary>
    public Task<ProxyTestResult> TestProxyAsync(
        string proxyId,
        string country,
        string? session = null,
        string? protocol = null,
        CancellationToken cancellationToken = default)
        => MutateAsync<ProxyTestResult>(
            HttpMethod.Post,
            $"proxies/{Uri.EscapeDataString(proxyId)}/test",
            new { country, session, protocol },
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>Discover valid cities/states/asns/zips for a pool_type+country. Public, 6h cache. Not available for residential_premium.</summary>
    public async Task<List<ProxyLocationItem>> ListProxyLocationsAsync(string poolType, string country, string kind, CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync(
            "proxies/locations",
            Q(("pool_type", poolType), ("country", country), ("kind", kind)),
            requireAuth: false,
            cancellationToken).ConfigureAwait(false);

        var arr = FindArray(doc.RootElement, "items");
        var result = new List<ProxyLocationItem>();
        foreach (var it in arr)
        {
            result.Add(new ProxyLocationItem
            {
                Code = StringOf(it, "code") ?? "",
                Name = StringOf(it, "name") ?? "",
                Count = (int)(NumberOf(it, "count") ?? 0),
            });
        }
        return result;
    }

    /// <summary>
    /// Compose a ready-to-use connection string. No backend call, no
    /// purchase -- pure client-side function. Looks up the proxy's
    /// credentials via ListProxiesAsync, then delegates to
    /// <see cref="ProxyEndpointBuilder"/> (must stay byte-identical in
    /// behavior to the dashboard's own generator; drift here silently breaks
    /// connection strings).
    /// </summary>
    public async Task<ProxyEndpointResult> GenerateProxyEndpointAsync(string proxyId, ProxyEndpointBuilder.Params options, CancellationToken cancellationToken = default)
    {
        RequireApiKey();
        var proxies = await ListProxiesAsync(cancellationToken).ConfigureAwait(false);
        var proxy = proxies.FirstOrDefault(p => p.ProxyId == proxyId);
        if (proxy is null)
        {
            throw new NotFoundException($"Not found: proxy {proxyId} does not exist on this account");
        }
        return ProxyEndpointBuilder.Build(proxy, options);
    }
}
