// Basic activation flow: buy a number, wait for the SMS code, cancel if unused.
//
// Get your API key at https://virtualsms.io/dashboard (Settings -> API Keys)
// and set it as an environment variable before running:
//   VIRTUALSMS_API_KEY=vsms_... dotnet run

using VirtualSMS;

var apiKey = Environment.GetEnvironmentVariable("VIRTUALSMS_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set VIRTUALSMS_API_KEY first. Get a key at https://virtualsms.io/dashboard");
    return 1;
}

using var client = new VirtualSMSClient(apiKey);

// Check balance before spending.
var balance = await client.GetBalanceAsync();
Console.WriteLine($"Balance: ${balance.BalanceUsd:F2}");

// Find the cheapest in-stock country for WhatsApp.
var cheapest = await client.FindCheapestAsync(service: "wa", limit: 3);
if (cheapest.CheapestOptions.Count == 0)
{
    Console.WriteLine(cheapest.Message ?? "No stock available right now.");
    return 0;
}
var pick = cheapest.CheapestOptions[0];
Console.WriteLine($"Cheapest: {pick.CountryName} (${pick.PriceUsd:F2})");

// Buy the number.
var order = await client.CreateOrderAsync(service: "wa", country: pick.Country);
Console.WriteLine($"Bought {order.PhoneNumber} (order {order.OrderId}), status={order.Status}");

// Wait for the SMS (up to 2 minutes).
var wait = await client.WaitForSmsAsync(order.OrderId, timeoutSeconds: 120);
if (wait.Success)
{
    Console.WriteLine($"Code received: {wait.Code} (via {wait.DeliveryMethod}, {wait.ElapsedSeconds}s)");
}
else
{
    Console.WriteLine("No SMS yet -- refunding the order instead of leaving it dangling.");
    try
    {
        var cancel = await client.CancelOrderAsync(order.OrderId);
        Console.WriteLine($"Cancelled: refunded={cancel.Refunded}");
    }
    catch (CooldownActiveException ex)
    {
        Console.WriteLine($"Can't cancel yet: {ex.Message}");
    }
}

return 0;
