namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with current state (e.g. duplicate). Maps to HTTP 409.
/// Optionally carries a machine-readable <see cref="ErrorCode"/> (e.g. DUPLICATE_VISIT_REQUEST)
/// surfaced to the client in the error payload.
/// </summary>
public class ConflictException : Exception
{
    public string? ErrorCode { get; }

    /// <summary>
    /// Optional structured payload surfaced to the client under <c>data</c> (e.g. the existing
    /// Staff Leader's id/name/email/status so the UI can render a specific warning). Never
    /// include sensitive fields here — it is serialized verbatim into the error response.
    /// </summary>
    public new object? Data { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, string errorCode) : base(message) => ErrorCode = errorCode;

    public ConflictException(string message, string errorCode, object? data) : base(message)
    {
        ErrorCode = errorCode;
        Data = data;
    }
}
