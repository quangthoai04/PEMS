namespace PEMS.Application.Common.Interfaces;

/// <summary>Abstraction over the system clock (UTC) to keep handlers testable.</summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
}
