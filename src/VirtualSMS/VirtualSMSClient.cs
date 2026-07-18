using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace VirtualSMS;

/// <summary>
/// Native .NET client for the VirtualSMS REST API v1: SMS verification on
/// real carrier SIM cards (not VoIP), number rentals, and residential/
/// mobile/datacenter proxies. Get your API key at https://virtualsms.io
/// (Dashboard -> API Keys). Docs: https://virtualsms.io/docs
/// </summary>
public sealed partial class VirtualSMSClient : IDisposable
{
    /// <summary>Default API base URL. Every endpoint hangs off this root.</summary>
    public const string DefaultBaseUrl = "https://virtualsms.io/api/v1";

    // GET-only bounded retry: a 5xx (or a dropped connection) on a mutating
    // request does NOT mean the operation failed server-side -- it may have
    // gone through right before the error was returned. Only idempotent
    // reads get this safety net, and only for failures that are plausibly
    // transient. Never retries 4xx (401/402/404/429 are not transient, and
    // 429 retried blindly would actively fight the server's own rate limiter).
    internal const int GetRetryMaxAttempts = 3; // 1 initial try + up to 2 retries
    private const int GetRetryBaseDelayMs = 300;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Mutating-call bodies are built from anonymous objects with
        // optional fields left null (e.g. PATCH webhook update). Omit them
        // entirely from the wire payload rather than sending explicit
        // `"field": null`, matching JSON.stringify's own undefined-drops
        // behavior in the canonical (JS) client this SDK mirrors.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    public VirtualSMSClient(string apiKey, VirtualSMSClientOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("apiKey is required. Get one at https://virtualsms.io", nameof(apiKey));
        }

        _apiKey = apiKey;
        options ??= new VirtualSMSClientOptions();

        _baseUrl = (options.BaseUrl
            ?? Environment.GetEnvironmentVariable("VIRTUALSMS_BASE_URL")
            ?? DefaultBaseUrl).TrimEnd('/');

        _ownsHttpClient = options.HttpClient is null;
        _http = options.HttpClient ?? new HttpClient();
        if (_ownsHttpClient)
        {
            _http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds ?? 30);
        }
    }

    /// <summary>The API key this client was constructed with, if any.</summary>
    public string? ApiKey => _apiKey;

    /// <summary>The base URL this client sends requests to.</summary>
    public string BaseUrl => _baseUrl;

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    private void RequireApiKey()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new VirtualSMSException(
                "An API key is required for this operation. Get your API key at https://virtualsms.io");
        }
    }

    // ─── URI + query building ──────────────────────────────────────────────

    private Uri BuildUri(string path, IEnumerable<KeyValuePair<string, string?>>? query)
    {
        var sb = new StringBuilder(_baseUrl);
        if (!path.StartsWith('/')) sb.Append('/');
        sb.Append(path);

        if (query is not null)
        {
            var first = true;
            foreach (var (key, value) in query)
            {
                if (value is null) continue;
                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            }
        }

        return new Uri(sb.ToString());
    }

    private static IEnumerable<KeyValuePair<string, string?>> Q(params (string Key, string? Value)[] pairs)
        => pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value));

    // ─── Core request plumbing ──────────────────────────────────────────────

    private async Task<T> GetAsync<T>(
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        bool requireAuth = false,
        CancellationToken cancellationToken = default)
    {
        if (requireAuth) RequireApiKey();
        var uri = BuildUri(path, query);

        for (var attempt = 1; attempt <= GetRetryMaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuthHeader(request);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt < GetRetryMaxAttempts)
                {
                    await Task.Delay(RetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                throw new VirtualSMSException($"Network error calling GET {path}: {ex.Message}", ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return await DeserializeAsync<T>(response, cancellationToken).ConfigureAwait(false);
                }

                if ((int)response.StatusCode >= 500 && attempt < GetRetryMaxAttempts)
                {
                    await Task.Delay(RetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await ThrowForErrorAsync(response, isMutating: false, cancellationToken).ConfigureAwait(false);
            }
        }

        // Unreachable: the loop above always either returns or throws.
        throw new VirtualSMSException($"GET {path} failed after {GetRetryMaxAttempts} attempts.");
    }

    /// <summary>
    /// Mutating call (POST/PATCH/DELETE). Never retried by the SDK: a 5xx on a
    /// purchase/cancel/rotate/extend/etc. call does not prove the operation
    /// failed server-side. Auto-generates X-Idempotency-Key unless the caller
    /// wants their own dedup key -- pass it in the body if the endpoint
    /// supports it (buy_proxy does).
    /// </summary>
    private async Task<T> MutateAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        bool requireAuth = true,
        CancellationToken cancellationToken = default)
    {
        if (requireAuth) RequireApiKey();
        var uri = BuildUri(path, null);

        using var request = new HttpRequestMessage(method, uri);
        ApplyAuthHeader(request);
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }
        else if (method == HttpMethod.Post)
        {
            // Some endpoints (rotate/cancel/extend/test) accept an empty JSON body.
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new VirtualSMSException(
                $"Network error on {method} {path}. This is a mutating call: it may have completed " +
                "server-side despite the error. Do NOT blindly retry -- first verify with a read call " +
                "(list_orders/get_order/list_rentals/list_proxies/etc.) whether it actually succeeded. " +
                $"Details: {ex.Message}", ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return await DeserializeAsync<T>(response, cancellationToken).ConfigureAwait(false);
            }
            await ThrowForErrorAsync(response, isMutating: true, cancellationToken).ConfigureAwait(false);
        }

        throw new VirtualSMSException($"{method} {path} failed."); // unreachable
    }

    private void ApplyAuthHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static TimeSpan RetryDelay(int attemptNumber)
        => TimeSpan.FromMilliseconds(GetRetryBaseDelayMs * Math.Pow(2, attemptNumber - 1));

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Buffered read rather than a live stream: HttpContent streams (e.g.
        // chunked transfer) don't reliably support Stream.Length, so an
        // empty-body check against stream.Length would throw on some
        // responses instead of just telling us the body is empty.
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return default!;
        }
        var result = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        return result!;
    }

    private static async Task ThrowForErrorAsync(HttpResponseMessage response, bool isMutating, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        string message;
        try
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            message = ExtractErrorMessage(raw) ?? (string.IsNullOrWhiteSpace(raw) ? (response.ReasonPhrase ?? "unknown error") : raw);
        }
        catch
        {
            message = response.ReasonPhrase ?? "unknown error";
        }

        switch (status)
        {
            case 401:
                throw new BadApiKeyException("Invalid or missing API key. Get one at https://virtualsms.io");
            case 402:
                throw new InsufficientBalanceException("Insufficient balance. Top up at https://virtualsms.io");
            case 404:
                throw new NotFoundException($"Not found: {message}");
            case 429:
                throw new RateLimitedException("Rate limit exceeded. Slow down; never auto-retry a 429.");
        }

        if (status >= 500)
        {
            var errMessage = isMutating
                ? $"VirtualSMS server error ({status}) on a request that may have made a purchase or changed " +
                  "state. DO NOT blindly retry: first verify with a read call (list_orders/get_order/" +
                  $"list_rentals/etc.) whether it actually succeeded, as you may have been charged. Details: {message}"
                : $"VirtualSMS server error ({status}). Safe to retry this read-only request. Details: {message}";
            throw new ServerErrorException(status, isMutating, errMessage);
        }

        throw new VirtualSMSApiException(status, $"API error ({status}): {message}");
    }

    // No-supplier-name rule applies to error messages too: only the raw
    // backend message/error field is ever surfaced, never internal debug data.
    private static string? ExtractErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            {
                return m.GetString();
            }
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
            {
                return e.GetString();
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
