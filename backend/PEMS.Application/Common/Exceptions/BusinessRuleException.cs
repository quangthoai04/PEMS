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
}
