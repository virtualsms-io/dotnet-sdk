namespace VirtualSMS;

/// <summary>
/// Base exception for every error the VirtualSMS client raises. Catch this to
/// handle any SDK-level failure generically, or catch one of the typed
/// subclasses below for status-specific handling.
/// </summary>
public class VirtualSMSException : Exception
{
    public VirtualSMSException(string message) : base(message) { }
    public VirtualSMSException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>HTTP 401 - the API key is missing or invalid.</summary>
public class BadApiKeyException : VirtualSMSException
{
    public BadApiKeyException(string message) : base(message) { }
}

/// <summary>HTTP 402 - account balance is too low for the requested purchase.</summary>
public class InsufficientBalanceException : VirtualSMSException
{
    public InsufficientBalanceException(string message) : base(message) { }
}

/// <summary>HTTP 404 - the requested resource (order/rental/proxy/webhook id) does not exist.</summary>
public class NotFoundException : VirtualSMSException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// HTTP 429 - rate limit exceeded. Never auto-retry a 429: fighting the
/// server's own rate limiter is wrong. Slow down and back off yourself.
/// </summary>
public class RateLimitedException : VirtualSMSException
{
    public RateLimitedException(string message) : base(message) { }
}

/// <summary>
/// HTTP 5xx. On a GET/HEAD this is safe to retry (the SDK already attempted
/// its own bounded retry before surfacing this). On a mutating call
/// (POST/PUT/PATCH/DELETE) <see cref="IsMutating"/> is true and the operation
/// may have completed server-side despite the error -- verify with a read
/// call (ListOrdersAsync/GetOrderAsync/ListRentalsAsync/etc.) before retrying,
/// never retry blindly.
/// </summary>
public class ServerErrorException : VirtualSMSException
{
    public int StatusCode { get; }
    public bool IsMutating { get; }

    public ServerErrorException(int statusCode, bool isMutating, string message) : base(message)
    {
        StatusCode = statusCode;
        IsMutating = isMutating;
    }
}

/// <summary>Any other 4xx not covered by a more specific exception above.</summary>
public class VirtualSMSApiException : VirtualSMSException
{
    public int StatusCode { get; }

    public VirtualSMSApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Local, client-side guard: CancelOrderAsync/SwapNumberAsync pre-check the
/// order's cancel_available_at/swap_available_at cooldown timestamp before
/// calling the backend, and throw this instead of making a round-trip that
/// would just 4xx anyway. Not an HTTP error -- it never reaches the network.
/// </summary>
public class CooldownActiveException : VirtualSMSException
{
    public string Action { get; }
    public int WaitSeconds { get; }
    public string RetryAt { get; }

    public CooldownActiveException(string action, int waitSeconds, string retryAt)
        : base($"{(action == "cancel" ? "Cancel" : "Swap")} cooldown active. Try again in {waitSeconds} seconds (at {retryAt}).")
    {
        Action = action;
        WaitSeconds = waitSeconds;
        RetryAt = retryAt;
    }
}
