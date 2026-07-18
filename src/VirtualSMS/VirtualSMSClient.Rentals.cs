using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace VirtualSMS;

/// <summary>Rental tier. No supplier names are ever surfaced by this SDK.</summary>
public enum RentalTier
{
    /// <summary>Local SIM inventory, usable for any service, longer durations.</summary>
    FullAccess,
    /// <summary>Sourced via our global supplier network, locked to one chosen service, short durations (24/72/168h).</summary>
    Platform,
}

public sealed partial class VirtualSMSClient
{
    /// <summary>Raw Full-Access pricing tiers (catalog dump, not authoritative for what's purchasable today). Public.</summary>
    public Task<List<RentalPricingTier>> RentalsPricingAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<RentalPricingTier>>("rentals/pricing", null, requireAuth: false, cancellationToken);

    /// <summary>List country availability + pricing per tier. Public.</summary>
    public Task<RentalAvailabilityResult> RentalsAvailableAsync(
        string? country = null,
        string? service = null,
        string? type = null,
        RentalTier tier = RentalTier.FullAccess,
        CancellationToken cancellationToken = default)
    {
        // "platform" tier maps to the backend's opaque provider=network token.
        var provider = tier == RentalTier.Platform ? "network" : null;
        return GetAsync<RentalAvailabilityResult>(
            "rentals/available",
            Q(("country", country), ("service", service), ("type", type), ("provider", provider)),
            requireAuth: false,
            cancellationToken);
    }

    /// <summary>List platform-tier services available in a country with stock + retail price. Public. Explicit field allowlist -- never forwards an internal supplier-code field the backend may include.</summary>
    public async Task<List<RentalCatalogService>> RentalsServicesAsync(string countryCode, int durationHours = 24, CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync(
            "rentals/services",
            Q(("country_code", countryCode), ("duration", durationHours.ToString())),
            requireAuth: false,
            cancellationToken).ConfigureAwait(false);

        var arr = FindArray(doc.RootElement, "services");
        var result = new List<RentalCatalogService>();
        foreach (var s in arr)
        {
            result.Add(new RentalCatalogService
            {
                ServiceId = StringOf(s, "service_id") ?? "",
                ServiceName = StringOf(s, "service_name") ?? "",
                PhysicalCount = (int)(NumberOf(s, "physical_count") ?? 0),
                OurPrice = NumberOf(s, "our_price"),
                BasePrice = NumberOf(s, "base_price"),
                Popular = BoolOf(s, "popular") ?? false,
                IconUrl = StringOf(s, "icon_url"),
            });
        }
        return result;
    }

    /// <summary>Catalog price for (service, country, duration) platform-tier combo. Public.</summary>
    public Task<RentalPriceResult> RentalsPriceAsync(string service, string countryCode, int durationHours, CancellationToken cancellationToken = default)
        => GetAsync<RentalPriceResult>(
            "rentals/price",
            Q(("service", service), ("country_code", countryCode), ("duration", durationHours.ToString())),
            requireAuth: false,
            cancellationToken);

    /// <summary>Create a Full Access tier rental: local SIM inventory, any service.</summary>
    public Task<CreateRentalResult> CreateFullAccessRentalAsync(
        string country,
        int durationHours,
        string? service = null,
        bool autoRenew = false,
        CancellationToken cancellationToken = default)
        => MutateAsync<CreateRentalResult>(
            HttpMethod.Post,
            "rentals",
            new { country, rental_type = service is not null ? "service" : "full", duration_hours = durationHours, service, auto_renew = autoRenew },
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Create a Platform tier rental: sourced via our global supplier
    /// network, locked to one service per number, 24/72/168h durations only.
    /// Resolves country_code (ISO-2) to the internal numeric ID via
    /// <see cref="PlatformTierCountryIds"/> -- callers never need to know or
    /// pass the numeric ID.
    /// </summary>
    public async Task<CreateRentalResult> CreatePlatformRentalAsync(string service, string countryCode, int durationHours, CancellationToken cancellationToken = default)
    {
        if (!PlatformTierCountryIds.Map.TryGetValue(countryCode.ToUpperInvariant(), out var countryId))
        {
            throw new VirtualSMSException(
                $"Platform-tier rentals are not available for country_code \"{countryCode}\". " +
                "Use RentalsAvailableAsync with tier=Platform to see supported countries.");
        }

        using var doc = await GetJsonMutationDocumentAsync(
            "rentals/provider",
            new { service, country = countryId, duration_hours = durationHours, provider = "network" },
            cancellationToken).ConfigureAwait(false);

        var root = doc.RootElement;
        return new CreateRentalResult
        {
            Success = BoolOf(root, "success") ?? true,
            RentalId = StringOf(root, "rental_id") ?? "",
            PhoneNumber = StringOf(root, "phone_number") ?? "",
            ExpiresAt = StringOf(root, "expires_at") ?? "",
            RetailCost = NumberOf(root, "retail_cost"),
            Currency = StringOf(root, "currency"),
            Status = "active",
        };
    }

    /// <summary>List rentals, optionally filtered by status (server default: "active").</summary>
    public Task<List<Rental>> ListRentalsAsync(string? status = null, CancellationToken cancellationToken = default)
        => GetAsync<List<Rental>>("rentals", status is null ? null : Q(("status", status)), requireAuth: true, cancellationToken);

    /// <summary>Get one rental by id. No dedicated backend route exists -- this is client-side: ListRentalsAsync("all").Find(id).</summary>
    public async Task<Rental?> GetRentalAsync(string rentalId, CancellationToken cancellationToken = default)
    {
        var all = await ListRentalsAsync("all", cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(r => r.Id == rentalId);
    }

    /// <summary>Extend an active rental, charged at current catalog price.</summary>
    public Task<RentalActionResult> ExtendRentalAsync(string rentalId, int durationHours, CancellationToken cancellationToken = default)
        => MutateAsync<RentalActionResult>(HttpMethod.Post, $"rentals/{Uri.EscapeDataString(rentalId)}/extend", new { duration_hours = durationHours }, requireAuth: true, cancellationToken: cancellationToken);

    /// <summary>Full refund: only within 20 minutes of purchase and before the first SMS. Either tier.</summary>
    public Task<RentalActionResult> CancelRentalAsync(string rentalId, CancellationToken cancellationToken = default)
        => MutateAsync<RentalActionResult>(HttpMethod.Post, $"rentals/{Uri.EscapeDataString(rentalId)}/cancel", null, requireAuth: true, cancellationToken: cancellationToken);

    // release_rental is intentionally NOT implemented: gated behind
    // VIRTUALSMS_ENABLE_RELEASE on the MCP surface pending a pricing decision
    // (VSMS-486). Out of scope for SDK v2.0.0 -- see the canonical spec appendix.

    private async Task<JsonDocument> GetJsonMutationDocumentAsync(string path, object body, CancellationToken cancellationToken)
        => await MutateAsync<JsonDocument>(HttpMethod.Post, path, body, requireAuth: true, cancellationToken: cancellationToken).ConfigureAwait(false);
}
