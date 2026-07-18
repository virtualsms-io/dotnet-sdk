using System.Text.Json.Serialization;

namespace VirtualSMS;

// ─── Catalog / activations ───────────────────────────────────────────────

public sealed record Service
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("icon")] public string? Icon { get; init; }
}

public sealed record Country
{
    [JsonPropertyName("iso")] public string Iso { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("flag")] public string? Flag { get; init; }
}

public sealed record Price
{
    [JsonPropertyName("price_usd")] public double PriceUsd { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = "USD";
    [JsonPropertyName("available")] public bool Available { get; init; }
}

/// <summary>
/// A row from /catalog/countries. This is the only endpoint that carries
/// real per-country stock (<see cref="Count"/> &gt; 0 = in stock);
/// GetPriceAsync and FindCheapestAsync both read stock from here, never
/// from /price alone.
/// </summary>
public sealed record CatalogCountry
{
    [JsonPropertyName("iso")] public string Iso { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("price_usd")] public double PriceUsd { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

public sealed record SmsMessage
{
    [JsonPropertyName("content")] public string Content { get; init; } = "";
    [JsonPropertyName("sender")] public string? Sender { get; init; }
    [JsonPropertyName("received_at")] public string? ReceivedAt { get; init; }
}

public sealed record Order
{
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = "";
    [JsonPropertyName("phone_number")] public string PhoneNumber { get; init; } = "";
    [JsonPropertyName("service")] public string? Service { get; init; }
    [JsonPropertyName("country")] public string? Country { get; init; }
    [JsonPropertyName("price")] public double? Price { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
    [JsonPropertyName("expires_at")] public string? ExpiresAt { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("cancel_available_at")] public string? CancelAvailableAt { get; init; }
    [JsonPropertyName("swap_available_at")] public string? SwapAvailableAt { get; init; }
    // Legacy fields kept for backward compat with older API responses.
    [JsonPropertyName("sms_code")] public string? SmsCode { get; init; }
    [JsonPropertyName("sms_text")] public string? SmsText { get; init; }
    [JsonPropertyName("messages")] public List<SmsMessage>? Messages { get; init; }
}

public sealed record CancelResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("refunded")] public bool Refunded { get; init; }
}

public sealed record RetryOrderResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = "";
    [JsonPropertyName("message")] public string Message { get; init; } = "";
}

/// <summary>Response of GetSmsAsync -- a thin normalized wrapper over GetOrderAsync.</summary>
public sealed record SmsResult
{
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("phone_number")] public string PhoneNumber { get; init; } = "";
    [JsonPropertyName("messages")] public List<SmsMessage>? Messages { get; init; }
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("sms_code")] public string? SmsCode { get; init; }
    [JsonPropertyName("sms_text")] public string? SmsText { get; init; }
}

/// <summary>Result of WaitForSmsAsync. On timeout, Success is false and no code/messages are set.</summary>
public sealed record WaitForSmsResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = "";
    [JsonPropertyName("phone_number")] public string PhoneNumber { get; init; } = "";
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("messages")] public List<SmsMessage>? Messages { get; init; }
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("sms_code")] public string? SmsCode { get; init; }
    [JsonPropertyName("sms_text")] public string? SmsText { get; init; }
    [JsonPropertyName("delivery_method")] public string? DeliveryMethod { get; init; }
    [JsonPropertyName("elapsed_seconds")] public int? ElapsedSeconds { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("tip")] public string? Tip { get; init; }
}

public sealed record OrderHistoryFilters
{
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("service")] public string? Service { get; init; }
    [JsonPropertyName("country")] public string? Country { get; init; }
    [JsonPropertyName("since_days")] public int? SinceDays { get; init; }
}

public sealed record OrderHistoryResult
{
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("total_matched")] public int TotalMatched { get; init; }
    [JsonPropertyName("filters")] public OrderHistoryFilters Filters { get; init; } = new();
    [JsonPropertyName("orders")] public List<Order> Orders { get; init; } = new();
}

public sealed record CancelledOrderRef
{
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = "";
    [JsonPropertyName("refunded")] public bool Refunded { get; init; }
}

public sealed record CancelOrderFailure
{
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = "";
    [JsonPropertyName("error")] public string Error { get; init; } = "";
}

public sealed record CancelAllOrdersResult
{
    [JsonPropertyName("cancelled")] public int Cancelled { get; init; }
    [JsonPropertyName("failed")] public int Failed { get; init; }
    [JsonPropertyName("total_active")] public int TotalActive { get; init; }
    [JsonPropertyName("cancelled_orders")] public List<CancelledOrderRef> CancelledOrders { get; init; } = new();
    [JsonPropertyName("failures")] public List<CancelOrderFailure> Failures { get; init; } = new();
    [JsonPropertyName("message")] public string? Message { get; init; }
}

public sealed record ServiceMatch
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("match_score")] public double MatchScore { get; init; }
}

public sealed record SearchServicesResult
{
    [JsonPropertyName("query")] public string Query { get; init; } = "";
    [JsonPropertyName("matches")] public List<ServiceMatch> Matches { get; init; } = new();
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("tip")] public string? Tip { get; init; }
}

public sealed record CheapestOption
{
    [JsonPropertyName("country")] public string Country { get; init; } = "";
    [JsonPropertyName("country_name")] public string CountryName { get; init; } = "";
    [JsonPropertyName("price_usd")] public double PriceUsd { get; init; }
    [JsonPropertyName("stock")] public bool Stock { get; init; } = true;
}

public sealed record FindCheapestResult
{
    [JsonPropertyName("service")] public string Service { get; init; } = "";
    [JsonPropertyName("cheapest_options")] public List<CheapestOption> CheapestOptions { get; init; } = new();
    [JsonPropertyName("total_available_countries")] public int TotalAvailableCountries { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}

public sealed record TopEntry
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

public sealed record StatsResult
{
    [JsonPropertyName("window_days")] public int WindowDays { get; init; }
    [JsonPropertyName("balance_usd")] public double BalanceUsd { get; init; }
    [JsonPropertyName("total_orders")] public int TotalOrders { get; init; }
    [JsonPropertyName("successful_orders")] public int SuccessfulOrders { get; init; }
    [JsonPropertyName("success_rate")] public double? SuccessRate { get; init; }
    [JsonPropertyName("total_spend_usd")] public double TotalSpendUsd { get; init; }
    [JsonPropertyName("status_breakdown")] public Dictionary<string, int> StatusBreakdown { get; init; } = new();
    [JsonPropertyName("top_services")] public List<TopEntry> TopServices { get; init; } = new();
    [JsonPropertyName("top_countries")] public List<TopEntry> TopCountries { get; init; } = new();
    [JsonPropertyName("note")] public string? Note { get; init; }
}

// ─── Account ──────────────────────────────────────────────────────────────

public sealed record Balance
{
    [JsonPropertyName("balance_usd")] public double BalanceUsd { get; init; }
}

public sealed record Profile
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("email")] public string Email { get; init; } = "";
    [JsonPropertyName("telegram_linked")] public bool TelegramLinked { get; init; }
    [JsonPropertyName("telegram_username")] public string? TelegramUsername { get; init; }
    [JsonPropertyName("balance_usd")] public double BalanceUsd { get; init; }
    [JsonPropertyName("total_spent_usd")] public double TotalSpentUsd { get; init; }
    [JsonPropertyName("total_credits_usd")] public double TotalCreditsUsd { get; init; }
    [JsonPropertyName("total_orders")] public int TotalOrders { get; init; }
    [JsonPropertyName("active_api_keys")] public int ActiveApiKeys { get; init; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
}

public sealed record Transaction
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("amount")] public double Amount { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("order_id")] public string? OrderId { get; init; }
    [JsonPropertyName("balance_before")] public double BalanceBefore { get; init; }
    [JsonPropertyName("balance_after")] public double BalanceAfter { get; init; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
}

public sealed record TransactionsPage
{
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("limit")] public int Limit { get; init; }
    [JsonPropertyName("offset")] public int Offset { get; init; }
    [JsonPropertyName("transactions")] public List<Transaction> Transactions { get; init; } = new();
}

// ─── Rentals ──────────────────────────────────────────────────────────────
// Two tiers, no supplier names: "full_access" (local SIM inventory, any
// service) and "platform" (sourced via our global supplier network, one
// service per number). Both refund fully within 20 minutes, before the
// first SMS.

public sealed record RentalPricingTier
{
    [JsonPropertyName("rental_type")] public string RentalType { get; init; } = "";
    [JsonPropertyName("duration_hours")] public int DurationHours { get; init; }
    [JsonPropertyName("duration_label")] public string DurationLabel { get; init; } = "";
    [JsonPropertyName("base_price")] public double BasePrice { get; init; }
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("service_id")] public string ServiceId { get; init; } = "";
}

public sealed record RentalDurationPrice
{
    [JsonPropertyName("duration_hours")] public int DurationHours { get; init; }
    [JsonPropertyName("duration_label")] public string DurationLabel { get; init; } = "";
    [JsonPropertyName("price")] public double Price { get; init; }
}

public sealed record RentalAvailabilityCountry
{
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("country_name")] public string CountryName { get; init; } = "";
    [JsonPropertyName("flag")] public string? Flag { get; init; }
    [JsonPropertyName("available_count")] public int AvailableCount { get; init; }
    [JsonPropertyName("pricing")] public Dictionary<string, List<RentalDurationPrice>> Pricing { get; init; } = new();
    [JsonPropertyName("service_count")] public int? ServiceCount { get; init; }
    [JsonPropertyName("popular_services")] public List<string>? PopularServices { get; init; }
    [JsonPropertyName("min_price_per_day")] public double? MinPricePerDay { get; init; }
}

public sealed record RentalAvailabilityResult
{
    [JsonPropertyName("countries")] public List<RentalAvailabilityCountry> Countries { get; init; } = new();
    [JsonPropertyName("total_available")] public int TotalAvailable { get; init; }
    [JsonPropertyName("full_access_countries")] public List<System.Text.Json.JsonElement>? FullAccessCountries { get; init; }
    [JsonPropertyName("provider")] public string? Provider { get; init; }
}

public sealed record RentalCatalogService
{
    [JsonPropertyName("service_id")] public string ServiceId { get; init; } = "";
    [JsonPropertyName("service_name")] public string ServiceName { get; init; } = "";
    [JsonPropertyName("physical_count")] public int PhysicalCount { get; init; }
    [JsonPropertyName("our_price")] public double? OurPrice { get; init; }
    [JsonPropertyName("base_price")] public double? BasePrice { get; init; }
    [JsonPropertyName("popular")] public bool Popular { get; init; }
    [JsonPropertyName("icon_url")] public string? IconUrl { get; init; }
}

public sealed record RentalPriceResult
{
    [JsonPropertyName("price")] public double Price { get; init; }
    [JsonPropertyName("duration_hours")] public int DurationHours { get; init; }
}

public sealed record CreateRentalResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("rental_id")] public string RentalId { get; init; } = "";
    [JsonPropertyName("phone_number")] public string PhoneNumber { get; init; } = "";
    [JsonPropertyName("rental_type")] public string? RentalType { get; init; }
    [JsonPropertyName("service")] public string? Service { get; init; }
    [JsonPropertyName("duration")] public string? Duration { get; init; }
    [JsonPropertyName("price")] public double? Price { get; init; }
    [JsonPropertyName("started_at")] public string? StartedAt { get; init; }
    [JsonPropertyName("expires_at")] public string ExpiresAt { get; init; } = "";
    [JsonPropertyName("auto_renew")] public bool? AutoRenew { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("retail_cost")] public double? RetailCost { get; init; }
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

public sealed record Rental
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("phone_number")] public string PhoneNumber { get; init; } = "";
    [JsonPropertyName("rental_type")] public string RentalType { get; init; } = "";
    [JsonPropertyName("service_id")] public string? ServiceId { get; init; }
    [JsonPropertyName("duration_hours")] public int DurationHours { get; init; }
    [JsonPropertyName("started_at")] public string StartedAt { get; init; } = "";
    [JsonPropertyName("expires_at")] public string ExpiresAt { get; init; } = "";
    [JsonPropertyName("price")] public double Price { get; init; }
    [JsonPropertyName("auto_renew")] public bool AutoRenew { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("sms_received")] public int SmsReceived { get; init; }
    [JsonPropertyName("sms_forwarded")] public int SmsForwarded { get; init; }
    [JsonPropertyName("last_sms_at")] public string? LastSmsAt { get; init; }
    [JsonPropertyName("provider")] public string Provider { get; init; } = "";
}

public sealed record RentalActionResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("rental_id")] public string RentalId { get; init; } = "";
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("refund")] public double? Refund { get; init; }
    [JsonPropertyName("new_expires_at")] public string? NewExpiresAt { get; init; }
    [JsonPropertyName("price")] public double? Price { get; init; }
    [JsonPropertyName("hours_used")] public string? HoursUsed { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}

// ─── Proxies ──────────────────────────────────────────────────────────────

public sealed record ProxyCatalogCountry
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("ip_count")] public int IpCount { get; init; }
}

public sealed record ProxyCatalogPoolType
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("price_per_gb")] public double PricePerGb { get; init; }
    [JsonPropertyName("countries")] public List<ProxyCatalogCountry> Countries { get; init; } = new();
}

public sealed record ProxyListItem
{
    [JsonPropertyName("proxy_id")] public string ProxyId { get; init; } = "";
    [JsonPropertyName("pool_type")] public string PoolType { get; init; } = "";
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("country_name")] public string? CountryName { get; init; }
    [JsonPropertyName("gb_total")] public double GbTotal { get; init; }
    [JsonPropertyName("gb_used")] public double GbUsed { get; init; }
    [JsonPropertyName("gb_remaining")] public double GbRemaining { get; init; }
    [JsonPropertyName("proxy_host")] public string ProxyHost { get; init; } = "";
    [JsonPropertyName("proxy_port")] public int ProxyPort { get; init; }
    [JsonPropertyName("proxy_login")] public string ProxyLogin { get; init; } = "";
    [JsonPropertyName("proxy_password")] public string ProxyPassword { get; init; } = "";
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
}

public sealed record ProxyPurchaseResult
{
    [JsonPropertyName("proxy_id")] public string ProxyId { get; init; } = "";
    [JsonPropertyName("pool_type")] public string PoolType { get; init; } = "";
    [JsonPropertyName("gb_added")] public double GbAdded { get; init; }
    [JsonPropertyName("gb_remaining")] public double GbRemaining { get; init; }
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("proxy_login")] public string ProxyLogin { get; init; } = "";
    [JsonPropertyName("proxy_password")] public string ProxyPassword { get; init; } = "";
    [JsonPropertyName("proxy_host")] public string ProxyHost { get; init; } = "";
    [JsonPropertyName("proxy_port")] public int ProxyPort { get; init; }
    [JsonPropertyName("proxy_port_socks")] public int? ProxyPortSocks { get; init; }
    [JsonPropertyName("price")] public double Price { get; init; }
    [JsonPropertyName("balance")] public double? Balance { get; init; }
}

public sealed record ProxyRotateResult
{
    [JsonPropertyName("rotated")] public bool Rotated { get; init; }
    [JsonPropertyName("port")] public int Port { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = "";
}

public sealed record ProxyUsage
{
    [JsonPropertyName("gb_used")] public double GbUsed { get; init; }
    [JsonPropertyName("gb_remaining")] public double GbRemaining { get; init; }
    [JsonPropertyName("requests")] public long Requests { get; init; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; init; }
}

public sealed record ProxyUsageHistoryPoint
{
    [JsonPropertyName("date")] public string Date { get; init; } = "";
    [JsonPropertyName("gb")] public double Gb { get; init; }
    [JsonPropertyName("requests")] public long Requests { get; init; }
}

public sealed record ProxyUsageTotals
{
    [JsonPropertyName("gb")] public double Gb { get; init; }
    [JsonPropertyName("requests")] public long Requests { get; init; }
}

public sealed record ProxyUsageHistoryResult
{
    [JsonPropertyName("series")] public List<ProxyUsageHistoryPoint> Series { get; init; } = new();
    [JsonPropertyName("totals")] public ProxyUsageTotals Totals { get; init; } = new();
}

public sealed record ProxyTargetingResult
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    // true when city/state/zip/asn targeting was requested on a non-premium
    // pool: the customer's own funded GB burns 2x faster. Free on residential_premium.
    [JsonPropertyName("premium_2x")] public bool Premium2x { get; init; }
}

public sealed record ProxyTestResult
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("exit_ip")] public string? ExitIp { get; init; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; init; }
    [JsonPropertyName("country_name")] public string? CountryName { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("region")] public string? Region { get; init; }
    [JsonPropertyName("isp")] public string? Isp { get; init; }
    [JsonPropertyName("asn")] public string? Asn { get; init; }
    [JsonPropertyName("latency_ms")] public double? LatencyMs { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public sealed record ProxyLocationItem
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

public enum ProxyTargetBy { Country, State, City, Zip, Asn }
public enum ProxySession { Rotating, Sticky }
public enum ProxyProtocol { Http, Socks5 }
public enum ProxyEndpointFormat { HostPortUserPass, UserPassAtHostPort, Curl }

public sealed record ProxyEndpointResult
{
    [JsonPropertyName("proxy_id")] public string ProxyId { get; init; } = "";
    [JsonPropertyName("pool_type")] public string PoolType { get; init; } = "";
    [JsonPropertyName("host")] public string Host { get; init; } = "";
    [JsonPropertyName("port")] public int Port { get; init; }
    [JsonPropertyName("protocol")] public string Protocol { get; init; } = "HTTP";
    [JsonPropertyName("session")] public string Session { get; init; } = "rotating";
    [JsonPropertyName("sticky_ttl_minutes")] public int? StickyTtlMinutes { get; init; }
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("target_by")] public string TargetBy { get; init; } = "country";
    [JsonPropertyName("location_code")] public string? LocationCode { get; init; }
    [JsonPropertyName("premium_2x")] public bool Premium2x { get; init; }
    [JsonPropertyName("endpoints")] public List<string> Endpoints { get; init; } = new();
}

// ─── Session (manual registration, invite-only beta) ─────────────────────

public sealed record BrowserSessionTimelineEntry
{
    [JsonPropertyName("at")] public string At { get; init; } = "";
    [JsonPropertyName("event")] public string Event { get; init; } = "";
    [JsonPropertyName("detail")] public string? Detail { get; init; }
}

public sealed record BrowserSessionResult
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("service_name")] public string? ServiceName { get; init; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; init; }
    [JsonPropertyName("device_mode")] public string? DeviceMode { get; init; }
    [JsonPropertyName("with_proxy")] public bool? WithProxy { get; init; }
    // Only our own proxied viewer link is ever returned -- never a raw debug_url.
    [JsonPropertyName("viewer_url")] public string? ViewerUrl { get; init; }
    [JsonPropertyName("target_url")] public string? TargetUrl { get; init; }
    [JsonPropertyName("order_id")] public string? OrderId { get; init; }
    [JsonPropertyName("phone_number")] public string? PhoneNumber { get; init; }
    [JsonPropertyName("timeline")] public List<BrowserSessionTimelineEntry>? Timeline { get; init; }
}

// ─── Public tools ─────────────────────────────────────────────────────────

public sealed record NumberCheckResult
{
    [JsonPropertyName("valid")] public bool Valid { get; init; }
    [JsonPropertyName("e164")] public string E164 { get; init; } = "";
    [JsonPropertyName("national")] public string? National { get; init; }
    [JsonPropertyName("country_code")] public string CountryCode { get; init; } = "";
    [JsonPropertyName("country_name")] public string CountryName { get; init; } = "";
    [JsonPropertyName("country_prefix")] public string? CountryPrefix { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("carrier")] public string? Carrier { get; init; }
    [JsonPropertyName("line_type")] public string LineType { get; init; } = "";
    [JsonPropertyName("spam_risk")] public string SpamRisk { get; init; } = "";
    [JsonPropertyName("cached")] public bool Cached { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}

// ─── Webhooks ─────────────────────────────────────────────────────────────

public sealed record WebhookEndpoint
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("events")] public List<string> Events { get; init; } = new();
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("paused")] public bool Paused { get; init; }
    [JsonPropertyName("threshold")] public double? Threshold { get; init; }
    [JsonPropertyName("failure_count_consecutive")] public int FailureCountConsecutive { get; init; }
    [JsonPropertyName("last_delivered_at")] public string? LastDeliveredAt { get; init; }
    [JsonPropertyName("last_error_at")] public string? LastErrorAt { get; init; }
    [JsonPropertyName("last_error_code")] public string? LastErrorCode { get; init; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; init; } = "";
    // Only present on the CreateWebhookAsync response, exactly once. Store it
    // immediately -- it is never returned again by any other call.
    [JsonPropertyName("secret")] public string? Secret { get; init; }
}

public sealed record WebhookListResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("webhooks")] public List<WebhookEndpoint> Webhooks { get; init; } = new();
    [JsonPropertyName("count")] public int Count { get; init; }
}

public sealed record WebhookResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("webhook")] public WebhookEndpoint Webhook { get; init; } = new();
}

public sealed record DeleteWebhookResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("id")] public string Id { get; init; } = "";
}

public sealed record TestWebhookResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = "";
    [JsonPropertyName("delivery_id")] public string DeliveryId { get; init; } = "";
    [JsonPropertyName("event_id")] public string EventId { get; init; } = "";
    [JsonPropertyName("event_type")] public string EventType { get; init; } = "";
}

public sealed record WebhookDelivery
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("event_id")] public string EventId { get; init; } = "";
    [JsonPropertyName("event_type")] public string EventType { get; init; } = "";
    [JsonPropertyName("attempt")] public int Attempt { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("response_status")] public int? ResponseStatus { get; init; }
    [JsonPropertyName("response_body")] public string? ResponseBody { get; init; }
    [JsonPropertyName("scheduled_for")] public string? ScheduledFor { get; init; }
    [JsonPropertyName("delivered_at")] public string? DeliveredAt { get; init; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
    [JsonPropertyName("payload")] public System.Text.Json.JsonElement? Payload { get; init; }
}

public sealed record WebhookDeliveriesResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("deliveries")] public List<WebhookDelivery> Deliveries { get; init; } = new();
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("limit")] public int Limit { get; init; }
    [JsonPropertyName("offset")] public int Offset { get; init; }
}
