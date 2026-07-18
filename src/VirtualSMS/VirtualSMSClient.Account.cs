using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    /// <summary>Check account balance.</summary>
    public Task<Balance> GetBalanceAsync(CancellationToken cancellationToken = default)
        => GetAsync<Balance>("customer/balance", null, requireAuth: true, cancellationToken);

    /// <summary>Full account profile.</summary>
    public Task<Profile> GetProfileAsync(CancellationToken cancellationToken = default)
        => GetAsync<Profile>("customer/profile", null, requireAuth: true, cancellationToken);

    /// <summary>Paginated transaction history.</summary>
    public Task<TransactionsPage> GetTransactionsAsync(
        string? type = null,
        string? from = null,
        string? to = null,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
        => GetAsync<TransactionsPage>(
            "customer/transactions",
            Q(("type", type), ("from", from), ("to", to), ("limit", limit.ToString()), ("offset", offset.ToString())),
            requireAuth: true,
            cancellationToken);

    /// <summary>
    /// Aggregated usage stats over a lookback window. Client-side: calls
    /// GetBalanceAsync + ListOrdersAsync in parallel, then aggregates locally
    /// (status/service/country breakdowns, spend excluding cancelled orders,
    /// success rate over terminal-state orders only).
    /// </summary>
    public async Task<StatsResult> GetStatsAsync(int sinceDays = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-sinceDays);

        var balanceTask = GetBalanceAsync(cancellationToken);
        var ordersTask = ListOrdersAsync(null, cancellationToken);
        await Task.WhenAll(balanceTask, ordersTask).ConfigureAwait(false);
        var balance = balanceTask.Result;
        var orders = ordersTask.Result;

        var inWindow = orders.Where(o => DateTimeOffset.TryParse(o.CreatedAt, out var ts) && ts >= cutoff).ToList();

        var byStatus = new Dictionary<string, int>();
        var byService = new Dictionary<string, int>();
        var byCountry = new Dictionary<string, int>();
        double totalSpend = 0;
        var successful = 0;
        var terminal = 0;

        var terminalStatuses = new HashSet<string>(StringComparer.Ordinal) { "completed", "sms_received", "expired", "cancelled" };

        foreach (var o in inWindow)
        {
            byStatus[o.Status] = byStatus.GetValueOrDefault(o.Status) + 1;
            if (!string.IsNullOrEmpty(o.Service)) byService[o.Service] = byService.GetValueOrDefault(o.Service) + 1;
            if (!string.IsNullOrEmpty(o.Country)) byCountry[o.Country] = byCountry.GetValueOrDefault(o.Country) + 1;

            if (o.Status != "cancelled" && o.Price.HasValue)
            {
                totalSpend += o.Price.Value;
            }

            if (terminalStatuses.Contains(o.Status))
            {
                terminal++;
                if (o.Status is "completed" or "sms_received") successful++;
            }
        }

        static List<TopEntry> TopEntries(Dictionary<string, int> rec, int n = 5) =>
            rec.OrderByDescending(kv => kv.Value).Take(n).Select(kv => new TopEntry { Key = kv.Key, Count = kv.Value }).ToList();

        return new StatsResult
        {
            WindowDays = sinceDays,
            BalanceUsd = balance.BalanceUsd,
            TotalOrders = inWindow.Count,
            SuccessfulOrders = successful,
            SuccessRate = terminal > 0 ? Math.Round(successful / (double)terminal * 1000) / 10 : null,
            TotalSpendUsd = Math.Round(totalSpend * 100) / 100,
            StatusBreakdown = byStatus,
            TopServices = TopEntries(byService),
            TopCountries = TopEntries(byCountry),
            Note = orders.Count >= 50
                ? "Server caps order history at 50 rows. Stats may undercount if your activity exceeds 50 orders in the window."
                : null,
        };
    }
}
