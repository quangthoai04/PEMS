namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when request validation fails. Maps to HTTP 400.
/// Optionally carries a machine-readable <see cref="ErrorCode"/> (e.g. EMAIL_RECIPIENT_DUPLICATE)
/// surfaced to the client in the error payload, matching <see cref="BusinessRuleException"/> and
/// <see cref="ConflictException"/>. A client that must react differently per failure — the compose
/// screen highlighting which recipient chip is wrong, say — cannot do that from a prose message alone.
/// </summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Stable failure code, or null for validation failures that need no machine handling.</summary>
    public string? ErrorCode { get; }

    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : this()
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    /// <summary>
    /// Field errors PLUS a stable code for the whole failure — for the (currently rare) case where
    /// every field in <paramref name="errors"/> stems from the SAME business-rule violation, so the
    /// client can look up one localized message (<c>errors:api.&lt;CODE&gt;</c>) instead of showing
    /// whatever language the raw messages happen to be written in.
    /// </summary>
    public ValidationException(IDictionary<string, string[]> errors, string? errorCode)
        : this(errors)
    {
        ErrorCode = errorCode;
    }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message, string errorCode)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
        ErrorCode = errorCode;
    }
}
