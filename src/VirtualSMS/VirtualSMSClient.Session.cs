using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    /// <summary>
    /// Start a country-matched cloud browser session the caller drives
    /// manually via the returned ViewerUrl. Beta, invite-only feature: on a
    /// 403/404/503 (beta-gate signals) this method catches the error and
    /// raises a clean "invite-only beta" message instead of a raw HTTP error.
    /// </summary>
    public async Task<BrowserSessionResult> StartManualRegistrationSessionAsync(
        string? serviceName = null,
        string? country = null,
        string? deviceMode = null,
        bool? withProxy = null,
        string? targetUrl = null,
        string? orderId = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedWithProxy = withProxy ?? (country is not null);

        try
        {
            using var doc = await MutateAsync<JsonDocument>(
                HttpMethod.Post,
                "browser-sessions/start",
                new
                {
                    serviceName,
                    country,
                    deviceMode,
                    withProxy = resolvedWithProxy,
                    targetUrl,
                    orderId,
                    mode = mode ?? "fresh",
                },
                requireAuth: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var root = doc.RootElement;
            var sessionEl = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("session", out var s) ? s : root;
            return JsonSerializer.Deserialize<BrowserSessionResult>(sessionEl.GetRawText(), JsonOptions)
                ?? throw new VirtualSMSException("Empty session response from the server.");
        }
        catch (Exception ex) when (IsSessionsUnavailable(ex))
        {
            throw new VirtualSMSException(
                "Manual registration sessions are an invite-only beta feature. Join https://t.me/VirtualSMS_io to request access.", ex);
        }
    }

    private static bool IsSessionsUnavailable(Exception ex) => ex switch
    {
        NotFoundException => true,
        VirtualSMSApiException api => api.StatusCode == 403,
        ServerErrorException srv => srv.StatusCode == 503,
        _ => false,
    };
}
