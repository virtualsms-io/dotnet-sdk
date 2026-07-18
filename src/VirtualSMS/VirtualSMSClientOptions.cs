using System.Net.Http;

namespace VirtualSMS;

/// <summary>
/// Optional constructor settings for <see cref="VirtualSMSClient"/>.
/// </summary>
public sealed class VirtualSMSClientOptions
{
    /// <summary>
    /// Override the API base URL. Defaults to
    /// <see cref="VirtualSMSClient.DefaultBaseUrl"/>, or the
    /// <c>VIRTUALSMS_BASE_URL</c> environment variable when set.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>Request timeout in seconds. Defaults to 30.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Supply your own <see cref="HttpClient"/> (e.g. one wired through
    /// <c>IHttpClientFactory</c>). When set, the client does not own or
    /// dispose it, and <see cref="TimeoutSeconds"/> is ignored (set the
    /// timeout on your own HttpClient instead).
    /// </summary>
    public HttpClient? HttpClient { get; init; }
}
