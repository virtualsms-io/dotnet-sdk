using System.Net.Http;
using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    /// <summary>List the account's webhook subscriptions. Never carries a secret (only CreateWebhookAsync does, once).</summary>
    public Task<WebhookListResult> ListWebhooksAsync(CancellationToken cancellationToken = default)
        => GetAsync<WebhookListResult>("customer/webhooks", null, requireAuth: true, cancellationToken);

    /// <summary>
    /// Create a webhook subscription. The response's Webhook.Secret is
    /// returned exactly ONCE, on create only -- store it immediately, it is
    /// never surfaced again by any other call.
    /// </summary>
    public Task<WebhookResult> CreateWebhookAsync(
        string url,
        List<string> events,
        string? description = null,
        double? threshold = null,
        CancellationToken cancellationToken = default)
        => MutateAsync<WebhookResult>(
            HttpMethod.Post,
            "customer/webhooks",
            new { url, description, events, threshold },
            requireAuth: true,
            cancellationToken: cancellationToken);

    /// <summary>Get one webhook (no secret).</summary>
    public Task<WebhookResult> GetWebhookAsync(string id, CancellationToken cancellationToken = default)
        => GetAsync<WebhookResult>($"customer/webhooks/{Uri.EscapeDataString(id)}", null, requireAuth: true, cancellationToken);

    /// <summary>
    /// Partial update (url/description/events/threshold/active/paused). At
    /// least one field is required. Un-pausing (paused: false when
    /// previously true) resets the consecutive-failure counter server-side.
    /// </summary>
    public Task<WebhookResult> UpdateWebhookAsync(
        string id,
        string? url = null,
        string? description = null,
        List<string>? events = null,
        double? threshold = null,
        bool? active = null,
        bool? paused = null,
        CancellationToken cancellationToken = default)
    {
        if (url is null && description is null && events is null && threshold is null && active is null && paused is null)
        {
            throw new ArgumentException("At least one field (url/description/events/threshold/active/paused) is required.");
        }
        return MutateAsync<WebhookResult>(
            HttpMethod.Patch,
            $"customer/webhooks/{Uri.EscapeDataString(id)}",
            new { url, description, events, threshold, active, paused },
            requireAuth: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>Delete a webhook.</summary>
    public Task<DeleteWebhookResult> DeleteWebhookAsync(string id, CancellationToken cancellationToken = default)
        => MutateAsync<DeleteWebhookResult>(HttpMethod.Delete, $"customer/webhooks/{Uri.EscapeDataString(id)}", null, requireAuth: true, cancellationToken: cancellationToken);

    /// <summary>Fire a synthetic test event through the real dispatcher. Requires the webhook to be active and not paused.</summary>
    public Task<TestWebhookResult> TestWebhookAsync(string id, CancellationToken cancellationToken = default)
        => MutateAsync<TestWebhookResult>(HttpMethod.Post, $"customer/webhooks/{Uri.EscapeDataString(id)}/test", null, requireAuth: true, cancellationToken: cancellationToken);

    /// <summary>List recent delivery attempts for a webhook.</summary>
    public Task<WebhookDeliveriesResult> ListWebhookDeliveriesAsync(string id, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        => GetAsync<WebhookDeliveriesResult>(
            $"customer/webhooks/{Uri.EscapeDataString(id)}/deliveries",
            Q(("limit", limit.ToString()), ("offset", offset.ToString())),
            requireAuth: true,
            cancellationToken);
}
