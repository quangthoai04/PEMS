using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Common;

public sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
