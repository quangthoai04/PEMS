namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist. Maps to HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, object key)
        : base($"{entity} ({key}) was not found.") { }
}
