// Rental flow: check Full Access availability for a country, create a rental,
// extend it, then cancel for a refund (only valid within 20 minutes and
// before the first SMS).
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

const string country = "GB";

var availability = await client.RentalsAvailableAsync(country: country, tier: RentalTier.FullAccess);
Console.WriteLine($"{availability.TotalAvailable} countries available for Full Access rentals.");

var rental = await client.CreateFullAccessRentalAsync(country: country, durationHours: 24);
Console.WriteLine($"Rented {rental.PhoneNumber} (rental {rental.RentalId}), expires {rental.ExpiresAt}");

// Extend it by another 24 hours.
var extended = await client.ExtendRentalAsync(rental.RentalId, durationHours: 24);
Console.WriteLine($"Extended: new expiry {extended.NewExpiresAt}, charged ${extended.Price:F2}");

// Cancel for a refund (only within 20 minutes of purchase, before any SMS).
var cancelled = await client.CancelRentalAsync(rental.RentalId);
Console.WriteLine($"Cancelled: refund=${cancelled.Refund:F2}");

return 0;
