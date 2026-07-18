using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    // ─── Catalog ────────────────────────────────────────────────────────────

    /// <summary>List all SMS-verification services (Telegram, WhatsApp, etc.). Public, no auth.</summary>
    public async Task<List<Service>> ListServicesAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("customer/services", null, requireAuth: false, cancellationToken).ConfigureAwait(false);
        var arr = FindArray(doc.RootElement, "services");
        var result = new List<Service>();
        foreach (var s in arr)
        {
            result.Add(new Service
            {
                Code = StringOf(s, "service_id") ?? StringOf(s, "code") ?? "",
                Name = StringOf(s, "service_name") ?? StringOf(s, "name") ?? "",
                Icon = StringOf(s, "icon"),
            });
        }
        return result;
    }

    /// <summary>List all available countries. Public, no auth.</summary>
    public async Task<List<Country>> ListCountriesAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("customer/countries", null, requireAuth: false, cancellationToken).ConfigureAwait(false);
        var arr = FindArray(doc.RootElement, "countries");
        var result = new List<Country>();
        foreach (var c in arr)
        {
            result.Add(new Country
            {
                Iso = StringOf(c, "country_id") ?? StringOf(c, "iso") ?? "",
                Name = StringOf(c, "country_name") ?? StringOf(c, "name") ?? "",
                Flag = StringOf(c, "flag"),
            });
        }
        return result;
    }

    /// <summary>
    /// Catalog rows carrying REAL per-country stock (Count &gt; 0 = in stock).
    /// GetPriceAsync and FindCheapestAsync both read stock from here -- /price
    /// alone never carries an availability field.
    /// </summary>
    public async Task<List<CatalogCountry>> GetCatalogCountriesAsync(string service, CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("catalog/countries", Q(("service", service)), requireAuth: false, cancellationToken).ConfigureAwait(false);
        var arr = FindArray(doc.RootElement, "countries");
        var result = new List<CatalogCountry>();
        foreach (var c in arr)
        {
            result.Add(new CatalogCountry
            {
                Iso = StringOf(c, "id") ?? StringOf(c, "iso") ?? StringOf(c, "country") ?? "",
                Name = StringOf(c, "name") ?? StringOf(c, "country_name") ?? "",
                PriceUsd = NumberOf(c, "price") ?? NumberOf(c, "our_price") ?? NumberOf(c, "price_usd") ?? 0,
                Count = (int)(NumberOf(c, "count") ?? 0),
            });
        }
        return result;
    }

    /// <summary>
    /// Check price + REAL stock for a service+country combo. /price alone
    /// returns no availability field, so this fails closed: it cross-checks
    /// GetCatalogCountriesAsync's per-country Count (Count &gt; 0 = in stock)
    /// before ever reporting Available = true. Public, no auth.
    /// </summary>
    public async Task<Price> GetPriceAsync(string service, string country, CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("price", Q(("service", service), ("country", country)), requireAuth: false, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        var priceUsd = NumberOf(root, "price") ?? NumberOf(root, "price_usd") ?? 0;
        var currency = StringOf(root, "currency") ?? "USD";

        bool available;
        try
        {
            var catalog = await GetCatalogCountriesAsync(service, cancellationToken).ConfigureAwait(false);
            var row = catalog.FirstOrDefault(c => string.Equals(c.Iso, country, StringComparison.OrdinalIgnoreCase));
            available = row is not null && row.Count > 0;
        }
        catch
        {
            available = false; // fail closed on catalog lookup error
        }

        return new Price { PriceUsd = priceUsd, Currency = currency, Available = available };
    }

    // ─── Orders ─────────────────────────────────────────────────────────────

    /// <summary>Buy a virtual number for one-off SMS verification. Requires an API key.</summary>
    public Task<Order> CreateOrderAsync(string service, string country, CancellationToken cancellationToken = default)
        => MutateAsync<Order>(HttpMethod.Post, "customer/purchase", new { service, country }, requireAuth: true, cancellationToken: cancellationToken);

    /// <summary>Full order detail including any received SMS.</summary>
    public Task<Order> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => GetAsync<Order>($"customer/order/{Uri.EscapeDataString(orderId)}", null, requireAuth: true, cancellationToken);

    /// <summary>
    /// Poll for SMS delivery on an order -- a thin, normalized wrapper over
    /// GetOrderAsync. Normalizes canonical Messages[] and legacy sms_code/
    /// sms_text into one shape, and extracts a best-guess numeric code.
    /// </summary>
    public async Task<SmsResult> GetSmsAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        var messages = NormalizeMessages(order);
        var firstContent = messages.Count > 0 ? messages[0].Content : null;
        var code = order.SmsCode ?? (firstContent is not null ? ExtractCode(firstContent) : null);

        return new SmsResult
        {
            Status = order.Status,
            PhoneNumber = order.PhoneNumber,
            Messages = messages.Count > 0 ? messages : null,
            Code = code,
            SmsCode = code,
            SmsText = firstContent,
        };
    }

    private static List<SmsMessage> NormalizeMessages(Order order)
    {
        if (order.Messages is { Count: > 0 }) return order.Messages;
        if (!string.IsNullOrEmpty(order.SmsText) || !string.IsNullOrEmpty(order.SmsCode))
        {
            return new List<SmsMessage> { new() { Content = order.SmsText ?? order.SmsCode ?? "" } };
        }
        return new List<SmsMessage>();
    }

    // First 4-8 digit run wins (covers "SMS code: 666512", "Your code is 1234", etc.).
    private static readonly Regex CodePattern = new(@"\b(\d{4,8})\b", RegexOptions.Compiled);

    private static string? ExtractCode(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var m = CodePattern.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// Block until an SMS arrives or the timeout elapses. Polling-only in
    /// v2.0.0 (the optional WebSocket race described in the SDK spec is a
    /// v2.1 candidate) -- polls GetOrderAsync every pollIntervalSeconds.
    /// Never throws on timeout: returns a result with Success = false.
    /// Throws if the order reaches a terminal failure state first.
    ///
    /// Defaults (300s timeout / 5s poll interval) intentionally differ from
    /// the MCP tool's own default (60s timeout, same 5s interval) -- a
    /// human/script blocking on this SDK call is typically willing to wait
    /// longer than an LLM agent loop, per the SDK spec.
    /// </summary>
    public async Task<WaitForSmsResult> WaitForSmsAsync(
        string orderId,
        int timeoutSeconds = 300,
        int pollIntervalSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var startedAt = DateTime.UtcNow;

        Order initial;
        try
        {
            initial = await GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new VirtualSMSException($"Failed to load order {orderId}: {ex.Message}", ex);
        }

        var phoneNumber = initial.PhoneNumber;

        WaitForSmsResult BuildSuccess(Order order, string deliveryMethod)
        {
            var messages = NormalizeMessages(order);
            var firstContent = messages.Count > 0 ? messages[0].Content : "";
            var code = ExtractCode(firstContent);
            return new WaitForSmsResult
            {
                Success = true,
                OrderId = orderId,
                PhoneNumber = phoneNumber,
                Status = "sms_received",
                Messages = messages,
                Code = code,
                SmsCode = code,
                SmsText = firstContent,
                DeliveryMethod = deliveryMethod,
                ElapsedSeconds = (int)Math.Round((DateTime.UtcNow - startedAt).TotalSeconds),
            };
        }

        var initialMessages = NormalizeMessages(initial);
        if (initialMessages.Count > 0)
        {
            return BuildSuccess(initial, "instant");
        }

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken).ConfigureAwait(false);

            var status = await GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            var statusMessages = NormalizeMessages(status);
            if (statusMessages.Count > 0)
            {
                return BuildSuccess(status, "polling");
            }
            if (status.Status is "cancelled" or "failed")
            {
                throw new VirtualSMSException($"Order {orderId} was {status.Status} before SMS arrived.");
            }
        }

        return new WaitForSmsResult
        {
            Success = false,
            Error = "timeout",
            OrderId = orderId,
            PhoneNumber = phoneNumber,
            Tip = "Call GetSmsAsync with this order id later to check, or CancelOrderAsync to refund.",
        };
    }

    // Reads cancel_available_at / swap_available_at off an order and returns a
    // CooldownActiveException if it's still in the future. Returns null when
    // the action is allowed (or the field is missing on a legacy payload, in
    // which case we let the backend make the call). Saves a round-trip on the
    // typical "caller fires immediately after purchase" pattern.
    private static CooldownActiveException? PreCheckCooldown(string? availableAt, string action)
    {
        if (string.IsNullOrEmpty(availableAt)) return null;
        if (!DateTimeOffset.TryParse(availableAt, out var availableTime)) return null;
        var now = DateTimeOffset.UtcNow;
        if (now >= availableTime) return null;
        var waitSeconds = (int)Math.Ceiling((availableTime - now).TotalSeconds);
        return new CooldownActiveException(action, waitSeconds, availableAt);
    }

    /// <summary>
    /// Cancel and refund an order (only before any SMS is received). Pre-checks
    /// the 120s post-purchase cooldown from a fresh GetOrderAsync call and
    /// throws <see cref="CooldownActiveException"/> locally rather than
    /// spending a round-trip on a call the backend would reject anyway.
    /// </summary>
    public async Task<CancelResult> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            var blocked = PreCheckCooldown(order.CancelAvailableAt, "cancel");
            if (blocked is not null) throw blocked;
        }
        catch (CooldownActiveException)
        {
            throw;
        }
        catch
        {
            // Lookup failed for any other reason. Let the backend handle it.
        }

        return await MutateAsync<CancelResult>(HttpMethod.Post, $"customer/cancel/{Uri.EscapeDataString(orderId)}", null, requireAuth: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get a new number for the same service/country, no extra charge. Same
    /// cooldown pre-check pattern as <see cref="CancelOrderAsync"/>.
    /// </summary>
    public async Task<Order> SwapNumberAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            var blocked = PreCheckCooldown(order.SwapAvailableAt, "swap");
            if (blocked is not null) throw blocked;
        }
        catch (CooldownActiveException)
        {
            throw;
        }
        catch
        {
            // Lookup failed for any other reason. Let the backend handle it.
        }

        return await MutateAsync<Order>(HttpMethod.Post, $"customer/swap/{Uri.EscapeDataString(orderId)}", null, requireAuth: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ask the provider to resend the SMS to the SAME number (not a new number; see SwapNumberAsync for that).</summary>
    public Task<RetryOrderResult> RetryOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => MutateAsync<RetryOrderResult>(HttpMethod.Post, $"orders/{Uri.EscapeDataString(orderId)}/retry", null, requireAuth: true, cancellationToken: cancellationToken);

    /// <summary>
    /// List orders, optionally filtered by status. A 404 on this endpoint is
    /// swallowed to an empty list rather than thrown -- the endpoint may not
    /// exist on older deployments.
    /// </summary>
    public async Task<List<Order>> ListOrdersAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = await GetJsonDocumentAsync("customer/orders", status is null ? null : Q(("status", status)), requireAuth: true, cancellationToken).ConfigureAwait(false);
            var arr = FindArray(doc.RootElement, "orders");
            var result = new List<Order>();
            foreach (var o in arr)
            {
                result.Add(new Order
                {
                    OrderId = StringOf(o, "order_id") ?? StringOf(o, "id") ?? "",
                    PhoneNumber = StringOf(o, "phone_number") ?? "",
                    Service = StringOf(o, "service_id") ?? StringOf(o, "service"),
                    Country = StringOf(o, "country_id") ?? StringOf(o, "country"),
                    Price = NumberOf(o, "price_charged") ?? NumberOf(o, "price"),
                    CreatedAt = StringOf(o, "created_at"),
                    ExpiresAt = StringOf(o, "expires_at"),
                    Status = StringOf(o, "status") ?? "",
                    SmsCode = StringOf(o, "sms_code"),
                    SmsText = StringOf(o, "sms_text"),
                    CancelAvailableAt = StringOf(o, "cancel_available_at"),
                    SwapAvailableAt = StringOf(o, "swap_available_at"),
                });
            }
            return result;
        }
        catch (NotFoundException)
        {
            return new List<Order>();
        }
    }

    /// <summary>Order history with client-side filtering (service/country/since_days) and a server-mirrored cap.</summary>
    public async Task<OrderHistoryResult> OrderHistoryAsync(
        string? status = null,
        string? service = null,
        string? country = null,
        int? sinceDays = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Min(Math.Max(limit, 1), 50);
        var orders = await ListOrdersAsync(status, cancellationToken).ConfigureAwait(false);

        DateTimeOffset? cutoff = sinceDays.HasValue ? DateTimeOffset.UtcNow.AddDays(-sinceDays.Value) : null;
        var serviceFilter = service?.ToLowerInvariant();
        var countryFilter = country?.ToUpperInvariant();

        var filtered = orders.Where(o =>
        {
            if (cutoff is not null)
            {
                if (!DateTimeOffset.TryParse(o.CreatedAt, out var ts) || ts < cutoff.Value) return false;
            }
            if (serviceFilter is not null && (o.Service ?? "").ToLowerInvariant() != serviceFilter) return false;
            if (countryFilter is not null && (o.Country ?? "").ToUpperInvariant() != countryFilter) return false;
            return true;
        }).ToList();

        var capped = filtered.Take(limit).ToList();

        return new OrderHistoryResult
        {
            Count = capped.Count,
            TotalMatched = filtered.Count,
            Filters = new OrderHistoryFilters { Status = status, Service = service, Country = country, SinceDays = sinceDays },
            Orders = capped,
        };
    }

    private static readonly HashSet<string> ActiveOrderStatuses = new(StringComparer.Ordinal) { "waiting", "pending", "sms_received", "created" };

    private sealed record OneCancelOutcome(bool Ok, string OrderId, bool Refunded, string? Error);

    private async Task<OneCancelOutcome> CancelOneOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            var res = await CancelOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            return new OneCancelOutcome(true, orderId, res.Refunded, null);
        }
        catch (Exception ex)
        {
            return new OneCancelOutcome(false, orderId, false, ex.Message);
        }
    }

    /// <summary>Bulk-cancel every active order. Gathers with partial failure -- never aborts on the first error.</summary>
    public async Task<CancelAllOrdersResult> CancelAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await ListOrdersAsync(null, cancellationToken).ConfigureAwait(false);
        var active = orders.Where(o => ActiveOrderStatuses.Contains(o.Status)).ToList();

        if (active.Count == 0)
        {
            return new CancelAllOrdersResult { Cancelled = 0, Failed = 0, TotalActive = 0, Message = "No active orders to cancel." };
        }

        var succeeded = new List<CancelledOrderRef>();
        var failed = new List<CancelOrderFailure>();

        var tasks = active.Select(o => CancelOneOrderAsync(o.OrderId, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var r in results)
        {
            if (r.Ok) succeeded.Add(new CancelledOrderRef { OrderId = r.OrderId, Refunded = r.Refunded });
            else failed.Add(new CancelOrderFailure { OrderId = r.OrderId, Error = r.Error ?? "unknown error" });
        }

        return new CancelAllOrdersResult
        {
            Cancelled = succeeded.Count,
            Failed = failed.Count,
            TotalActive = active.Count,
            CancelledOrders = succeeded,
            Failures = failed,
        };
    }

    /// <summary>Find the right service code using natural language ("uber", "binance", "steam"). Client-side fuzzy match over ListServicesAsync.</summary>
    public async Task<SearchServicesResult> SearchServicesAsync(string query, CancellationToken cancellationToken = default)
    {
        var services = await ListServicesAsync(cancellationToken).ConfigureAwait(false);
        var q = query.ToLowerInvariant().Trim();
        var queryTokens = q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var scored = services.Select(s =>
        {
            var name = s.Name.ToLowerInvariant();
            var code = s.Code.ToLowerInvariant();
            double score;

            if (code == q || name == q)
            {
                score = 1.0;
            }
            else if (code.StartsWith(q, StringComparison.Ordinal) || name.StartsWith(q, StringComparison.Ordinal))
            {
                score = 0.9;
            }
            else if (code.Contains(q, StringComparison.Ordinal) || name.Contains(q, StringComparison.Ordinal))
            {
                score = 0.7;
            }
            else
            {
                var nameTokens = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var matches = queryTokens.Count(qt => nameTokens.Any(nt => nt.Contains(qt, StringComparison.Ordinal) || qt.Contains(nt, StringComparison.Ordinal)));
                score = matches > 0 ? (matches / (double)Math.Max(queryTokens.Length, nameTokens.Length)) * 0.6 : 0;
            }

            return new ServiceMatch { Code = s.Code, Name = s.Name, MatchScore = Math.Round(score, 2) };
        });

        var matches = scored.Where(m => m.MatchScore >= 0.5).OrderByDescending(m => m.MatchScore).Take(5).ToList();

        return matches.Count > 0
            ? new SearchServicesResult { Query = query, Matches = matches, Tip = "Use the Code field as the service parameter in other methods." }
            : new SearchServicesResult { Query = query, Matches = matches, Message = "No matching services found", Tip = "Try ListServicesAsync to browse all available services." };
    }

    /// <summary>Find the cheapest in-stock countries for a service, sorted by price. Reads real stock from GetCatalogCountriesAsync, never fans out over /price.</summary>
    public async Task<FindCheapestResult> FindCheapestAsync(string service, int limit = 5, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogCountriesAsync(service, cancellationToken).ConfigureAwait(false);
        var results = catalog
            .Where(c => c.Count > 0)
            .Select(c => new CheapestOption { Country = c.Iso, CountryName = c.Name, PriceUsd = c.PriceUsd, Stock = true })
            .OrderBy(c => c.PriceUsd)
            .ToList();

        var top = results.Take(limit).ToList();

        if (top.Count == 0)
        {
            return new FindCheapestResult
            {
                Service = service,
                CheapestOptions = top,
                TotalAvailableCountries = 0,
                Message = $"No countries available for service \"{service}\". Use SearchServicesAsync to verify the service code, or ListServicesAsync to see all available services.",
            };
        }

        return new FindCheapestResult { Service = service, CheapestOptions = top, TotalAvailableCountries = results.Count };
    }

    // ─── JSON helpers shared across method groups ──────────────────────────

    private async Task<JsonDocument> GetJsonDocumentAsync(
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query,
        bool requireAuth,
        CancellationToken cancellationToken)
    {
        // Reuses the same GET plumbing (retry, error mapping) as GetAsync<T>,
        // but returns a raw JsonDocument for endpoints whose response shape
        // needs field-by-field normalization instead of a direct record map.
        return await GetAsync<JsonDocument>(path, query, requireAuth, cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<JsonElement> FindArray(JsonElement root, string wrapperKey)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().ToList();
        }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(wrapperKey, out var wrapped) && wrapped.ValueKind == JsonValueKind.Array)
        {
            return wrapped.EnumerateArray().ToList();
        }
        return Array.Empty<JsonElement>();
    }

    internal static string? StringOf(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            JsonValueKind.True or JsonValueKind.False => v.ToString(),
            _ => null,
        };
    }

    internal static double? NumberOf(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => null,
        };
    }

    internal static bool? BoolOf(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
