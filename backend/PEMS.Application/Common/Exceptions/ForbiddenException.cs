namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when the current user is authenticated but lacks permission / scope.
/// Maps to HTTP 403.
///
/// <para>
/// Optionally carries a machine-readable <see cref="ErrorCode"/>, matching <see cref="NotFoundException"/>,
/// <see cref="ConflictException"/> and <see cref="BusinessRuleException"/>. Most refusals need no code —
/// "you may not do this" is the whole answer. Some do: where a client must be able to tell WHICH refusal
/// it hit, a prose message is not something it can branch on, and the alternative is parsing Vietnamese.
/// </para>
/// </summary>
public class ForbiddenException : Exception
{
    /// <summary>Stable failure code, or null when the prose message is enough.</summary>
    public string? ErrorCode { get; }

    public ForbiddenException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
}
