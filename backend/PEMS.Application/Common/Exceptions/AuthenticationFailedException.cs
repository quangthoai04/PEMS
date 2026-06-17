namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when authentication cannot proceed (bad credentials, locked / inactive
/// account, wrong portal, invalid token, ...). Maps to HTTP 401.
/// <para>
/// <see cref="Message"/> is always a generic, safe-to-display string so the API
/// never reveals whether an email exists, whether a password was wrong, or why an
/// account is blocked. The real reason is carried separately in
/// <see cref="InternalReason"/> for server-side logging only.
/// </para>
/// </summary>
public class AuthenticationFailedException : Exception
{
    /// <summary>Detailed reason for internal logs. Never returned to the client.</summary>
    public string? InternalReason { get; }

    public AuthenticationFailedException(string publicMessage, string? internalReason = null)
        : base(publicMessage)
    {
        InternalReason = internalReason;
    }
}
