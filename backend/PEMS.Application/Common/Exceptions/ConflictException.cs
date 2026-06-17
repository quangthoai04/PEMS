namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with current state (e.g. duplicate). Maps to HTTP 409.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
