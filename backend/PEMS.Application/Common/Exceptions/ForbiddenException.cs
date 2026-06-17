namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when the current user is authenticated but lacks permission / scope.
/// Maps to HTTP 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenException(string message) : base(message) { }
}
