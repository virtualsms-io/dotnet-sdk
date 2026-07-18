// Proxy flow: browse the catalog, buy some traffic, rotate the exit IP, then
// generate a ready-to-use connection string.
//
//   VIRTUALSMS_API_KEY=vsms_... dotnet run

using VirtualSMS;

var apiKey = Environment.GetEnvironmentVariable("VIRTUALSMS_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set VIRTUALSMS_API_KEY first. Get a key at https://virtualsms.io/dashboard");
    return 1;
}

using var client = new VirtualSMSClient(apiKey);

var catalog = await client.ListProxyCatalogAsync();
Console.WriteLine($"{catalog.Count} pool types available.");
var pool = catalog.FirstOrDefault();
if (pool is null)
{
    Console.WriteLine("No pool types returned.");
    return 0;
}

var purchase = await client.BuyProxyAsync(poolType: pool.Id, gb: 1);
Console.WriteLine($"Bought proxy {purchase.ProxyId}: {purchase.ProxyHost}:{purchase.ProxyPort}");

var rotated = await client.RotateProxyAsync(purchase.ProxyId);
Console.WriteLine($"Rotated: {rotated.Message}");

var endpoint = await client.GenerateProxyEndpointAsync(
    purchase.ProxyId,
    new ProxyEndpointBuilder.Params
    {
        CountryCode = purchase.CountryCode,
        Protocol = ProxyProtocol.Http,
        Format = ProxyEndpointFormat.HostPortUserPass,
    });
Console.WriteLine($"Connection string: {endpoint.Endpoints.FirstOrDefault()}");

return 0;
