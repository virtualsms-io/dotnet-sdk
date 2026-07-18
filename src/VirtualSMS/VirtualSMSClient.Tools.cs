using System.Threading;

namespace VirtualSMS;

public sealed partial class VirtualSMSClient
{
    /// <summary>Carrier + line-type lookup for an arbitrary E.164 number (e.g. "+447911123456"). Public, no auth.</summary>
    public Task<NumberCheckResult> CheckNumberAsync(string number, CancellationToken cancellationToken = default)
        => GetAsync<NumberCheckResult>("tools/number-check", Q(("number", number)), requireAuth: false, cancellationToken);
}
