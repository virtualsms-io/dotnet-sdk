using VirtualSMS;
using Xunit;

namespace VirtualSMS.Tests;

/// <summary>
/// Minimum smoke coverage per the SDK spec's per-repo deliverables checklist.
/// Both ListServicesAsync and GetBalanceAsync require a valid key against
/// the live API, so both only run when a throwaway API key is supplied via
/// VIRTUALSMS_TEST_API_KEY -- CI stays green without a secret configured,
/// and exercises the authenticated paths when one is.
/// </summary>
public class SmokeTests
{
    [Fact]
    public async Task ListServices_ReturnsNonEmptyCatalog()
    {
        var apiKey = Environment.GetEnvironmentVariable("VIRTUALSMS_TEST_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // No throwaway key configured for this CI run -- soft-skip rather
            // than fail. Set VIRTUALSMS_TEST_API_KEY as a repo secret to
            // exercise the live path.
            return;
        }

        using var client = new VirtualSMSClient(apiKey);

        var services = await client.ListServicesAsync();

        Assert.NotEmpty(services);
        Assert.All(services, s => Assert.False(string.IsNullOrEmpty(s.Code)));
    }

    [Fact]
    public async Task GetBalance_SucceedsWithApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("VIRTUALSMS_TEST_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // No throwaway key configured for this CI run -- soft-skip rather
            // than fail. Set VIRTUALSMS_TEST_API_KEY as a repo secret to
            // exercise the authenticated path.
            return;
        }

        using var client = new VirtualSMSClient(apiKey);
        var balance = await client.GetBalanceAsync();

        Assert.True(balance.BalanceUsd >= 0);
    }

    [Fact]
    public void Constructor_RejectsEmptyApiKey()
    {
        Assert.Throws<ArgumentException>(() => new VirtualSMSClient(""));
    }

    [Fact]
    public void ProxyEndpointBuilder_IsPureAndDeterministic()
    {
        var proxy = new ProxyListItem
        {
            ProxyId = "px_1",
            PoolType = "residential",
            CountryCode = "US",
            GbTotal = 5,
            GbUsed = 1,
            GbRemaining = 4,
            ProxyHost = "gw.virtualsms.io",
            ProxyPort = 823,
            ProxyLogin = "user123",
            ProxyPassword = "pass456",
        };

        var result = ProxyEndpointBuilder.Build(proxy, new ProxyEndpointBuilder.Params
        {
            CountryCode = "US",
            Protocol = ProxyProtocol.Http,
            Format = ProxyEndpointFormat.HostPortUserPass,
        });

        Assert.Equal(823, result.Port);
        Assert.Equal("HTTP", result.Protocol);
        Assert.Single(result.Endpoints);
        Assert.Contains("gw.virtualsms.io:823:", result.Endpoints[0]);
        Assert.Contains("__cr.us", result.Endpoints[0]);
    }
}
