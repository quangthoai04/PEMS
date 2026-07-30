namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// Supplies the <c>Idempotency-Key</c> of the request being handled.
///
/// <para>
/// An abstraction rather than a direct <c>HttpContext</c> read, for the same reason
/// <c>ICurrentUserService</c> is one: the Application layer decides what the key MEANS, and the API layer
/// knows where it came from. It also keeps the state machine testable without a web host.
/// </para>
/// </summary>
public interface IIdempotencyKeyAccessor
{
    /// <summary>The raw header value, or null when the caller sent none.</summary>
    string? CurrentKey { get; }
}
