namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when a domain/business rule is violated. Maps to HTTP 422.
/// Optionally carries a machine-readable <see cref="ErrorCode"/> (e.g. CAMPUS_INACTIVE)
/// surfaced to the client in the error payload.
/// </summary>
public class BusinessRuleException : Exception
{
    public string? ErrorCode { get; }

    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string message, string errorCode) : base(message) => ErrorCode = errorCode;

    /// <summary>
    /// For a rule violation that was DETECTED through a lower-level failure — a missing table read as
    /// "this database is not configured", say. The operator-facing message and code describe the rule;
    /// the inner exception keeps the provider's own error for the server log, which is the only place
    /// with any use for it.
    /// </summary>
    public BusinessRuleException(string message, string errorCode, System.Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;
}
