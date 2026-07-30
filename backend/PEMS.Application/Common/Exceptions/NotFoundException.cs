namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist. Maps to HTTP 404.
/// Optionally carries a machine-readable <see cref="ErrorCode"/>, matching
/// <see cref="ConflictException"/> and <see cref="BusinessRuleException"/>, for the cases where "not
/// found" is a condition the caller must handle specifically rather than merely report — a missing
/// email template, say, which an operator fixes by seeding it rather than by retrying.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Stable failure code, or null when the prose message is enough.</summary>
    public string? ErrorCode { get; }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, object key)
        : base($"{entity} ({key}) was not found.") { }

    public NotFoundException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
}
