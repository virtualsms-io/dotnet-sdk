using System.Globalization;

namespace VirtualSMS;

/// <summary>
/// Composes ready-to-use proxy connection strings. Pure function, no network
/// call, no purchase -- one credential serves every country/refinement at
/// connection time. Ported byte-identical (in behavior) from the canonical
/// client's buildProxyUsername/buildProxyEndpointString/buildProxyEndpointResult
/// (client.ts), which itself mirrors the frontend's ProxyEndpointGenerator.
/// Any drift here silently breaks connection strings customers copy from the
/// dashboard -- do not "improve" the format without re-syncing from source.
/// </summary>
public static class ProxyEndpointBuilder
{
    // Fixed gateway ports. Rotating vs. sticky is encoded entirely in the
    // username's sessid/sessttl params, NOT by port selection.
    public const int HttpPort = 823;
    public const int Socks5Port = 824;

    public sealed class Params
    {
        public required string CountryCode { get; init; }
        public ProxyTargetBy TargetBy { get; init; } = ProxyTargetBy.Country;
        public string? LocationCode { get; init; }
        public ProxySession Session { get; init; } = ProxySession.Rotating;
        public int StickyTtlMinutes { get; init; } = 10;
        public int Count { get; init; } = 1;
        public ProxyProtocol Protocol { get; init; } = ProxyProtocol.Http;
        public ProxyEndpointFormat Format { get; init; } = ProxyEndpointFormat.HostPortUserPass;
    }

    public static ProxyEndpointResult Build(ProxyListItem proxy, Params p)
    {
        var ttl = p.StickyTtlMinutes <= 0 ? 10 : p.StickyTtlMinutes;
        var count = Math.Max(1, Math.Min(100, p.Count));
        var port = p.Protocol == ProxyProtocol.Socks5 ? Socks5Port : HttpPort;

        var hasLocation = !string.IsNullOrWhiteSpace(p.LocationCode);
        var premium2x = p.TargetBy != ProxyTargetBy.Country && hasLocation && proxy.PoolType != "residential_premium";

        List<string> endpoints;
        if (p.Session == ProxySession.Rotating)
        {
            var user = BuildUsername(proxy.ProxyLogin, p.CountryCode, p.TargetBy, p.LocationCode);
            var ep = BuildEndpointString(proxy.ProxyHost, port, user, proxy.ProxyPassword, p.Format, p.Protocol);
            endpoints = Enumerable.Repeat(ep, count).ToList();
        }
        else
        {
            endpoints = Enumerable.Range(1, count)
                .Select(i =>
                {
                    var user = BuildUsername(proxy.ProxyLogin, p.CountryCode, p.TargetBy, p.LocationCode, i, ttl);
                    return BuildEndpointString(proxy.ProxyHost, port, user, proxy.ProxyPassword, p.Format, p.Protocol);
                })
                .ToList();
        }

        return new ProxyEndpointResult
        {
            ProxyId = proxy.ProxyId,
            PoolType = proxy.PoolType,
            Host = proxy.ProxyHost,
            Port = port,
            Protocol = p.Protocol == ProxyProtocol.Socks5 ? "SOCKS5" : "HTTP",
            Session = p.Session == ProxySession.Sticky ? "sticky" : "rotating",
            StickyTtlMinutes = p.Session == ProxySession.Sticky ? ttl : null,
            CountryCode = p.CountryCode,
            TargetBy = TargetByToString(p.TargetBy),
            LocationCode = p.LocationCode,
            Premium2x = premium2x,
            Endpoints = endpoints,
        };
    }

    private static string TargetByToString(ProxyTargetBy t) => t switch
    {
        ProxyTargetBy.State => "state",
        ProxyTargetBy.City => "city",
        ProxyTargetBy.Zip => "zip",
        ProxyTargetBy.Asn => "asn",
        _ => "country",
    };

    private static string BuildUsername(
        string login,
        string countryCode,
        ProxyTargetBy targetBy,
        string? locationCode,
        int? stickyIndex = null,
        int? stickyMinutes = null)
    {
        var u = $"{login}__cr.{countryCode.ToLowerInvariant()}";
        var loc = (locationCode ?? "").Trim();
        if (loc.Length > 0 && targetBy != ProxyTargetBy.Country)
        {
            u += targetBy switch
            {
                ProxyTargetBy.State => $";state.{loc.ToLowerInvariant()}",
                ProxyTargetBy.City => $";city.{loc.ToLowerInvariant()}",
                ProxyTargetBy.Zip => $";zip.{loc}",
                ProxyTargetBy.Asn => $";asn.{loc}",
                _ => "",
            };
        }
        if (stickyIndex.HasValue)
        {
            u += $";sessid.s{stickyIndex.Value};sessttl.{stickyMinutes ?? 10}";
        }
        return u;
    }

    private static string BuildEndpointString(
        string host,
        int port,
        string user,
        string pass,
        ProxyEndpointFormat format,
        ProxyProtocol protocol)
    {
        if (format == ProxyEndpointFormat.HostPortUserPass)
        {
            return $"{host}:{port.ToString(CultureInfo.InvariantCulture)}:{user}:{pass}";
        }
        if (format == ProxyEndpointFormat.UserPassAtHostPort)
        {
            return $"{user}:{pass}@{host}:{port.ToString(CultureInfo.InvariantCulture)}";
        }
        var scheme = protocol == ProxyProtocol.Socks5 ? "socks5h" : "http";
        return $"curl -x \"{scheme}://{user}:{pass}@{host}:{port.ToString(CultureInfo.InvariantCulture)}\" https://api.ipify.org";
    }
}
