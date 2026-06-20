namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with current state (e.g. duplicate). Maps to HTTP 409.
/// Optionally carries a machine-readable <see cref="ErrorCode"/> (e.g. DUPLICATE_VISIT_REQUEST)
/// surfaced to the client in the error payload.
/// </summary>
public class ConflictException : Exception
{
    public string? ErrorCode { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
}
