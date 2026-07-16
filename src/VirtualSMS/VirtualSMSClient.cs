using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VirtualSMS;

/// <summary>
/// Client for the VirtualSMS API: SMS verification using real physical SIM cards, not VoIP.
/// Get your API key at https://virtualsms.io (Settings → API Keys).
/// API docs: https://virtualsms.io/api
/// </summary>
public class VirtualSMSClient
{
    private const string DefaultBaseUrl = "https://virtualsms.io/stubs/handler_api.php";

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    public VirtualSMSClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? DefaultBaseUrl;
        _http = httpClient ?? new HttpClient();
    }

    /// <summary>Get current account balance in USD.</summary>
    public async Task<double> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync("getBalance", cancellationToken: cancellationToken);
        if (result.StartsWith("ACCESS_BALANCE:"))
        {
            return double.Parse(result.Split(':')[1]);
        }
        throw new VirtualSMSException(result);
    }

    /// <summary>Request a phone number for SMS verification.</summary>
    /// <param name="service">Service code (e.g. "wa" for WhatsApp, "tg" for Telegram).</param>
    /// <param name="country">Country ID (default: 187 = US; 22 = UK, 12 = Germany).</param>
    public async Task<Activation> GetNumberAsync(string service, int country = 187, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync("getNumber", new() { ["service"] = service, ["country"] = country.ToString() }, cancellationToken);
        if (result.StartsWith("ACCESS_NUMBER:"))
        {
            var parts = result.Split(':');
            return new Activation(int.Parse(parts[1]), parts[2], service, country);
        }
        if (result == "NO_NUMBERS")
        {
            throw new NoNumbersException($"No numbers available for {service} in country {country}");
        }
        throw new VirtualSMSException(result);
    }

    /// <summary>Check the status of an activation.</summary>
    public async Task<ActivationStatus> GetStatusAsync(int activationId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync("getStatus", new() { ["id"] = activationId.ToString() }, cancellationToken);
        return result switch
        {
            "STATUS_WAIT_CODE" => new ActivationStatus("waiting", null),
            "STATUS_CANCEL" => new ActivationStatus("cancelled", null),
            _ when result.StartsWith("STATUS_OK:") => new ActivationStatus("received", result.Split(':')[1]),
            _ => new ActivationStatus(result, null),
        };
    }

    /// <summary>Mark activation as done (code used successfully).</summary>
    public Task<string> DoneAsync(int activationId, CancellationToken cancellationToken = default)
        => RequestAsync("setStatus", new() { ["id"] = activationId.ToString(), ["status"] = "6" }, cancellationToken);

    /// <summary>Cancel an activation and get an automatic refund.</summary>
    public Task<string> CancelAsync(int activationId, CancellationToken cancellationToken = default)
        => RequestAsync("setStatus", new() { ["id"] = activationId.ToString(), ["status"] = "8" }, cancellationToken);

    /// <summary>Poll for the SMS code until it arrives or the timeout elapses.</summary>
    /// <param name="timeoutSeconds">Max wait in seconds (default: 300).</param>
    /// <param name="pollIntervalSeconds">Seconds between checks (default: 5).</param>
    public async Task<string?> WaitForCodeAsync(int activationId, int timeoutSeconds = 300, int pollIntervalSeconds = 5, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var status = await GetStatusAsync(activationId, cancellationToken);
            if (status.Code is not null) return status.Code;
            if (status.Status == "cancelled") return null;
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken);
        }
        return null;
    }

    private async Task<string> RequestAsync(string action, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"action={action}", $"api_key={_apiKey}" };
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                query.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }
        var url = $"{_baseUrl}?{string.Join("&", query)}";
        var response = await _http.GetStringAsync(url, cancellationToken);
        return response.Trim();
    }
}

public record Activation(int ActivationId, string Phone, string Service, int Country);

public record ActivationStatus(string Status, string? Code);

public class VirtualSMSException : Exception
{
    public VirtualSMSException(string message) : base(message) { }
}

public class NoNumbersException : VirtualSMSException
{
    public NoNumbersException(string message) : base(message) { }
}
